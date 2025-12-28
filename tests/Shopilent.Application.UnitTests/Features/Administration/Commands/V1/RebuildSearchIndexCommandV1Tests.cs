using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Features.Administration.Commands.RebuildSearchIndex.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.UnitTests.Features.Administration.Commands.V1;

public class RebuildSearchIndexCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public RebuildSearchIndexCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockProductReadRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.MockSearchService.Object);
        services.AddTransient(sp => Fixture.GetLogger<RebuildSearchIndexCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RebuildSearchIndexCommandV1>();
        });

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequestWithInitializeAndIndex_ReturnsSuccessfulResult()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new RebuildSearchIndexCommandV1
        {
            InitializeIndexes = true, IndexProducts = true, ForceReindex = false
        };

        var productDtos = new List<ProductDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Product 1", Slug = "product-1" },
            new() { Id = Guid.NewGuid(), Name = "Product 2", Slug = "product-2" }
        };

        // Create detail DTOs for both products
        var productDetailDtos = productDtos.Select(p => new ProductDetailDto
        {
            Id = p.Id, Name = p.Name, Slug = p.Slug, Description = $"Test {p.Name}"
        }).ToDictionary(p => p.Id);

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(adminUserId, isAdmin: true);

        // Mock search service
        Fixture.MockSearchService
            .Setup(service => service.InitializeIndexesAsync(CancellationToken))
            .Returns(Task.CompletedTask);

        Fixture.MockSearchService
            .Setup(service =>
                service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken))
            .ReturnsAsync(Result.Success());

        Fixture.MockSearchService
            .Setup(service => service.GetAllProductIdsAsync(CancellationToken))
            .ReturnsAsync(Result.Success<IEnumerable<Guid>>(productDtos.Select(p => p.Id)));

        // Mock product repository
        Fixture.MockProductReadRepository.Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(productDtos);

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(It.IsAny<Guid>(), CancellationToken))
            .ReturnsAsync((Guid id, CancellationToken ct) => productDetailDtos.GetValueOrDefault(id));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuccess.Should().BeTrue();
        result.Value.IndexesInitialized.Should().BeTrue();
        result.Value.ProductsIndexed.Should().Be(2);
        result.Value.ProductsDeleted.Should().Be(0);
        result.Value.Message.Should().Contain("indexes initialized");
        result.Value.Message.Should().Contain("products indexed");

        // Verify service calls
        Fixture.MockSearchService.Verify(
            service => service.InitializeIndexesAsync(CancellationToken),
            Times.Once);

        Fixture.MockSearchService.Verify(
            service => service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_InitializeOnlyRequest_ReturnsSuccessfulResult()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new RebuildSearchIndexCommandV1
        {
            InitializeIndexes = true, IndexProducts = false, ForceReindex = false
        };

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(adminUserId, isAdmin: true);

        // Mock search service
        Fixture.MockSearchService
            .Setup(service => service.InitializeIndexesAsync(CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuccess.Should().BeTrue();
        result.Value.IndexesInitialized.Should().BeTrue();
        result.Value.ProductsIndexed.Should().Be(0);
        result.Value.Message.Should().Contain("indexes initialized");
        result.Value.Message.Should().NotContain("products indexed");

        // Verify service calls
        Fixture.MockSearchService.Verify(
            service => service.InitializeIndexesAsync(CancellationToken),
            Times.Once);

        Fixture.MockSearchService.Verify(
            service => service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_IndexOnlyRequest_ReturnsSuccessfulResult()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new RebuildSearchIndexCommandV1
        {
            InitializeIndexes = false, IndexProducts = true, ForceReindex = true
        };

        var productDtos = new List<ProductDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Product 1", Slug = "product-1" }
        };

        var productDetailDto = new ProductDetailDto
        {
            Id = productDtos[0].Id,
            Name = productDtos[0].Name,
            Slug = productDtos[0].Slug,
            Description = "Test Product 1"
        };

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(adminUserId, isAdmin: true);

        // Mock search service
        Fixture.MockSearchService
            .Setup(service =>
                service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken))
            .ReturnsAsync(Result.Success());

        Fixture.MockSearchService
            .Setup(service => service.GetAllProductIdsAsync(CancellationToken))
            .ReturnsAsync(Result.Success<IEnumerable<Guid>>(productDtos.Select(p => p.Id)));

        // Mock product repository
        Fixture.MockProductReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(productDtos);

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(It.IsAny<Guid>(), CancellationToken))
            .ReturnsAsync(productDetailDto);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuccess.Should().BeTrue();
        result.Value.IndexesInitialized.Should().BeFalse();
        result.Value.ProductsIndexed.Should().Be(1);
        result.Value.ProductsDeleted.Should().Be(0);
        result.Value.Message.Should().NotContain("indexes initialized");
        result.Value.Message.Should().Contain("products indexed");

        // Verify service calls
        Fixture.MockSearchService.Verify(
            service => service.InitializeIndexesAsync(CancellationToken),
            Times.Never);

        Fixture.MockSearchService.Verify(
            service => service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NoProductsToIndex_ReturnsSuccessfulResult()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new RebuildSearchIndexCommandV1
        {
            InitializeIndexes = false, IndexProducts = true, ForceReindex = false
        };

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(adminUserId, isAdmin: true);

        // Mock empty product list
        Fixture.MockProductReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(new List<ProductDto>());

        // Mock GetAllProductIdsAsync to return empty list (for orphan cleanup)
        Fixture.MockSearchService
            .Setup(service => service.GetAllProductIdsAsync(CancellationToken))
            .ReturnsAsync(Result.Success<IEnumerable<Guid>>(Enumerable.Empty<Guid>()));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuccess.Should().BeTrue();
        result.Value.IndexesInitialized.Should().BeFalse();
        result.Value.ProductsIndexed.Should().Be(0);
        result.Value.ProductsDeleted.Should().Be(0);
        result.Value.Message.Should().Contain("0 products indexed");

        // Verify service calls
        Fixture.MockSearchService.Verify(
            service => service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken),
            Times.Never);

        // Verify orphan cleanup was attempted even with no products
        Fixture.MockSearchService.Verify(
            service => service.GetAllProductIdsAsync(CancellationToken),
            Times.Once);

        // Verify no deletion was needed since there were no orphans
        Fixture.MockSearchService.Verify(
            service => service.DeleteProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SearchServiceInitializationFails_ReturnsFailureResult()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new RebuildSearchIndexCommandV1
        {
            InitializeIndexes = true, IndexProducts = false, ForceReindex = false
        };

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(adminUserId, isAdmin: true);

        // Mock search service to throw exception
        Fixture.MockSearchService
            .Setup(service => service.InitializeIndexesAsync(CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Search service unavailable"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("Search service unavailable");

        // Verify service calls
        Fixture.MockSearchService.Verify(
            service => service.InitializeIndexesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ProductIndexingFails_ReturnsFailureResult()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new RebuildSearchIndexCommandV1
        {
            InitializeIndexes = true, IndexProducts = true, ForceReindex = false
        };

        var productDtos = new List<ProductDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Product 1", Slug = "product-1" }
        };

        var productDetailDto = new ProductDetailDto
        {
            Id = productDtos[0].Id,
            Name = productDtos[0].Name,
            Slug = productDtos[0].Slug,
            Description = "Test Product 1"
        };

        var indexingError = Domain.Common.Errors.Error.Failure("Search.IndexingFailed", "Failed to index products");

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(adminUserId, isAdmin: true);

        // Mock search service
        Fixture.MockSearchService
            .Setup(service => service.InitializeIndexesAsync(CancellationToken))
            .Returns(Task.CompletedTask);

        Fixture.MockSearchService
            .Setup(service =>
                service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken))
            .ReturnsAsync(Result.Failure(indexingError));

        // Mock product repository
        Fixture.MockProductReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(productDtos);

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(It.IsAny<Guid>(), CancellationToken))
            .ReturnsAsync(productDetailDto);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("Failed to index products");

        // Verify service calls
        Fixture.MockSearchService.Verify(
            service => service.InitializeIndexesAsync(CancellationToken),
            Times.Once);

        Fixture.MockSearchService.Verify(
            service => service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ProductRepositoryFails_ReturnsFailureResult()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new RebuildSearchIndexCommandV1
        {
            InitializeIndexes = false, IndexProducts = true, ForceReindex = false
        };

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(adminUserId, isAdmin: true);

        // Mock product repository to throw exception
        Fixture.MockProductReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("Database connection failed");

        // Verify service calls
        Fixture.MockProductReadRepository
            .Verify(repo => repo.ListAllAsync(CancellationToken),
                Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_SetsCorrectResponseFields()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new RebuildSearchIndexCommandV1
        {
            InitializeIndexes = true, IndexProducts = true, ForceReindex = false
        };

        var productDtos = new List<ProductDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Product 1", Slug = "product-1" }
        };

        var productDetailDto = new ProductDetailDto
        {
            Id = productDtos[0].Id,
            Name = productDtos[0].Name,
            Slug = productDtos[0].Slug,
            Description = "Test Product 1"
        };

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(adminUserId, isAdmin: true);

        // Mock search service
        Fixture.MockSearchService
            .Setup(service => service.InitializeIndexesAsync(CancellationToken))
            .Returns(Task.CompletedTask);

        Fixture.MockSearchService
            .Setup(service =>
                service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken))
            .ReturnsAsync(Result.Success());

        Fixture.MockSearchService
            .Setup(service => service.GetAllProductIdsAsync(CancellationToken))
            .ReturnsAsync(Result.Success<IEnumerable<Guid>>(productDtos.Select(p => p.Id)));

        // Mock product repository
        Fixture.MockProductReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(productDtos);

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(It.IsAny<Guid>(), CancellationToken))
            .ReturnsAsync(productDetailDto);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuccess.Should().BeTrue();
        result.Value.IndexesInitialized.Should().BeTrue();
        result.Value.ProductsIndexed.Should().Be(1);
        result.Value.CompletedAt.Should().NotBe(DateTime.MinValue);
        result.Value.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        result.Value.Message.Should().NotBeNull();
        result.Value.Message.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_OrphanedProductsInIndex_DeletesOrphanedProducts()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new RebuildSearchIndexCommandV1
        {
            InitializeIndexes = false, IndexProducts = true, ForceReindex = false
        };

        var productDtos = new List<ProductDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Product 1", Slug = "product-1" },
            new() { Id = Guid.NewGuid(), Name = "Product 2", Slug = "product-2" }
        };

        // Create detail DTOs for both products
        var productDetailDtos = productDtos.Select(p => new ProductDetailDto
        {
            Id = p.Id, Name = p.Name, Slug = p.Slug, Description = $"Test {p.Name}"
        }).ToDictionary(p => p.Id);

        // IDs in Meilisearch include orphaned products
        var orphanedId1 = Guid.NewGuid();
        var orphanedId2 = Guid.NewGuid();
        var meilisearchIds = productDtos.Select(p => p.Id).Concat(new[] { orphanedId1, orphanedId2 });

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(adminUserId, isAdmin: true);

        // Mock search service
        Fixture.MockSearchService
            .Setup(service =>
                service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken))
            .ReturnsAsync(Result.Success());

        Fixture.MockSearchService
            .Setup(service => service.GetAllProductIdsAsync(CancellationToken))
            .ReturnsAsync(Result.Success<IEnumerable<Guid>>(meilisearchIds));

        Fixture.MockSearchService
            .Setup(service => service.DeleteProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), CancellationToken))
            .ReturnsAsync(Result.Success());

        // Mock product repository
        Fixture.MockProductReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(productDtos);

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(It.IsAny<Guid>(), CancellationToken))
            .ReturnsAsync((Guid id, CancellationToken ct) => productDetailDtos.GetValueOrDefault(id));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuccess.Should().BeTrue();
        result.Value.ProductsIndexed.Should().Be(2);
        result.Value.ProductsDeleted.Should().Be(2);
        result.Value.Message.Should().Contain("products indexed");
        result.Value.Message.Should().Contain("orphaned products deleted");

        // Verify service calls
        Fixture.MockSearchService.Verify(
            service => service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken),
            Times.Once);

        Fixture.MockSearchService.Verify(
            service => service.GetAllProductIdsAsync(CancellationToken),
            Times.Once);

        Fixture.MockSearchService.Verify(
            service => service.DeleteProductsByIdsAsync(
                It.Is<IEnumerable<Guid>>(ids =>
                    ids.Count() == 2 && ids.Contains(orphanedId1) && ids.Contains(orphanedId2)),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_GetAllProductIdsFails_ContinuesWithoutDeletion()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new RebuildSearchIndexCommandV1
        {
            InitializeIndexes = false, IndexProducts = true, ForceReindex = false
        };

        var productDtos = new List<ProductDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Product 1", Slug = "product-1" }
        };

        var productDetailDto = new ProductDetailDto
        {
            Id = productDtos[0].Id,
            Name = productDtos[0].Name,
            Slug = productDtos[0].Slug,
            Description = "Test Product 1"
        };

        var getIdsError = Domain.Common.Errors.Error.Failure("Search.GetIdsFailed", "Failed to fetch IDs");

        // Setup authenticated admin user
        Fixture.SetAuthenticatedUser(adminUserId, isAdmin: true);

        // Mock search service
        Fixture.MockSearchService
            .Setup(service =>
                service.IndexProductsAsync(It.IsAny<IEnumerable<ProductSearchDocument>>(), CancellationToken))
            .ReturnsAsync(Result.Success());

        Fixture.MockSearchService
            .Setup(service => service.GetAllProductIdsAsync(CancellationToken))
            .ReturnsAsync(Result.Failure<IEnumerable<Guid>>(getIdsError));

        // Mock product repository
        Fixture.MockProductReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(productDtos);

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(It.IsAny<Guid>(), CancellationToken))
            .ReturnsAsync(productDetailDto);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert - should still succeed even though cleanup failed
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuccess.Should().BeTrue();
        result.Value.ProductsIndexed.Should().Be(1);
        result.Value.ProductsDeleted.Should().Be(0);

        // Verify DeleteProductsByIdsAsync was never called
        Fixture.MockSearchService.Verify(
            service => service.DeleteProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), CancellationToken),
            Times.Never);
    }
}
