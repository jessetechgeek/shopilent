using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Identity.Queries.GetUsersDatatable.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing;
using Shopilent.Domain.Common.Models;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Identity.Enums;
using Shopilent.Domain.Shipping.DTOs;

namespace Shopilent.Application.UnitTests.Features.Identity.Queries.V1;

public class GetUsersDatatableQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetUsersDatatableQueryV1Tests()
    {
        var services = new ServiceCollection();
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockAddressReadRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetUsersDatatableQueryHandlerV1>());

        services.AddMediatRWithValidation();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsDatatableResult()
    {
        // Arrange
        var datatableRequest = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };

        var query = new GetUsersDatatableQueryV1 { Request = datatableRequest };

        var users = new List<UserDto>
        {
            new UserDto
            {
                Id = Guid.NewGuid(),
                Email = "john.doe@example.com",
                FirstName = "John",
                LastName = "Doe",
                Phone = "+1234567890",
                Role = UserRole.Customer,
                IsActive = true,
                EmailVerified = true,
                LastLogin = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new UserDto
            {
                Id = Guid.NewGuid(),
                Email = "jane.smith@example.com",
                FirstName = "Jane",
                LastName = "Smith",
                Phone = "+9876543210",
                Role = UserRole.Admin,
                IsActive = true,
                EmailVerified = true,
                LastLogin = DateTime.UtcNow.AddHours(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                UpdatedAt = DateTime.UtcNow.AddDays(-10)
            }
        };

        var datatableResult = new DataTableResult<UserDto>(1, 100, 2, users);

        var addresses1 = new List<AddressDto>
        {
            new AddressDto { Id = Guid.NewGuid() }, new AddressDto { Id = Guid.NewGuid() }
        };

        var addresses2 = new List<AddressDto> { new AddressDto { Id = Guid.NewGuid() } };

        Fixture.MockUnitOfWork
            .Setup(uow => uow.UserReader.GetDataTableAsync(datatableRequest, CancellationToken))
            .ReturnsAsync(datatableResult);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(users[0].Id, CancellationToken))
            .ReturnsAsync(addresses1);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(users[1].Id, CancellationToken))
            .ReturnsAsync(addresses2);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Draw.Should().Be(1);
        result.Value.RecordsTotal.Should().Be(100);
        result.Value.RecordsFiltered.Should().Be(2);
        result.Value.Data.Should().HaveCount(2);

        // Verify first user
        var firstUser = result.Value.Data.First();
        firstUser.Email.Should().Be("john.doe@example.com");
        firstUser.FirstName.Should().Be("John");
        firstUser.LastName.Should().Be("Doe");
        firstUser.FullName.Should().Be("John Doe");
        firstUser.Role.Should().Be(UserRole.Customer);
        firstUser.RoleName.Should().Be("Customer");
        firstUser.IsActive.Should().BeTrue();
        firstUser.IsEmailVerified.Should().BeTrue();
        firstUser.AddressCount.Should().Be(2);

        // Verify second user
        var secondUser = result.Value.Data.Last();
        secondUser.Email.Should().Be("jane.smith@example.com");
        secondUser.FirstName.Should().Be("Jane");
        secondUser.LastName.Should().Be("Smith");
        secondUser.FullName.Should().Be("Jane Smith");
        secondUser.Role.Should().Be(UserRole.Admin);
        secondUser.RoleName.Should().Be("Admin");
        secondUser.IsActive.Should().BeTrue();
        secondUser.IsEmailVerified.Should().BeTrue();
        secondUser.AddressCount.Should().Be(1);

        // Verify repository interactions
        Fixture.MockUnitOfWork.Verify(
            uow => uow.UserReader.GetDataTableAsync(datatableRequest, CancellationToken),
            Times.Once);

        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByUserIdAsync(It.IsAny<Guid>(), CancellationToken),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_UserWithNoAddresses_ReturnsZeroAddressCount()
    {
        // Arrange
        var datatableRequest = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };

        var query = new GetUsersDatatableQueryV1 { Request = datatableRequest };

        var users = new List<UserDto>
        {
            new UserDto
            {
                Id = Guid.NewGuid(),
                Email = "no.address@example.com",
                FirstName = "No",
                LastName = "Address",
                Role = UserRole.Customer,
                IsActive = true,
                EmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            }
        };

        var datatableResult = new DataTableResult<UserDto>(1, 1, 1, users);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.UserReader.GetDataTableAsync(datatableRequest, CancellationToken))
            .ReturnsAsync(datatableResult);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(users[0].Id, CancellationToken))
            .ReturnsAsync((List<AddressDto>)null); // No addresses

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data.First().AddressCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_EmptyResults_ReturnsEmptyDataTable()
    {
        // Arrange
        var datatableRequest = new DataTableRequest { Draw = 2, Start = 10, Length = 10 };

        var query = new GetUsersDatatableQueryV1 { Request = datatableRequest };

        var emptyUsers = new List<UserDto>();
        var datatableResult = new DataTableResult<UserDto>(2, 5, 0, emptyUsers);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.UserReader.GetDataTableAsync(datatableRequest, CancellationToken))
            .ReturnsAsync(datatableResult);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Draw.Should().Be(2);
        result.Value.RecordsTotal.Should().Be(5);
        result.Value.RecordsFiltered.Should().Be(0);
        result.Value.Data.Should().BeEmpty();

        // Verify no address queries were made
        Fixture.MockAddressReadRepository.Verify(
            repo => repo.GetByUserIdAsync(It.IsAny<Guid>(), CancellationToken),
            Times.Never);
    }

    [Theory]
    [InlineData(UserRole.Customer, "Customer")]
    [InlineData(UserRole.Admin, "Admin")]
    [InlineData(UserRole.Manager, "Manager")]
    public async Task Handle_DifferentUserRoles_ReturnsCorrectRoleNames(UserRole role, string expectedRoleName)
    {
        // Arrange
        var datatableRequest = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };

        var query = new GetUsersDatatableQueryV1 { Request = datatableRequest };

        var users = new List<UserDto>
        {
            new UserDto
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                Role = role,
                IsActive = true,
                EmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            }
        };

        var datatableResult = new DataTableResult<UserDto>(1, 1, 1, users);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.UserReader.GetDataTableAsync(datatableRequest, CancellationToken))
            .ReturnsAsync(datatableResult);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(users[0].Id, CancellationToken))
            .ReturnsAsync(new List<AddressDto>());

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.First().RoleName.Should().Be(expectedRoleName);
    }

    [Fact]
    public async Task Handle_UserWithMiddleNameInFullName_TrimsCorrectly()
    {
        // Arrange
        var datatableRequest = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };

        var query = new GetUsersDatatableQueryV1 { Request = datatableRequest };

        var users = new List<UserDto>
        {
            new UserDto
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                FirstName = "John",
                LastName = "Doe",
                Role = UserRole.Customer,
                IsActive = true,
                EmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            }
        };

        var datatableResult = new DataTableResult<UserDto>(1, 1, 1, users);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.UserReader.GetDataTableAsync(datatableRequest, CancellationToken))
            .ReturnsAsync(datatableResult);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(users[0].Id, CancellationToken))
            .ReturnsAsync(new List<AddressDto>());

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.First().FullName.Should().Be("John Doe");
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var datatableRequest = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };

        var query = new GetUsersDatatableQueryV1 { Request = datatableRequest };

        Fixture.MockUnitOfWork
            .Setup(uow => uow.UserReader.GetDataTableAsync(datatableRequest, CancellationToken))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Users.GetDataTableFailed");
        result.Error.Message.Should().Contain("Failed to retrieve users");
        result.Error.Message.Should().Contain("Database connection failed");
    }

    [Fact]
    public async Task Handle_WhenAddressRepositoryThrows_ReturnsFailureResult()
    {
        // Arrange
        var datatableRequest = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };

        var query = new GetUsersDatatableQueryV1 { Request = datatableRequest };

        var users = new List<UserDto>
        {
            new UserDto
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                Role = UserRole.Customer,
                IsActive = true,
                EmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            }
        };

        var datatableResult = new DataTableResult<UserDto>(1, 1, 1, users);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.UserReader.GetDataTableAsync(datatableRequest, CancellationToken))
            .ReturnsAsync(datatableResult);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(users[0].Id, CancellationToken))
            .ThrowsAsync(new Exception("Address fetch failed"));

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Users.GetDataTableFailed");
        result.Error.Message.Should().Contain("Failed to retrieve users");
    }

    [Fact]
    public async Task Handle_InactiveUsers_ReturnsCorrectStatus()
    {
        // Arrange
        var datatableRequest = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };

        var query = new GetUsersDatatableQueryV1 { Request = datatableRequest };

        var users = new List<UserDto>
        {
            new UserDto
            {
                Id = Guid.NewGuid(),
                Email = "inactive@example.com",
                FirstName = "Inactive",
                LastName = "User",
                Role = UserRole.Customer,
                IsActive = false,
                EmailVerified = false,
                LastLogin = null,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            }
        };

        var datatableResult = new DataTableResult<UserDto>(1, 1, 1, users);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.UserReader.GetDataTableAsync(datatableRequest, CancellationToken))
            .ReturnsAsync(datatableResult);

        Fixture.MockAddressReadRepository
            .Setup(repo => repo.GetByUserIdAsync(users[0].Id, CancellationToken))
            .ReturnsAsync(new List<AddressDto>());

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var user = result.Value.Data.First();
        user.IsActive.Should().BeFalse();
        user.IsEmailVerified.Should().BeFalse();
        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PaginationRequest_PassesCorrectParameters()
    {
        // Arrange
        var datatableRequest = new DataTableRequest { Draw = 3, Start = 20, Length = 10 };

        var query = new GetUsersDatatableQueryV1 { Request = datatableRequest };

        var emptyUsers = new List<UserDto>();
        var datatableResult = new DataTableResult<UserDto>(3, 100, 0, emptyUsers);

        Fixture.MockUnitOfWork
            .Setup(uow => uow.UserReader.GetDataTableAsync(datatableRequest, CancellationToken))
            .ReturnsAsync(datatableResult);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Draw.Should().Be(3);
        result.Value.RecordsTotal.Should().Be(100);

        // Verify the exact request was passed to the repository
        Fixture.MockUnitOfWork.Verify(
            uow => uow.UserReader.GetDataTableAsync(
                It.Is<DataTableRequest>(r => r.Draw == 3 && r.Start == 20 && r.Length == 10),
                CancellationToken),
            Times.Once);
    }
}
