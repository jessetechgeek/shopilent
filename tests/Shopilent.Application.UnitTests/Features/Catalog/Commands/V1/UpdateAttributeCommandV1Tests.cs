using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.UpdateAttribute.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.Enums;
using Shopilent.Domain.Catalog.Errors;
using DomainAttribute = Shopilent.Domain.Catalog.Attribute;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class UpdateAttributeCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Helper method to create an attribute with a specific ID for testing
    /// </summary>
    private static DomainAttribute CreateAttributeWithId(Guid id, string name, string displayName, AttributeType type)
    {
        var attribute = DomainAttribute.Create(name, displayName, type).Value;
        var idProperty = typeof(DomainAttribute).GetProperty("Id");
        idProperty?.SetValue(attribute, id);
        return attribute;
    }

    public UpdateAttributeCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockAttributeWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateAttributeCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<UpdateAttributeCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<UpdateAttributeCommandV1>, UpdateAttributeCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var command = new UpdateAttributeCommandV1
        {
            Id = attributeId,
            Name = "color",
            DisplayName = "Updated Color",
            Filterable = true,
            Searchable = true,
            IsVariant = false,
            Configuration = new Dictionary<string, object> { { "options", new[] { "Red", "Blue", "Green", "Yellow" } } }
        };

        // Create existing attribute with the same ID
        var existingAttribute = CreateAttributeWithId(attributeId, "color", "Color", AttributeType.Text);
        existingAttribute.SetFilterable(false);
        existingAttribute.SetSearchable(false);
        existingAttribute.SetIsVariant(true);

        // Mock attribute retrieval
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(existingAttribute);

        // Setup authenticated user for audit info
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Capture the updated attribute
        DomainAttribute capturedAttribute = null;
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<DomainAttribute>(), CancellationToken))
            .Callback<DomainAttribute, CancellationToken>((a, _) => capturedAttribute = a)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the attribute was updated correctly
        capturedAttribute.Should().NotBeNull();
        capturedAttribute.DisplayName.Should().Be(command.DisplayName);
        capturedAttribute.Filterable.Should().Be(command.Filterable);
        capturedAttribute.Searchable.Should().Be(command.Searchable);
        capturedAttribute.IsVariant.Should().Be(command.IsVariant);

        // Verify configuration was updated
        capturedAttribute.Configuration.Should().NotBeNull();
        capturedAttribute.Configuration.Should().ContainSingle();
        capturedAttribute.Configuration.Keys.Should().Contain("options");

        // Verify response
        var response = result.Value;
        response.Id.Should().Be(attributeId);
        response.DisplayName.Should().Be(command.DisplayName);
        response.Filterable.Should().Be(command.Filterable);
        response.Searchable.Should().Be(command.Searchable);
        response.IsVariant.Should().Be(command.IsVariant);

        // Verify the attribute was saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentAttribute_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new UpdateAttributeCommandV1
        {
            Id = nonExistentId,
            Name = "color",
            DisplayName = "Updated Color"
        };

        // Mock that attribute doesn't exist
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(nonExistentId, CancellationToken))
            .ReturnsAsync((DomainAttribute)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(AttributeErrors.NotFound(nonExistentId).Code);

        // Verify the attribute was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UpdateProperties_UpdatesOnlyChangedProperties()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var command = new UpdateAttributeCommandV1
        {
            Id = attributeId,
            Name = "size",
            DisplayName = "Product Size",
            Filterable = true,   // Changed from false
            Searchable = false,  // Unchanged
            IsVariant = true,    // Unchanged
        };

        // Create existing attribute with initial values
        var existingAttribute = CreateAttributeWithId(attributeId, "size", "Size", AttributeType.Number);
        existingAttribute.SetFilterable(false);  // Will be changed
        existingAttribute.SetSearchable(false);  // Will remain same
        existingAttribute.SetIsVariant(true);    // Will remain same

        // Mock attribute retrieval
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(existingAttribute);

        // Capture the updated attribute
        DomainAttribute capturedAttribute = null;
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<DomainAttribute>(), CancellationToken))
            .Callback<DomainAttribute, CancellationToken>((a, _) => capturedAttribute = a)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedAttribute.Should().NotBeNull();

        // Verify only changed properties were updated
        capturedAttribute.DisplayName.Should().Be(command.DisplayName);
        capturedAttribute.Filterable.Should().BeTrue();   // Changed from false to true
        capturedAttribute.Searchable.Should().BeFalse();  // Remained false
        capturedAttribute.IsVariant.Should().BeTrue();    // Remained true
    }

    [Fact]
    public async Task Handle_UpdateConfiguration_ReplacesEntireConfiguration()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var newConfiguration = new Dictionary<string, object>
        {
            { "new_option", "new_value" },
            { "another_option", 42 }
        };

        var command = new UpdateAttributeCommandV1
        {
            Id = attributeId,
            Name = "material",
            DisplayName = "Material",
            Configuration = newConfiguration
        };

        // Create existing attribute with initial configuration
        var existingAttribute = CreateAttributeWithId(attributeId, "material", "Material", AttributeType.Text);
        existingAttribute.UpdateConfiguration("old_option", "old_value");
        existingAttribute.UpdateConfiguration("existing_option", "existing_value");

        // Mock attribute retrieval
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(existingAttribute);

        // Capture the updated attribute
        DomainAttribute capturedAttribute = null;
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<DomainAttribute>(), CancellationToken))
            .Callback<DomainAttribute, CancellationToken>((a, _) => capturedAttribute = a)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedAttribute.Should().NotBeNull();

        // Verify configuration was completely replaced
        capturedAttribute.Configuration.Count.Should().Be(2);
        capturedAttribute.Configuration.Keys.Should().Contain("new_option");
        capturedAttribute.Configuration.Keys.Should().Contain("another_option");
        capturedAttribute.Configuration.Keys.Should().NotContain("old_option");
        capturedAttribute.Configuration.Keys.Should().NotContain("existing_option");
        capturedAttribute.Configuration["new_option"].Should().Be("new_value");
        capturedAttribute.Configuration["another_option"].Should().Be(42);
    }

    [Fact]
    public async Task Handle_NullConfiguration_DoesNotUpdateConfiguration()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var command = new UpdateAttributeCommandV1
        {
            Id = attributeId,
            Name = "brand",
            DisplayName = "Updated Brand",
            Configuration = null
        };

        // Create existing attribute with initial configuration
        var existingAttribute = CreateAttributeWithId(attributeId, "brand", "Brand", AttributeType.Text);
        existingAttribute.UpdateConfiguration("existing_option", "existing_value");
        var originalConfigCount = existingAttribute.Configuration.Count;

        // Mock attribute retrieval
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(existingAttribute);

        // Capture the updated attribute
        DomainAttribute capturedAttribute = null;
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<DomainAttribute>(), CancellationToken))
            .Callback<DomainAttribute, CancellationToken>((a, _) => capturedAttribute = a)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedAttribute.Should().NotBeNull();

        // Verify configuration was not modified
        capturedAttribute.Configuration.Count.Should().Be(originalConfigCount);
        capturedAttribute.Configuration.Keys.Should().Contain("existing_option");
        capturedAttribute.Configuration["existing_option"].Should().Be("existing_value");
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_UpdatesAttributeWithoutAuditInfo()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var command = new UpdateAttributeCommandV1
        {
            Id = attributeId,
            Name = "weight",
            DisplayName = "Updated Weight"
        };

        // Create existing attribute
        var existingAttribute = CreateAttributeWithId(attributeId, "weight", "Weight", AttributeType.Number);

        // Mock attribute retrieval
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(existingAttribute);

        // Setup no authenticated user (uses default unauthenticated state)
        // No setup needed as TestFixture defaults to unauthenticated state

        // Capture the updated attribute
        DomainAttribute capturedAttribute = null;
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<DomainAttribute>(), CancellationToken))
            .Callback<DomainAttribute, CancellationToken>((a, _) => capturedAttribute = a)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedAttribute.Should().NotBeNull();

        // Verify the attribute was still updated successfully
        capturedAttribute.DisplayName.Should().Be(command.DisplayName);
    }
}
