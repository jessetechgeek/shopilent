using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetCategory.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Errors;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetCategoryQueryV1Tests : TestBase
{
    private readonly GetCategoryQueryHandlerV1 _handler;

    public GetCategoryQueryV1Tests()
    {
        _handler = new GetCategoryQueryHandlerV1(
            Fixture.MockCategoryReadRepository.Object,
            Fixture.GetLogger<GetCategoryQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithValidCategoryId_ReturnsSuccessfulResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var categoryDto = new CategoryDto
        {
            Id = categoryId,
            Name = "Test Category",
            Slug = "test-category",
            Description = "Test category description",
            ParentId = null,
            Level = 0,
            Path = "/test-category",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync(categoryDto);

        var query = new GetCategoryQueryV1 { Id = categoryId };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(categoryId);
        result.Value.Name.Should().Be(categoryDto.Name);
        result.Value.Slug.Should().Be(categoryDto.Slug);
    }

    [Fact]
    public async Task Handle_WithInvalidCategoryId_ReturnsNotFoundError()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync((CategoryDto)null);

        var query = new GetCategoryQueryV1 { Id = categoryId };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CategoryErrors.NotFound(categoryId).Code);
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ThrowsAsync(new Exception("Test exception"));

        var query = new GetCategoryQueryV1 { Id = categoryId };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Category.GetFailed");
        result.Error.Message.Should().Contain("Test exception");
    }
}
