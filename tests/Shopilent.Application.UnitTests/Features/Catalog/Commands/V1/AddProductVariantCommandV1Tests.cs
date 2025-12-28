using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.AddProductVariant.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Enums;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;
using DomainAttribute = Shopilent.Domain.Catalog.Attribute;
using CommandProductAttributeDto =
    Shopilent.Application.Features.Catalog.Commands.AddProductVariant.V1.ProductAttributeDto;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class AddProductVariantCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Helper method to create a product with a specific ID for testing
    /// </summary>
    private static Product CreateProductWithId(Guid id, string name, string slug, decimal price)
    {
        var slugValue = Slug.Create(slug).Value;
        var moneyValue = Money.Create(price, "USD").Value;
        var product = Product.Create(name, slugValue, moneyValue).Value;
        var idProperty = typeof(Product).GetProperty("Id");
        idProperty?.SetValue(product, id);
        return product;
    }

    /// <summary>
    /// Helper method to create an attribute with a specific ID for testing
    /// </summary>
    private static DomainAttribute CreateAttributeWithId(Guid id, string name, string displayName, AttributeType type,
        bool isVariant = true)
    {
        var attribute = DomainAttribute.Create(name, displayName, type).Value;
        var idProperty = typeof(DomainAttribute).GetProperty("Id");
        idProperty?.SetValue(attribute, id);
        if (isVariant)
        {
            attribute.SetIsVariant(true);
        }

        return attribute;
    }

    public AddProductVariantCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockProductVariantWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockAttributeWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockAttributeReadRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.MockS3StorageService.Object);
        services.AddTransient(sp => Fixture.MockImageService.Object);
        services.AddTransient(sp => Fixture.GetLogger<AddProductVariantCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<AddProductVariantCommandV1>();
        });

        // Register validator
        services
            .AddTransient<FluentValidation.IValidator<AddProductVariantCommandV1>,
                AddProductVariantCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var attributeId = Guid.NewGuid();

        var command = new AddProductVariantCommandV1
        {
            ProductId = productId,
            Sku = "VARIANT-001",
            Price = 19.99m,
            StockQuantity = 100,
            IsActive = true,
            Attributes = new List<CommandProductAttributeDto>
            {
                new CommandProductAttributeDto { AttributeId = attributeId, Value = "Red" }
            },
            Metadata = new Dictionary<string, object> { { "key1", "value1" } }
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Create existing attribute
        var existingAttribute = CreateAttributeWithId(attributeId, "color", "Color", AttributeType.Text, true);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock SKU doesn't exist
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.SkuExistsAsync(command.Sku, null, CancellationToken))
            .ReturnsAsync(false);

        // Mock attribute retrieval (called twice - once for validation, once for adding)
        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(new AttributeDto { Id = attributeId, Name = "color", IsVariant = true });

        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(existingAttribute);

        // Setup authenticated user for audit info
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Capture the variant being added
        ProductVariant capturedVariant = null;
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<ProductVariant>(), CancellationToken))
            .Callback<ProductVariant, CancellationToken>((v, _) => capturedVariant = v)
            .ReturnsAsync((ProductVariant v, CancellationToken _) => v);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the variant was created correctly
        capturedVariant.Should().NotBeNull();
        capturedVariant.ProductId.Should().Be(command.ProductId);
        capturedVariant.Sku.Should().Be(command.Sku);
        capturedVariant.Price?.Amount.Should().Be(command.Price);
        capturedVariant.StockQuantity.Should().Be(command.StockQuantity);
        capturedVariant.IsActive.Should().Be(command.IsActive);

        // Verify response
        var response = result.Value;
        response.Id.Should().Be(capturedVariant.Id);
        response.ProductId.Should().Be(command.ProductId);
        response.Sku.Should().Be(command.Sku);
        response.Price.Should().Be(command.Price);
        response.StockQuantity.Should().Be(command.StockQuantity);
        response.IsActive.Should().Be(command.IsActive);
        response.Attributes.Should().ContainSingle();

        // Verify the variant was saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentProduct_ReturnsNotFound()
    {
        // Arrange
        var nonExistentProductId = Guid.NewGuid();
        var command = new AddProductVariantCommandV1
        {
            ProductId = nonExistentProductId, Sku = "VARIANT-002", StockQuantity = 50
        };

        // Mock that product doesn't exist
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(nonExistentProductId, CancellationToken))
            .ReturnsAsync((Product)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ProductErrors.NotFound(nonExistentProductId).Code);

        // Verify the variant was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateSku_ReturnsConflict()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new AddProductVariantCommandV1
        {
            ProductId = productId, Sku = "EXISTING-SKU", StockQuantity = 50
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock SKU already exists
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.SkuExistsAsync(command.Sku, null, CancellationToken))
            .ReturnsAsync(true);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ProductVariantErrors.DuplicateSku(command.Sku).Code);

        // Verify the variant was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NonExistentAttribute_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var nonExistentAttributeId = Guid.NewGuid();

        var command = new AddProductVariantCommandV1
        {
            ProductId = productId,
            Sku = "VARIANT-003",
            StockQuantity = 25,
            Attributes = new List<CommandProductAttributeDto>
            {
                new CommandProductAttributeDto { AttributeId = nonExistentAttributeId, Value = "Blue" }
            }
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock SKU doesn't exist
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.SkuExistsAsync(command.Sku, null, CancellationToken))
            .ReturnsAsync(false);

        // Mock attribute doesn't exist
        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.GetByIdAsync(nonExistentAttributeId, CancellationToken))
            .ReturnsAsync((AttributeDto)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(AttributeErrors.NotFound(nonExistentAttributeId).Code);

        // Verify the variant was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NonVariantAttribute_ReturnsValidationError()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var attributeId = Guid.NewGuid();

        var command = new AddProductVariantCommandV1
        {
            ProductId = productId,
            Sku = "VARIANT-004",
            StockQuantity = 25,
            Attributes = new List<CommandProductAttributeDto>
            {
                new CommandProductAttributeDto { AttributeId = attributeId, Value = "Large" }
            }
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock SKU doesn't exist
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.SkuExistsAsync(command.Sku, null, CancellationToken))
            .ReturnsAsync(false);

        // Mock attribute exists but is not a variant attribute
        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(new AttributeDto { Id = attributeId, Name = "description", IsVariant = false });

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(AttributeErrors.NotVariantAttribute("description").Code);

        // Verify the variant was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutPrice_InheritsProductBasePrice()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new AddProductVariantCommandV1
        {
            ProductId = productId,
            Sku = "VARIANT-005",
            Price = null, // No price specified
            StockQuantity = 75
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock SKU doesn't exist
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.SkuExistsAsync(command.Sku, null, CancellationToken))
            .ReturnsAsync(false);

        // Capture the variant being added
        ProductVariant capturedVariant = null;
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<ProductVariant>(), CancellationToken))
            .Callback<ProductVariant, CancellationToken>((v, _) => capturedVariant = v)
            .ReturnsAsync((ProductVariant v, CancellationToken _) => v);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the variant was created with inherited price from product
        capturedVariant.Should().NotBeNull();
        capturedVariant.Price.Should().NotBeNull();
        capturedVariant.Price.Amount.Should().Be(29.99m); // Inherited from product base price

        // Verify response has inherited price
        var response = result.Value;
        response.Price.Should().Be(29.99m);
    }

    [Fact]
    public async Task Handle_InactiveVariant_CreatesInactiveVariant()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new AddProductVariantCommandV1
        {
            ProductId = productId, Sku = "VARIANT-006", StockQuantity = 10, IsActive = false // Inactive variant
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock SKU doesn't exist
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.SkuExistsAsync(command.Sku, null, CancellationToken))
            .ReturnsAsync(false);

        // Capture the variant being added
        ProductVariant capturedVariant = null;
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<ProductVariant>(), CancellationToken))
            .Callback<ProductVariant, CancellationToken>((v, _) => capturedVariant = v)
            .ReturnsAsync((ProductVariant v, CancellationToken _) => v);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the variant was created as inactive
        capturedVariant.Should().NotBeNull();
        capturedVariant.IsActive.Should().BeFalse();

        // Verify response shows inactive status
        var response = result.Value;
        response.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_CreatesVariantWithoutAuditInfo()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new AddProductVariantCommandV1 { ProductId = productId, Sku = "VARIANT-007", StockQuantity = 30 };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock SKU doesn't exist
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.SkuExistsAsync(command.Sku, null, CancellationToken))
            .ReturnsAsync(false);

        // Setup no authenticated user (uses default unauthenticated state)
        // No setup needed as TestFixture defaults to unauthenticated state

        // Capture the variant being added
        ProductVariant capturedVariant = null;
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<ProductVariant>(), CancellationToken))
            .Callback<ProductVariant, CancellationToken>((v, _) => capturedVariant = v)
            .ReturnsAsync((ProductVariant v, CancellationToken _) => v);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the variant was still created successfully
        capturedVariant.Should().NotBeNull();
        capturedVariant.ProductId.Should().Be(command.ProductId);
        capturedVariant.Sku.Should().Be(command.Sku);
    }
}
