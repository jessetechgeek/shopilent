using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetProductsDatatable.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Common.Models;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetProductsDatatableQueryV1Tests : TestBase
{
    private readonly GetProductsDatatableQueryHandlerV1 _handler;

    public GetProductsDatatableQueryV1Tests()
    {
        _handler = new GetProductsDatatableQueryHandlerV1(
            Fixture.MockUnitOfWork.Object,
            Fixture.GetLogger<GetProductsDatatableQueryHandlerV1>(),
            Fixture.MockS3StorageService.Object);

        // Setup S3 service to return presigned URLs
        Fixture.MockS3StorageService
            .Setup(s => s.GetPresignedUrlAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string bucket, string key, TimeSpan expiry, CancellationToken ct) =>
                Result.Success($"https://s3.example.com/{bucket}/{key}"));
    }

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsFormattedDatatableResult()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10,
            Search = new DataTableSearch { Value = "laptop" },
            Order = new List<DataTableOrder>
            {
                new DataTableOrder { Column = 0, Dir = "asc" }
            },
            Columns = new List<DataTableColumn>
            {
                new DataTableColumn { Data = "name", Name = "Name", Searchable = true, Orderable = true }
            }
        };

        var query = new GetProductsDatatableQueryV1
        {
            Request = request
        };

        var variants = new List<ProductVariantDto>
        {
            new ProductVariantDto { Id = Guid.NewGuid(), StockQuantity = 50 },
            new ProductVariantDto { Id = Guid.NewGuid(), StockQuantity = 30 }
        };

        var categories = new List<CategoryDto>
        {
            new CategoryDto { Id = Guid.NewGuid(), Name = "Electronics" },
            new CategoryDto { Id = Guid.NewGuid(), Name = "Computers" }
        };

        var productDetails = new List<ProductDetailDto>
        {
            new ProductDetailDto
            {
                Id = Guid.NewGuid(),
                Name = "Gaming Laptop",
                Slug = "gaming-laptop",
                Description = "High-performance gaming laptop",
                BasePrice = 1299.99m,
                Currency = "USD",
                Sku = "GL-001",
                IsActive = true,
                Variants = variants,
                Categories = categories,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new ProductDetailDto
            {
                Id = Guid.NewGuid(),
                Name = "Business Laptop",
                Slug = "business-laptop",
                Description = "Professional business laptop",
                BasePrice = 899.99m,
                Currency = "USD",
                Sku = "BL-001",
                IsActive = true,
                Variants = new List<ProductVariantDto>
                {
                    new ProductVariantDto { Id = Guid.NewGuid(), StockQuantity = 25 }
                },
                Categories = categories,
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                UpdatedAt = DateTime.UtcNow
            }
        };

        var datatableResult = new DataTableResult<ProductDetailDto>(
            draw: 1,
            recordsTotal: 2,
            recordsFiltered: 2,
            data: productDetails);

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetProductDetailDataTableAsync(request, CancellationToken))
            .ReturnsAsync(datatableResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Draw.Should().Be(1);
        result.Value.RecordsTotal.Should().Be(2);
        result.Value.RecordsFiltered.Should().Be(2);
        result.Value.Data.Count.Should().Be(2);

        var firstProduct = result.Value.Data.First();
        firstProduct.Name.Should().Be("Gaming Laptop");
        firstProduct.Slug.Should().Be("gaming-laptop");
        firstProduct.BasePrice.Should().Be(1299.99m);
        firstProduct.Sku.Should().Be("GL-001");
        firstProduct.VariantsCount.Should().Be(2);
        firstProduct.TotalStockQuantity.Should().Be(80);
        firstProduct.Categories.Count.Should().Be(2);
        firstProduct.Categories.Should().Contain("Electronics");
        firstProduct.Categories.Should().Contain("Computers");
    }

    [Fact]
    public async Task Handle_WithProductsWithoutVariantsOrCategories_HandlesNullCollections()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10
        };

        var query = new GetProductsDatatableQueryV1
        {
            Request = request
        };

        var productDetails = new List<ProductDetailDto>
        {
            new ProductDetailDto
            {
                Id = Guid.NewGuid(),
                Name = "Simple Product",
                Slug = "simple-product",
                BasePrice = 49.99m,
                Currency = "USD",
                Sku = "SP-001",
                IsActive = true,
                Variants = null, // Null variants
                Categories = null, // Null categories
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var datatableResult = new DataTableResult<ProductDetailDto>(
            draw: 1,
            recordsTotal: 1,
            recordsFiltered: 1,
            data: productDetails);

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetProductDetailDataTableAsync(request, CancellationToken))
            .ReturnsAsync(datatableResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var product = result.Value.Data.First();
        product.VariantsCount.Should().Be(0);
        product.TotalStockQuantity.Should().Be(0);
        product.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithEmptyResult_ReturnsEmptyDatatableResult()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10
        };

        var query = new GetProductsDatatableQueryV1
        {
            Request = request
        };

        var datatableResult = new DataTableResult<ProductDetailDto>(
            draw: 1,
            recordsTotal: 0,
            recordsFiltered: 0,
            data: new List<ProductDetailDto>());

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetProductDetailDataTableAsync(request, CancellationToken))
            .ReturnsAsync(datatableResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Draw.Should().Be(1);
        result.Value.RecordsTotal.Should().Be(0);
        result.Value.RecordsFiltered.Should().Be(0);
        result.Value.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10
        };

        var query = new GetProductsDatatableQueryV1
        {
            Request = request
        };

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetProductDetailDataTableAsync(request, CancellationToken))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Products.GetDataTableFailed");
        result.Error.Message.Should().Contain("Test exception");
    }

    [Fact]
    public async Task Handle_VerifiesCorrectMappingFromProductDetailToDto()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10
        };

        var query = new GetProductsDatatableQueryV1
        {
            Request = request
        };

        var sourceProduct = new ProductDetailDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Slug = "test-product",
            Description = "Test product description",
            BasePrice = 199.99m,
            Currency = "EUR",
            Sku = "TP-123",
            IsActive = false,
            Variants = new List<ProductVariantDto>
            {
                new ProductVariantDto { Id = Guid.NewGuid(), StockQuantity = 10 },
                new ProductVariantDto { Id = Guid.NewGuid(), StockQuantity = 15 },
                new ProductVariantDto { Id = Guid.NewGuid(), StockQuantity = 0 }
            },
            Categories = new List<CategoryDto>
            {
                new CategoryDto { Id = Guid.NewGuid(), Name = "Category 1" },
                new CategoryDto { Id = Guid.NewGuid(), Name = "Category 2" }
            },
            CreatedAt = DateTime.UtcNow.AddDays(-60),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };

        var datatableResult = new DataTableResult<ProductDetailDto>(
            draw: 1,
            recordsTotal: 1,
            recordsFiltered: 1,
            data: new List<ProductDetailDto> { sourceProduct });

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetProductDetailDataTableAsync(request, CancellationToken))
            .ReturnsAsync(datatableResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var mappedProduct = result.Value.Data.First();
        mappedProduct.Id.Should().Be(sourceProduct.Id);
        mappedProduct.Name.Should().Be(sourceProduct.Name);
        mappedProduct.Slug.Should().Be(sourceProduct.Slug);
        mappedProduct.Description.Should().Be(sourceProduct.Description);
        mappedProduct.BasePrice.Should().Be(sourceProduct.BasePrice);
        mappedProduct.Currency.Should().Be(sourceProduct.Currency);
        mappedProduct.Sku.Should().Be(sourceProduct.Sku);
        mappedProduct.IsActive.Should().Be(sourceProduct.IsActive);
        mappedProduct.VariantsCount.Should().Be(3);
        mappedProduct.TotalStockQuantity.Should().Be(25); // 10 + 15 + 0
        mappedProduct.Categories.Count.Should().Be(2);
        mappedProduct.Categories.Should().Contain("Category 1");
        mappedProduct.Categories.Should().Contain("Category 2");
        mappedProduct.CreatedAt.Should().Be(sourceProduct.CreatedAt);
        mappedProduct.UpdatedAt.Should().Be(sourceProduct.UpdatedAt);
    }
}