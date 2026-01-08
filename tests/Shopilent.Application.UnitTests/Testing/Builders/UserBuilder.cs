using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Identity.Enums;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Application.UnitTests.Testing.Builders;

public class UserBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _email = "test@example.com";
    private string _firstName = "Test";
    private string _lastName = "User";
    private string _passwordHash = "hashed_password_123";
    private UserRole _role = UserRole.Customer;
    private bool _isActive = true;
    private bool _emailVerified = false;
    private int _failedLoginAttempts = 0;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;

    public UserBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }
    
    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }
    
    public UserBuilder WithName(string firstName, string lastName)
    {
        _firstName = firstName;
        _lastName = lastName;
        return this;
    }
    
    public UserBuilder WithPasswordHash(string passwordHash)
    {
        _passwordHash = passwordHash;
        return this;
    }
    
    public UserBuilder WithRole(UserRole role)
    {
        _role = role;
        return this;
    }
    
    public UserBuilder IsInactive()
    {
        _isActive = false;
        return this;
    }
    
    public UserBuilder WithEmailVerified()
    {
        _emailVerified = true;
        return this;
    }
    
    public UserBuilder WithFailedLoginAttempts(int attempts)
    {
        _failedLoginAttempts = attempts;
        return this;
    }
    
    public UserBuilder CreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public UserDto BuildDto()
    {
        return new UserDto
        {
            Id = _id,
            Email = _email,
            FirstName = _firstName,
            LastName = _lastName,
            MiddleName = null,
            Phone = null,
            Role = _role,
            IsActive = _isActive,
            LastLogin = null,
            EmailVerified = _emailVerified,
            CreatedAt = _createdAt,
            UpdatedAt = _updatedAt
        };
    }

    public User Build()
    {
        var emailResult = Email.Create(_email);
        if (emailResult.IsFailure)
            throw new InvalidOperationException($"Invalid email: {_email}");
            
        var fullNameResult = FullName.Create(_firstName, _lastName);
        if (fullNameResult.IsFailure)
            throw new InvalidOperationException($"Invalid name: {_firstName} {_lastName}");
            
        var userResult = User.Create(emailResult.Value, _passwordHash, fullNameResult.Value, _role);
        if (userResult.IsFailure)
            throw new InvalidOperationException($"Failed to create user: {userResult.Error.Message}");
            
        var user = userResult.Value;
        
        // Use reflection to set private properties
        SetPrivatePropertyValue(user, "Id", _id);
        SetPrivatePropertyValue(user, "CreatedAt", _createdAt);
        SetPrivatePropertyValue(user, "UpdatedAt", _updatedAt);
        SetPrivatePropertyValue(user, "EmailVerified", _emailVerified);
        SetPrivatePropertyValue(user, "FailedLoginAttempts", _failedLoginAttempts);
        
        // Set inactive if needed
        if (!_isActive)
        {
            user.Deactivate();
        }
        
        return user;
    }
    
    private static void SetPrivatePropertyValue<T>(object obj, string propertyName, T value)
    {
        var propertyInfo = obj.GetType().GetProperty(propertyName);
        if (propertyInfo != null)
        {
            propertyInfo.SetValue(obj, value, null);
        }
        else
        {
            var fieldInfo = obj.GetType().GetField(propertyName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
                
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(obj, value);
            }
            else
            {
                throw new InvalidOperationException($"Property or field {propertyName} not found on type {obj.GetType().Name}");
            }
        }
    }
}