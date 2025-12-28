using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.CreateCategory.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class CreateCategoryCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public CreateCategoryCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockCategoryWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        // services.AddTransient(sp => Fixture.GetLogger<CreateCategoryCommandV1>());
        services.AddTransient(sp => Fixture.GetLogger<CreateCategoryCommandHandlerV1>());


        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<CreateCategoryCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<CreateCategoryCommandV1>, CreateCategoryCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task CreateCategory_WithValidData_ReturnsSuccessfulResult()
    {
        // Arrange
        var command = new CreateCategoryCommandV1
        {
            Name = "Test Category",
            Slug = "test-category",
            Description = "Test category description"
        };

        // Mock that slug doesn't exist
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, null, CancellationToken))
            .ReturnsAsync(false);

        // Setup authenticated user for audit info
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Capture the category being added
        Category capturedCategory = null;
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Category>(), CancellationToken))
            .Callback<Category, CancellationToken>((c, _) => capturedCategory = c)
            .ReturnsAsync((Category c, CancellationToken _) => c);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the category was created and saved correctly
        capturedCategory.Should().NotBeNull();
        capturedCategory.Name.Should().Be(command.Name);
        capturedCategory.Slug.Value.Should().Be(command.Slug);
        capturedCategory.Description.Should().Be(command.Description);
        capturedCategory.IsActive.Should().BeTrue();

        // Verify the category was saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateSlug_ReturnsErrorResult()
    {
        // Arrange
        var command = new CreateCategoryCommandV1
        {
            Name = "Test Category",
            Slug = "test-category",
            Description = "Test category description"
        };

        // Mock that slug already exists
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, null, CancellationToken))
            .ReturnsAsync(true);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CategoryErrors.DuplicateSlug(command.Slug).Code);

        // Verify the category was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task CreateCategory_WithParentCategory_CreatesChildCategoryCorrectly()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var command = new CreateCategoryCommandV1
        {
            Name = "Child Category",
            Slug = "child-category",
            Description = "Child category description",
            ParentId = parentId
        };

        // Mock that slug doesn't exist
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, null, CancellationToken))
            .ReturnsAsync(false);

        // Create a parent category
        var parentCategory = new CategoryBuilder()
            .WithId(parentId)
            .WithName("Parent Category")
            .WithSlug("parent-category")
            .Build();

        // Mock parent category retrieval
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(parentId, CancellationToken))
            .ReturnsAsync(parentCategory);

        // Capture the category being added
        Category capturedCategory = null;
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Category>(), CancellationToken))
            .Callback<Category, CancellationToken>((c, _) => capturedCategory = c)
            .ReturnsAsync((Category c, CancellationToken _) => c);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedCategory.Should().NotBeNull();
        capturedCategory.Name.Should().Be(command.Name);
        capturedCategory.ParentId.Should().Be(command.ParentId);
        capturedCategory.Level.Should().Be(1); // Level 1 because it's a child
        capturedCategory.Path.Should().Be("/parent-category/child-category");
    }

    [Fact]
    public async Task CreateCategory_WithInvalidParentId_ReturnsErrorResult()
    {
        // Arrange
        var nonExistentParentId = Guid.NewGuid();
        var command = new CreateCategoryCommandV1
        {
            Name = "Child Category",
            Slug = "child-category",
            Description = "Child category description",
            ParentId = nonExistentParentId
        };

        // Mock that slug doesn't exist
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.SlugExistsAsync(command.Slug, null, CancellationToken))
            .ReturnsAsync(false);

        // Mock that parent category doesn't exist
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(nonExistentParentId, CancellationToken))
            .ReturnsAsync((Category)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CategoryErrors.NotFound(nonExistentParentId).Code);
    }
}
