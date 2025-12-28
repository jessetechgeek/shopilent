using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.DeleteAttribute.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Enums;
using Shopilent.Domain.Catalog.Errors;
using DomainAttribute = Shopilent.Domain.Catalog.Attribute;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class DeleteAttributeCommandV1Tests : TestBase
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

    public DeleteAttributeCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockAttributeWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<DeleteAttributeCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<DeleteAttributeCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<DeleteAttributeCommandV1>, DeleteAttributeCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var command = new DeleteAttributeCommandV1
        {
            Id = attributeId
        };

        // Create existing attribute
        var existingAttribute = CreateAttributeWithId(attributeId, "color", "Color", AttributeType.Text);

        // Mock attribute retrieval
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(existingAttribute);

        // Mock product search returns empty list (attribute not in use)
        Fixture.MockProductReadRepository
            .Setup(repo => repo.SearchAsync($"attribute:{existingAttribute.Name}",
                It.IsAny<Guid?>(), CancellationToken))
            .ReturnsAsync(new List<ProductDto>());

        // Mock delete operation
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.DeleteAsync(existingAttribute, CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify delete was called
        Fixture.MockAttributeWriteRepository.Verify(
            repo => repo.DeleteAsync(existingAttribute, CancellationToken),
            Times.Once);

        // Verify the changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentAttribute_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new DeleteAttributeCommandV1
        {
            Id = nonExistentId
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

        // Verify delete was never called
        Fixture.MockAttributeWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<DomainAttribute>(), CancellationToken),
            Times.Never);

        // Verify the changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AttributeInUse_ReturnsConflict()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var command = new DeleteAttributeCommandV1
        {
            Id = attributeId
        };

        // Create existing attribute
        var existingAttribute = CreateAttributeWithId(attributeId, "size", "Size", AttributeType.Text);

        // Mock attribute retrieval
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(existingAttribute);

        // Mock product search returns products using this attribute
        var productsUsingAttribute = new List<ProductDto>
        {
            // Create mock product DTOs - we just need the count to be > 0
            new ProductDto { Id = Guid.NewGuid(), Name = "Product 1", Slug = "product-1" },
            new ProductDto { Id = Guid.NewGuid(), Name = "Product 2", Slug = "product-2" }
        };

        Fixture.MockProductReadRepository
            .Setup(repo => repo.SearchAsync($"attribute:{existingAttribute.Name}",
                It.IsAny<Guid?>(), CancellationToken))
            .ReturnsAsync(productsUsingAttribute);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Attribute.InUse");
        result.Error.Message.Should().Contain("Cannot delete attribute");
        result.Error.Message.Should().Contain("2 products");

        // Verify delete was never called
        Fixture.MockAttributeWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<DomainAttribute>(), CancellationToken),
            Times.Never);

        // Verify the changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AttributeUsedBySingleProduct_ReturnsConflictWithCorrectCount()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var command = new DeleteAttributeCommandV1
        {
            Id = attributeId
        };

        // Create existing attribute
        var existingAttribute = CreateAttributeWithId(attributeId, "brand", "Brand", AttributeType.Text);

        // Mock attribute retrieval
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(existingAttribute);

        // Mock product search returns single product using this attribute
        var productsUsingAttribute = new List<ProductDto>
        {
            new ProductDto { Id = Guid.NewGuid(), Name = "Single Product", Slug = "single-product" }
        };

        Fixture.MockProductReadRepository
            .Setup(repo => repo.SearchAsync($"attribute:{existingAttribute.Name}",
                It.IsAny<Guid?>(), CancellationToken))
            .ReturnsAsync(productsUsingAttribute);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Attribute.InUse");
        result.Error.Message.Should().Contain("1 products");
    }

    [Fact]
    public async Task Handle_ProductSearchReturnsNull_DeletesSuccessfully()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var command = new DeleteAttributeCommandV1
        {
            Id = attributeId
        };

        // Create existing attribute
        var existingAttribute = CreateAttributeWithId(attributeId, "material", "Material", AttributeType.Text);

        // Mock attribute retrieval
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(existingAttribute);

        // Mock product search returns null (no products found)
        Fixture.MockProductReadRepository
            .Setup(repo => repo.SearchAsync($"attribute:{existingAttribute.Name}",
                It.IsAny<Guid?>(), CancellationToken))
            .ReturnsAsync((List<ProductDto>)null);

        // Mock delete operation
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.DeleteAsync(existingAttribute, CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify delete was called
        Fixture.MockAttributeWriteRepository.Verify(
            repo => repo.DeleteAsync(existingAttribute, CancellationToken),
            Times.Once);

        // Verify the changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeleteOperationThrowsException_ReturnsFailure()
    {
        // Arrange
        var attributeId = Guid.NewGuid();
        var command = new DeleteAttributeCommandV1
        {
            Id = attributeId
        };

        // Create existing attribute
        var existingAttribute = CreateAttributeWithId(attributeId, "weight", "Weight", AttributeType.Number);

        // Mock attribute retrieval
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.GetByIdAsync(attributeId, CancellationToken))
            .ReturnsAsync(existingAttribute);

        // Mock product search returns empty list
        Fixture.MockProductReadRepository
            .Setup(repo => repo.SearchAsync($"attribute:{existingAttribute.Name}",
                It.IsAny<Guid?>(), CancellationToken))
            .ReturnsAsync(new List<ProductDto>());

        // Mock delete operation throws exception
        Fixture.MockAttributeWriteRepository
            .Setup(repo => repo.DeleteAsync(existingAttribute, CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Attribute.DeleteFailed");
        result.Error.Message.Should().Be("Failed to delete attribute");

        // Verify delete was attempted
        Fixture.MockAttributeWriteRepository.Verify(
            repo => repo.DeleteAsync(existingAttribute, CancellationToken),
            Times.Once);

        // Verify the changes were not saved due to exception
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }
}
