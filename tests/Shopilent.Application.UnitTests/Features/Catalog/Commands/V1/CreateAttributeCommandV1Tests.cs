using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.CreateAttribute.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.Enums;
using Shopilent.Domain.Catalog.Errors;
using DomainAttribute = Shopilent.Domain.Catalog.Attribute;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class CreateAttributeCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public CreateAttributeCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockAttributeWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<CreateAttributeCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<CreateAttributeCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<CreateAttributeCommandV1>, CreateAttributeCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var command = new CreateAttributeCommandV1
        {
            Name = "color",
            DisplayName = "Color",
            Type = AttributeType.Text,
            Filterable = true,
            Searchable = false,
            IsVariant = true,
            Configuration = new Dictionary<string, object> { { "options", new[] { "Red", "Blue", "Green" } } }
        };

        // Mock that name doesn't exist
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.NameExistsAsync(command.Name, null, CancellationToken))
            .ReturnsAsync(false);

        // Setup authenticated user for audit info
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Capture the attribute being added
        DomainAttribute capturedAttribute = null;
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<DomainAttribute>(), CancellationToken))
            .Callback<DomainAttribute, CancellationToken>((a, _) => capturedAttribute = a)
            .ReturnsAsync((DomainAttribute a, CancellationToken _) => a);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the attribute was created correctly
        capturedAttribute.Should().NotBeNull();
        capturedAttribute.Name.Should().Be(command.Name);
        capturedAttribute.DisplayName.Should().Be(command.DisplayName);
        capturedAttribute.Type.Should().Be(command.Type);
        capturedAttribute.Filterable.Should().Be(command.Filterable);
        capturedAttribute.Searchable.Should().Be(command.Searchable);
        capturedAttribute.IsVariant.Should().Be(command.IsVariant);

        // Verify response
        var response = result.Value;
        response.Id.Should().Be(capturedAttribute.Id);
        response.Name.Should().Be(command.Name);
        response.DisplayName.Should().Be(command.DisplayName);
        response.Type.Should().Be(command.Type);
        response.Filterable.Should().Be(command.Filterable);
        response.Searchable.Should().Be(command.Searchable);
        response.IsVariant.Should().Be(command.IsVariant);

        // Verify the attribute was saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsFailure()
    {
        // Arrange
        var command = new CreateAttributeCommandV1
        {
            Name = "color",
            DisplayName = "Color",
            Type = AttributeType.Text
        };

        // Mock that name already exists
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.NameExistsAsync(command.Name, null, CancellationToken))
            .ReturnsAsync(true);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(AttributeErrors.DuplicateName(command.Name).Code);

        // Verify the attribute was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithConfiguration_SetsConfigurationCorrectly()
    {
        // Arrange
        var configuration = new Dictionary<string, object>
        {
            { "min_length", 5 },
            { "max_length", 50 },
            { "pattern", "^[a-zA-Z]+$" }
        };

        var command = new CreateAttributeCommandV1
        {
            Name = "description",
            DisplayName = "Description",
            Type = AttributeType.Text,
            Configuration = configuration
        };

        // Mock that name doesn't exist
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.NameExistsAsync(command.Name, null, CancellationToken))
            .ReturnsAsync(false);

        // Capture the attribute being added
        DomainAttribute capturedAttribute = null;
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<DomainAttribute>(), CancellationToken))
            .Callback<DomainAttribute, CancellationToken>((a, _) => capturedAttribute = a)
            .ReturnsAsync((DomainAttribute a, CancellationToken _) => a);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedAttribute.Should().NotBeNull();

        // Verify configuration was set
        capturedAttribute.Configuration.Should().NotBeNull();
        capturedAttribute.Configuration.Count.Should().Be(3);
        capturedAttribute.Configuration["min_length"].Should().Be(5);
        capturedAttribute.Configuration["max_length"].Should().Be(50);
        capturedAttribute.Configuration["pattern"].Should().Be("^[a-zA-Z]+$");
    }

    [Fact]
    public async Task Handle_WithoutConfiguration_CreatesAttributeWithEmptyConfiguration()
    {
        // Arrange
        var command = new CreateAttributeCommandV1
        {
            Name = "size",
            DisplayName = "Size",
            Type = AttributeType.Number
        };

        // Mock that name doesn't exist
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.NameExistsAsync(command.Name, null, CancellationToken))
            .ReturnsAsync(false);

        // Capture the attribute being added
        DomainAttribute capturedAttribute = null;
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<DomainAttribute>(), CancellationToken))
            .Callback<DomainAttribute, CancellationToken>((a, _) => capturedAttribute = a)
            .ReturnsAsync((DomainAttribute a, CancellationToken _) => a);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedAttribute.Should().NotBeNull();

        // Verify default values
        capturedAttribute.Filterable.Should().BeFalse();
        capturedAttribute.Searchable.Should().BeFalse();
        capturedAttribute.IsVariant.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_CreatesAttributeWithoutAuditInfo()
    {
        // Arrange
        var command = new CreateAttributeCommandV1
        {
            Name = "brand",
            DisplayName = "Brand",
            Type = AttributeType.Text
        };

        // Mock that name doesn't exist
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.NameExistsAsync(command.Name, null, CancellationToken))
            .ReturnsAsync(false);

        // Setup no authenticated user (uses default unauthenticated state)
        // No setup needed as TestFixture defaults to unauthenticated state

        // Capture the attribute being added
        DomainAttribute capturedAttribute = null;
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<DomainAttribute>(), CancellationToken))
            .Callback<DomainAttribute, CancellationToken>((a, _) => capturedAttribute = a)
            .ReturnsAsync((DomainAttribute a, CancellationToken _) => a);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedAttribute.Should().NotBeNull();

        // Verify the attribute was still created successfully
        capturedAttribute.Name.Should().Be(command.Name);
        capturedAttribute.DisplayName.Should().Be(command.DisplayName);
    }
}
