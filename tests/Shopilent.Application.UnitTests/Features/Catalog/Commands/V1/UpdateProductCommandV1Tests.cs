using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.UpdateProduct.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class UpdateProductCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Helper method to create a product with a specific ID for testing
    /// </summary>
    private static Product CreateProductWithId(Guid id, string name, string slug, decimal price, string currency = "USD")
    {
        var slugValue = Slug.Create(slug).Value;
        var moneyValue = Money.Create(price, currency).Value;
        var product = Product.Create(name, slugValue, moneyValue).Value;
        var idProperty = typeof(Product).GetProperty("Id");
        idProperty?.SetValue(product, id);
        return product;
    }

    public UpdateProductCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockProductWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCategoryWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockAttributeWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.MockS3StorageService.Object);
        services.AddTransient(sp => Fixture.MockImageService.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateProductCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<UpdateProductCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<UpdateProductCommandV1>, UpdateProductCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductCommandV1
        {
            Id = productId,
            Name = "Updated Product Name",
            Description = "Updated product description",
            BasePrice = 39.99m,
            Slug = "updated-product",
            Sku = "UPD-001",
            IsActive = true
        };

        // Create existing product with different values
        var existingProduct = CreateProductWithId(productId, "Old Product", "old-product", 29.99m);
        // Set SKU via the Update method
        var oldSlug = Slug.Create("old-product").Value;
        var oldPrice = Money.Create(29.99m, "USD").Value;
        existingProduct.Update("Old Product", oldSlug, oldPrice, null, "OLD-001");

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock slug doesn't exist for other products
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, productId, CancellationToken))
            .ReturnsAsync(false);

        // Mock SKU doesn't exist for other products
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SkuExistsAsync(command.Sku, productId, CancellationToken))
            .ReturnsAsync(false);

        // Setup authenticated user for audit info
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // No need to mock UpdateAsync - the handler modifies the entity in-memory
        // and saves via UnitOfWork.SaveChangesAsync

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the product was updated correctly (check the existing product instance)
        existingProduct.Name.Should().Be(command.Name);
        existingProduct.Description.Should().Be(command.Description);
        existingProduct.BasePrice.Amount.Should().Be(command.BasePrice);
        existingProduct.Slug.Value.Should().Be(command.Slug);
        existingProduct.Sku.Should().Be(command.Sku);
        existingProduct.IsActive.Should().Be(command.IsActive.Value);

        // Verify response
        var response = result.Value;
        response.Id.Should().Be(productId);
        response.Name.Should().Be(command.Name);
        response.Description.Should().Be(command.Description);
        response.BasePrice.Should().Be(command.BasePrice);
        response.Slug.Should().Be(command.Slug);
        response.Sku.Should().Be(command.Sku);
        response.IsActive.Should().Be(command.IsActive.Value);

        // Verify the product was saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentProduct_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new UpdateProductCommandV1
        {
            Id = nonExistentId,
            Name = "Updated Product",
            BasePrice = 19.99m,
            Slug = "updated-product"
        };

        // Mock that product doesn't exist
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(nonExistentId, CancellationToken))
            .ReturnsAsync((Product)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ProductErrors.NotFound(nonExistentId).Code);

        // Verify the product was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateSlug_ReturnsConflict()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductCommandV1
        {
            Id = productId,
            Name = "Updated Product",
            BasePrice = 19.99m,
            Slug = "existing-slug"
        };

        // Create existing product with different slug
        var existingProduct = CreateProductWithId(productId, "Test Product", "original-slug", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock slug already exists for another product
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, productId, CancellationToken))
            .ReturnsAsync(true);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ProductErrors.DuplicateSlug(command.Slug).Code);

        // Verify the product was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateSku_ReturnsConflict()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductCommandV1
        {
            Id = productId,
            Name = "Updated Product",
            BasePrice = 19.99m,
            Slug = "updated-product",
            Sku = "EXISTING-SKU"
        };

        // Create existing product with different SKU
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);
        // Set SKU via the Update method
        var originalSlug = Slug.Create("test-product").Value;
        var originalPrice = Money.Create(29.99m, "USD").Value;
        existingProduct.Update("Test Product", originalSlug, originalPrice, null, "ORIGINAL-SKU");

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock slug doesn't exist
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, productId, CancellationToken))
            .ReturnsAsync(false);

        // Mock SKU already exists for another product
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SkuExistsAsync(command.Sku, productId, CancellationToken))
            .ReturnsAsync(true);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ProductErrors.DuplicateSku(command.Sku).Code);

        // Verify the product was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SameSlugAsExisting_AllowsUpdate()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductCommandV1
        {
            Id = productId,
            Name = "Updated Product Name",
            BasePrice = 39.99m,
            Slug = "existing-slug" // Same as current product slug
        };

        // Create existing product with the same slug
        var existingProduct = CreateProductWithId(productId, "Original Product", "existing-slug", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Note: No slug existence check should be made since the slug hasn't changed

        // No need to mock UpdateAsync - the handler modifies the entity in-memory
        // and saves via UnitOfWork.SaveChangesAsync

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the product was updated (check the existing product instance)
        existingProduct.Name.Should().Be(command.Name);
        existingProduct.BasePrice.Amount.Should().Be(command.BasePrice);

        // Verify slug existence was not checked (since it's the same)
        Fixture.MockProductWriteRepository.Verify(
            repo => repo.SlugExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SameSkuAsExisting_AllowsUpdate()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductCommandV1
        {
            Id = productId,
            Name = "Updated Product Name",
            BasePrice = 39.99m,
            Slug = "updated-product",
            Sku = "EXISTING-SKU" // Same as current product SKU
        };

        // Create existing product with the same SKU
        var existingProduct = CreateProductWithId(productId, "Original Product", "original-product", 29.99m);
        // Set SKU via the Update method
        var originalSlug = Slug.Create("original-product").Value;
        var originalPrice = Money.Create(29.99m, "USD").Value;
        existingProduct.Update("Original Product", originalSlug, originalPrice, null, "EXISTING-SKU");

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock slug doesn't exist
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, productId, CancellationToken))
            .ReturnsAsync(false);

        // Note: No SKU existence check should be made since the SKU hasn't changed

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue($"Expected success but got failure: {result.Error?.Message}");

        // Verify the product was updated (check the existing product instance)
        existingProduct.Name.Should().Be(command.Name);
        existingProduct.Sku.Should().Be(command.Sku);
        existingProduct.BasePrice.Amount.Should().Be(command.BasePrice);

        // Verify SKU existence was not checked (since it's the same)
        Fixture.MockProductWriteRepository.Verify(
            repo => repo.SkuExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidSlug_ReturnsValidationError()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductCommandV1
        {
            Id = productId,
            Name = "Updated Product",
            BasePrice = 19.99m,
            Slug = "" // Invalid empty slug
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        // The exact error from Slug.Create validation for empty/invalid slugs
        result.Error.Code.Should().Be("Category.SlugRequired");

        // Verify the product was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidPrice_ReturnsValidationError()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductCommandV1
        {
            Id = productId,
            Name = "Updated Product",
            BasePrice = -10.00m, // Invalid negative price
            Slug = "updated-product"
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock slug doesn't exist
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, productId, CancellationToken))
            .ReturnsAsync(false);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        // The exact error from Money.Create validation for negative amounts
        result.Error.Code.Should().Be("Order.NegativeAmount");

        // Verify the product was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_UpdatesProductWithoutAuditInfo()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductCommandV1
        {
            Id = productId,
            Name = "Updated Product",
            BasePrice = 25.99m,
            Slug = "updated-product"
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Original Product", "original-product", 19.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock slug doesn't exist
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, productId, CancellationToken))
            .ReturnsAsync(false);

        // Setup no authenticated user (uses default unauthenticated state)
        // No setup needed as TestFixture defaults to unauthenticated state

        // No need to mock UpdateAsync - the handler modifies the entity in-memory
        // and saves via UnitOfWork.SaveChangesAsync

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the product was still updated successfully (check the existing product instance)
        existingProduct.Name.Should().Be(command.Name);
        existingProduct.BasePrice.Amount.Should().Be(command.BasePrice);
    }
}
