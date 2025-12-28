using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetAttribute.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Enums;
using Shopilent.Domain.Catalog.Errors;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetAttributeQueryV1Tests : TestBase
{
    private readonly GetAttributeQueryHandlerV1 _handler;

    public GetAttributeQueryV1Tests()
    {
        _handler = new GetAttributeQueryHandlerV1(
            Fixture.MockAttributeReadRepository.Object,
            Fixture.GetLogger<GetAttributeQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithValidAttributeId_ReturnsSuccessfulResult()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var attributeDto = new AttributeDto
        {
            Id = attributeId,
            Name = "Color",
            DisplayName = "Product Color",
            Type = AttributeType.Text,
            Filterable = true,
            Searchable = true,
            IsVariant = false,
            Configuration = new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(attributeDto);

        var query = new GetAttributeQueryV1 { Id = attributeId };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(attributeId);
        result.Value.Name.Should().Be(attributeDto.Name);
        result.Value.DisplayName.Should().Be(attributeDto.DisplayName);
        result.Value.Type.Should().Be(attributeDto.Type);
    }

    [Fact]
    public async Task Handle_WithInvalidAttributeId_ReturnsNotFoundError()
    {
        // Arrange
        var attributeId = Guid.NewGuid();

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync((AttributeDto)null);

        var query = new GetAttributeQueryV1 { Id = attributeId };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(AttributeErrors.NotFound(attributeId).Code);
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var attributeId = Guid.NewGuid();

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ThrowsAsync(new Exception("Test exception"));

        var query = new GetAttributeQueryV1 { Id = attributeId };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Attribute.GetFailed");
        result.Error.Message.Should().Contain("Test exception");
    }

    [Fact]
    public async Task Handle_VerifiesCacheKeyAndExpirationAreSet()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var query = new GetAttributeQueryV1 { Id = attributeId };

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(new AttributeDto { Id = attributeId, Name = "Test" });

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        query.CacheKey.Should().Be($"attribute-{attributeId}");
        query.Expiration.Should().NotBeNull();
        query.Expiration.Should().Be(TimeSpan.FromMinutes(30));
    }
}
