using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Abstractions.Imaging;
using Shopilent.Application.Features.Catalog.Commands.CreateProduct.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class CreateProductCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public CreateProductCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockCategoryWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockAttributeWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.MockImageService.Object);
        services.AddTransient(sp => Fixture.MockS3StorageService.Object);
        services.AddTransient(sp => Fixture.GetLogger<CreateProductCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateProductCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<CreateProductCommandV1>, CreateProductCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task CreateProduct_WithValidData_ReturnsSuccessfulResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var command = new CreateProductCommandV1
        {
            Name = "Test Product",
            Slug = "test-product",
            Description = "Test product description",
            BasePrice = 99.99m,
            Currency = "USD",
            Sku = "TEST-001",
            CategoryIds = new List<Guid> { categoryId },
            Attributes =
                new List<ProductAttributeDto>
                {
                    new ProductAttributeDto { AttributeId = Guid.NewGuid(), Value = "Test Value" }
                },
            Images = new List<ProductImageDto> { new ProductImageDto { AltText = "Test image", DisplayOrder = 1 } }
        };

        var category = new CategoryBuilder().WithId(categoryId).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId, isAdmin: true);

        // Mock repository calls
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, null, CancellationToken))
            .ReturnsAsync(false);

        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync(category);

        // Mock image and storage services
        Fixture.MockImageService
            .Setup(service => service.ProcessProductImage(It.IsAny<Stream>()))
            .ReturnsAsync(new ImageResult { MainImage = new MemoryStream(), Thumbnail = new MemoryStream() });

        Fixture.MockS3StorageService
            .Setup(service => service.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("products/test.jpg"));

        // Capture product being added
        Product capturedProduct = null;
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Product>(), CancellationToken))
            .Callback<Product, CancellationToken>((p, _) => capturedProduct = p)
            .ReturnsAsync((Product p, CancellationToken _) => p);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(command.Name);
        result.Value.Slug.Should().Be(command.Slug);
        result.Value.Description.Should().Be(command.Description);
        result.Value.BasePrice.Should().Be(command.BasePrice);
        result.Value.Sku.Should().Be(command.Sku);

        // Verify product was created and saved
        capturedProduct.Should().NotBeNull();
        capturedProduct.Name.Should().Be(command.Name);
        capturedProduct.Slug.Value.Should().Be(command.Slug);

        Fixture.MockProductWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Product>(), CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateSlug_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new CreateProductCommandV1
        {
            Name = "Test Product", Slug = "existing-product-slug", BasePrice = 99.99m, Currency = "USD"
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId, isAdmin: true);

        // Mock that slug already exists
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, null, CancellationToken))
            .ReturnsAsync(true);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ProductErrors.DuplicateSlug(command.Slug).Code);

        // Verify product was not saved
        Fixture.MockProductWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Product>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task CreateProduct_WithInvalidCategory_SkipsInvalidCategoryAndSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invalidCategoryId = Guid.NewGuid();

        var command = new CreateProductCommandV1
        {
            Name = "Test Product",
            Slug = "test-product",
            BasePrice = 99.99m,
            Currency = "USD",
            CategoryIds = new List<Guid> { invalidCategoryId }
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId, isAdmin: true);

        // Mock repository calls
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, null, CancellationToken))
            .ReturnsAsync(false);

        // Category not found
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(invalidCategoryId, CancellationToken))
            .ReturnsAsync((Category)null);

        // Capture product being added
        Product capturedProduct = null;
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Product>(), CancellationToken))
            .Callback<Product, CancellationToken>((p, _) => capturedProduct = p)
            .ReturnsAsync((Product p, CancellationToken _) => p);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // The business logic silently skips invalid categories rather than failing
        capturedProduct.Should().NotBeNull();
        capturedProduct.Categories.Should().BeEmpty(); // No categories should be added since the category was invalid

        // Verify product was saved
        Fixture.MockProductWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Product>(), CancellationToken),
            Times.Once);
    }


    [Fact]
    public async Task CreateProduct_WithImages_ProcessesAndUploadsImages()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new CreateProductCommandV1
        {
            Name = "Test Product",
            Slug = "test-product",
            BasePrice = 99.99m,
            Currency = "USD",
            Images = new List<ProductImageDto>
            {
                new ProductImageDto { AltText = "First image", DisplayOrder = 1 },
                new ProductImageDto { AltText = "Second image", DisplayOrder = 2 }
            }
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId, isAdmin: true);

        // Mock repository calls
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, null, CancellationToken))
            .ReturnsAsync(false);

        // Mock image processing
        Fixture.MockImageService
            .Setup(service => service.ProcessProductImage(It.IsAny<Stream>()))
            .ReturnsAsync(new ImageResult { MainImage = new MemoryStream(), Thumbnail = new MemoryStream() });

        // Mock S3 storage
        Fixture.MockS3StorageService
            .Setup(service => service.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("products/test-key"));

        // Capture product being added
        Product capturedProduct = null;
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Product>(), CancellationToken))
            .Callback<Product, CancellationToken>((p, _) => capturedProduct = p)
            .ReturnsAsync((Product p, CancellationToken _) => p);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Images.Count.Should().Be(2);

        // Verify image processing was called for each image
        Fixture.MockImageService.Verify(
            service => service.ProcessProductImage(It.IsAny<Stream>()),
            Times.Exactly(2));

        // Verify upload was called for each image (main + thumbnail for each)
        Fixture.MockS3StorageService.Verify(
            service => service.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4)); // 2 images x 2 uploads each
    }

    [Fact]
    public async Task CreateProduct_WithAttributes_AddsAttributesToProduct()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var attributeId1 = Guid.NewGuid();
        var attributeId2 = Guid.NewGuid();

        var command = new CreateProductCommandV1
        {
            Name = "Test Product",
            Slug = "test-product",
            BasePrice = 99.99m,
            Currency = "USD",
            Attributes = new List<ProductAttributeDto>
            {
                new ProductAttributeDto { AttributeId = attributeId1, Value = "Blue" },
                new ProductAttributeDto { AttributeId = attributeId2, Value = "Large" }
            }
        };

        var attribute1 = new AttributeBuilder().WithId(attributeId1).WithName("Color").Build();
        var attribute2 = new AttributeBuilder().WithId(attributeId2).WithName("Size").Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId, isAdmin: true);

        // Mock repository calls
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, null, CancellationToken))
            .ReturnsAsync(false);

        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId1, CancellationToken))
            .ReturnsAsync(attribute1);

        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId2, CancellationToken))
            .ReturnsAsync(attribute2);

        // Capture product being added
        Product capturedProduct = null;
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Product>(), CancellationToken))
            .Callback<Product, CancellationToken>((p, _) => capturedProduct = p)
            .ReturnsAsync((Product p, CancellationToken _) => p);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Attributes.Count.Should().Be(2);

        var colorAttribute = result.Value.Attributes.FirstOrDefault(a => a.AttributeId == attributeId1);
        colorAttribute.Should().NotBeNull();
        colorAttribute.Values.Should().BeOfType<Dictionary<string, object>>();
        var colorValues = (Dictionary<string, object>)colorAttribute.Values;
        colorValues.Should().ContainKey("value");
        colorValues["value"]?.ToString().Should().Be("Blue");

        var sizeAttribute = result.Value.Attributes.FirstOrDefault(a => a.AttributeId == attributeId2);
        sizeAttribute.Should().NotBeNull();
        sizeAttribute.Values.Should().BeOfType<Dictionary<string, object>>();
        var sizeValues = (Dictionary<string, object>)sizeAttribute.Values;
        sizeValues.Should().ContainKey("value");
        sizeValues["value"]?.ToString().Should().Be("Large");
    }
}
