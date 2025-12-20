using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Features.Identity.Commands.Register.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Identity.Commands.V1;

public class RegisterCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public RegisterCommandV1Tests()
    {
        var services = new ServiceCollection();
        services.AddTransient(sp => Fixture.MockAuthenticationService.Object);
        services.AddTransient(sp => Fixture.GetLogger<RegisterCommandHandlerV1>());

        services.AddMediatRWithValidation();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsSuccessfulResult()
    {
        // Arrange
        var command = new RegisterCommandV1
        {
            Email = "newuser@example.com",
            Password = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            Phone = "+1234567890",
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        var expectedResponse = new AuthTokenResponse
        {
            User = null,
            AccessToken = "test-access-token",
            RefreshToken = "test-refresh-token"
        };

        // Mock successful registration
        Fixture.MockAuthenticationService
            .Setup(auth => auth.RegisterAsync(
                It.IsAny<Email>(),
                command.Password,
                command.FirstName,
                command.LastName,
                command.Phone,
                command.IpAddress,
                command.UserAgent,
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(expectedResponse.AccessToken);
        result.Value.RefreshToken.Should().Be(expectedResponse.RefreshToken);

        // Verify auth service was called with correct parameters
        Fixture.MockAuthenticationService.Verify(auth => auth.RegisterAsync(
            It.Is<Email>(e => e.Value == command.Email),
            command.Password,
            command.FirstName,
            command.LastName,
            command.Phone,
            command.IpAddress,
            command.UserAgent,
            CancellationToken), Times.Once);
    }

    [Fact]
    public async Task Register_WithNullPhone_StillSucceeds()
    {
        // Arrange
        var command = new RegisterCommandV1
        {
            Email = "newuser@example.com",
            Password = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            Phone = null, // Null phone
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        var expectedResponse = new AuthTokenResponse
        {
            User = null,
            AccessToken = "test-access-token",
            RefreshToken = "test-refresh-token"
        };

        // Mock successful registration with null phone
        Fixture.MockAuthenticationService
            .Setup(auth => auth.RegisterAsync(
                It.IsAny<Email>(),
                command.Password,
                command.FirstName,
                command.LastName,
                null, // Expect null phone
                command.IpAddress,
                command.UserAgent,
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify auth service was called with null phone
        Fixture.MockAuthenticationService.Verify(auth => auth.RegisterAsync(
            It.IsAny<Email>(),
            command.Password,
            command.FirstName,
            command.LastName,
            null, // Null phone
            command.IpAddress,
            command.UserAgent,
            CancellationToken), Times.Once);
    }

    [Fact]
    public async Task Register_WithIpAddressAndUserAgent_PassesValuesToAuthService()
    {
        // Arrange
        var command = new RegisterCommandV1
        {
            Email = "newuser@example.com",
            Password = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            Phone = "+1234567890",
            IpAddress = "192.168.1.1", // Specific IP
            UserAgent = "Custom User Agent" // Specific User Agent
        };

        var expectedResponse = new AuthTokenResponse
        {
            User = null,
            AccessToken = "test-access-token",
            RefreshToken = "test-refresh-token"
        };

        // Mock successful registration
        Fixture.MockAuthenticationService
            .Setup(auth => auth.RegisterAsync(
                It.IsAny<Email>(),
                command.Password,
                command.FirstName,
                command.LastName,
                command.Phone,
                command.IpAddress,
                command.UserAgent,
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify specific IP and user agent were passed
        Fixture.MockAuthenticationService.Verify(auth => auth.RegisterAsync(
            It.IsAny<Email>(),
            command.Password,
            command.FirstName,
            command.LastName,
            command.Phone,
            "192.168.1.1", // Specific IP
            "Custom User Agent", // Specific user agent
            CancellationToken), Times.Once);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsErrorResult()
    {
        // Arrange
        var command = new RegisterCommandV1
        {
            Email = "invalid-email",
            Password = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            Phone = "+1234567890",
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Validation.Failed");

        // Verify metadata contains email validation error
        result.Error.Metadata.Should().NotBeNull();
        result.Error.Metadata.Keys.Should().Contain("Email");

        // Auth service should not be called for invalid input
        Fixture.MockAuthenticationService.Verify(auth => auth.RegisterAsync(
            It.IsAny<Email>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsErrorResult()
    {
        // Arrange
        var command = new RegisterCommandV1
        {
            Email = "existing@example.com",
            Password = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            Phone = "+1234567890",
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        // Mock authentication service to return duplicate email error
        Fixture.MockAuthenticationService
            .Setup(auth => auth.RegisterAsync(
                It.IsAny<Email>(),
                command.Password,
                command.FirstName,
                command.LastName,
                command.Phone,
                command.IpAddress,
                command.UserAgent,
                CancellationToken))
            .ReturnsAsync(Result.Failure<AuthTokenResponse>(UserErrors.EmailAlreadyExists(command.Email)));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(UserErrors.EmailAlreadyExists(command.Email).Code);
    }

    [Fact]
    public async Task Register_WithInvalidPassword_ReturnsErrorResult()
    {
        // Arrange
        var command = new RegisterCommandV1
        {
            Email = "newuser@example.com",
            Password = "weak", // Too weak password
            FirstName = "John",
            LastName = "Doe",
            Phone = "+1234567890",
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(UserErrors.PasswordTooShort.Code);
        result.Error.Message.Should().Contain("Password must be at least 8 characters long");

    }

    [Theory]
    [InlineData("", "Doe")] // Empty first name
    [InlineData("John", "")] // Empty last name
    public async Task Register_WithEmptyNames_ReturnsValidationError(string firstName, string lastName)
    {
        // Arrange
        var command = new RegisterCommandV1
        {
            Email = "test@example.com",
            Password = "Password123!",
            FirstName = firstName,
            LastName = lastName,
            Phone = "+1234567890",
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();

        if (string.IsNullOrEmpty(firstName))
        {
            result.Error.Code.Should().Be("User.FirstNameRequired");
        }

        if (string.IsNullOrEmpty(lastName))
        {
            result.Error.Code.Should().Contain("User.LastNameRequired");
        }
    }

    [Fact]
    public async Task Register_WithInvalidPhone_ReturnsValidationError()
    {
        // Arrange
        var command = new RegisterCommandV1
        {
            Email = "test@example.com",
            Password = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            Phone = "invalid-phone", // Invalid phone number
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.InvalidPhoneFormat");
    }

    [Fact]
    public async Task Register_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var command = new RegisterCommandV1
        {
            Email = "test@example.com",
            Password = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            Phone = "+1234567890",
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        // Mock authentication service to throw an exception
        Fixture.MockAuthenticationService
            .Setup(auth => auth.RegisterAsync(
                It.IsAny<Email>(),
                command.Password,
                command.FirstName,
                command.LastName,
                command.Phone,
                command.IpAddress,
                command.UserAgent,
                CancellationToken))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Registration.Failed");
        result.Error.Message.Should().Contain("Unexpected error");
    }

    [Fact]
    public async Task Register_WithSuccessfulRegistration_VerifiesUserData()
    {
        // Arrange
        var command = new RegisterCommandV1
        {
            Email = "newuser@example.com",
            Password = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            Phone = "+1234567890",
            IpAddress = "127.0.0.1",
            UserAgent = "Test User Agent"
        };

        // Create a mock user with proper initialization
        var email = Email.Create(command.Email).Value;
        var fullName = FullName.Create(command.FirstName, command.LastName).Value;
        var user = User.Create(email, "hashedpassword", fullName).Value;

        // Create response with the user
        var expectedResponse = new AuthTokenResponse
        {
            User = user,
            AccessToken = "test-access-token",
            RefreshToken = "test-refresh-token"
        };

        // Mock successful registration with user data
        Fixture.MockAuthenticationService
            .Setup(auth => auth.RegisterAsync(
                It.IsAny<Email>(),
                command.Password,
                command.FirstName,
                command.LastName,
                command.Phone,
                command.IpAddress,
                command.UserAgent,
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(expectedResponse.AccessToken);
        result.Value.RefreshToken.Should().Be(expectedResponse.RefreshToken);
        result.Value.User.Should().NotBeNull();
        result.Value.User.Should().BeSameAs(user);
        result.Value.User.Email.Value.Should().Be(command.Email);
        result.Value.User.FullName.FirstName.Should().Be(command.FirstName);
        result.Value.User.FullName.LastName.Should().Be(command.LastName);
    }
}
