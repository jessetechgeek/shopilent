using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetAllAttributes.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Enums;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetAllAttributesQueryV1Tests : TestBase
{
    private readonly GetAllAttributesQueryHandlerV1 _handler;

    public GetAllAttributesQueryV1Tests()
    {
        _handler = new GetAllAttributesQueryHandlerV1(
            Fixture.MockAttributeReadRepository.Object,
            Fixture.GetLogger<GetAllAttributesQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithExistingAttributes_ReturnsAllAttributes()
    {
        // Arrange
        var query = new GetAllAttributesQueryV1();

        var attributes = new List<AttributeDto>
        {
            new AttributeDto
            {
                Id = Guid.NewGuid(),
                Name = "Color",
                DisplayName = "Product Color",
                Type = AttributeType.Text,
                Filterable = true,
                Searchable = true,
                IsVariant = false,
                Configuration = new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AttributeDto
            {
                Id = Guid.NewGuid(),
                Name = "Size",
                DisplayName = "Product Size",
                Type = AttributeType.Select,
                Filterable = true,
                Searchable = true,
                IsVariant = true,
                Configuration = new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AttributeDto
            {
                Id = Guid.NewGuid(),
                Name = "Weight",
                DisplayName = "Product Weight",
                Type = AttributeType.Number,
                Filterable = false,
                Searchable = false,
                IsVariant = false,
                Configuration = new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(attributes);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(3);
        result.Value.Should().Contain(a => a.Name == "Color");
        result.Value.Should().Contain(a => a.Name == "Size");
        result.Value.Should().Contain(a => a.Name == "Weight");
    }

    [Fact]
    public async Task Handle_WithNoAttributes_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetAllAttributesQueryV1();

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(new List<AttributeDto>());

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var query = new GetAllAttributesQueryV1();

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Attributes.GetAllFailed");
        result.Error.Message.Should().Contain("Test exception");
    }

    [Fact]
    public async Task Handle_VerifiesCacheKeyAndExpirationAreSet()
    {
        // Arrange
        var query = new GetAllAttributesQueryV1();

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(new List<AttributeDto>());

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        query.CacheKey.Should().Be("all-attributes");
        query.Expiration.Should().NotBeNull();
        query.Expiration.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Handle_VerifiesNoFilteringIsApplied()
    {
        // Arrange
        var query = new GetAllAttributesQueryV1();

        var allAttributes = new List<AttributeDto>
        {
            new AttributeDto
            {
                Id = Guid.NewGuid(),
                Name = "Active Attribute",
                DisplayName = "Active Attribute",
                Type = AttributeType.Text,
                Filterable = true,
                Searchable = true,
                IsVariant = false,
                Configuration = new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AttributeDto
            {
                Id = Guid.NewGuid(),
                Name = "Inactive Attribute",
                DisplayName = "Inactive Attribute",
                Type = AttributeType.Text,
                Filterable = false,
                Searchable = false,
                IsVariant = false,
                Configuration = new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(allAttributes);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Should().Contain(a => a.Name == "Active Attribute");
        result.Value.Should().Contain(a => a.Name == "Inactive Attribute");

        Fixture.MockAttributeReadRepository.Verify(
            repo => repo.ListAllAsync(CancellationToken),
            Times.Once);
    }
}
