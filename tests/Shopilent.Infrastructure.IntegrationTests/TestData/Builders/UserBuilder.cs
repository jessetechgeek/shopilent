using Bogus;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Enums;
using Shopilent.Domain.Identity.ValueObjects;

namespace Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

public class UserBuilder
{
    private Email _email;
    private FullName _fullName;
    private string _username;
    private UserRole _role;
    private bool _isActive;
    private bool _isEmailVerified;
    private PhoneNumber? _phoneNumber;

    public UserBuilder()
    {
        var faker = new Faker();
        _email = Email.Create(faker.Internet.Email()).Value;
        _fullName = FullName.Create(faker.Name.FirstName(), faker.Name.LastName()).Value;
        _username = faker.Internet.UserName();
        _role = UserRole.Customer;
        _isActive = true;
        _isEmailVerified = false;
        _phoneNumber = null;
    }

    public UserBuilder WithEmail(string email)
    {
        _email = Email.Create(email).Value;
        return this;
    }

    public UserBuilder WithFullName(string firstName, string lastName)
    {
        _fullName = FullName.Create(firstName, lastName).Value;
        return this;
    }

    public UserBuilder WithUsername(string username)
    {
        _username = username;
        return this;
    }

    public UserBuilder WithRole(UserRole role)
    {
        _role = role;
        return this;
    }

    public UserBuilder AsAdmin()
    {
        _role = UserRole.Admin;
        return this;
    }

    public UserBuilder AsCustomer()
    {
        _role = UserRole.Customer;
        return this;
    }

    public UserBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public UserBuilder AsActive()
    {
        _isActive = true;
        return this;
    }

    public UserBuilder WithVerifiedEmail()
    {
        _isEmailVerified = true;
        return this;
    }

    public UserBuilder WithUnverifiedEmail()
    {
        _isEmailVerified = false;
        return this;
    }

    public UserBuilder WithPhoneNumber(string phoneNumber)
    {
        _phoneNumber = PhoneNumber.Create(phoneNumber).Value;
        return this;
    }

    public User Build()
    {
        // For testing, use a simple password hash
        var user = User.Create(_email, "test-password-hash", _fullName, _role).Value;
        
        if (!_isActive)
        {
            user.Deactivate();
        }
        
        if (_isEmailVerified)
        {
            user.VerifyEmail();
        }
        
        if (_phoneNumber != null)
        {
            user.UpdatePersonalInfo(user.FullName, _phoneNumber);
        }

        return user;
    }

    public static UserBuilder Random()
    {
        return new UserBuilder();
    }

    public static UserBuilder AdminUser()
    {
        return Random().AsAdmin().WithVerifiedEmail();
    }

    public static UserBuilder CustomerUser()
    {
        return Random().AsCustomer();
    }

    public static User CreateDefaultUser()
    {
        return Random().Build();
    }

    public static List<User> CreateMany(int count)
    {
        var users = new List<User>();
        
        for (int i = 0; i < count; i++)
        {
            users.Add(Random().Build());
        }
        
        return users;
    }
}