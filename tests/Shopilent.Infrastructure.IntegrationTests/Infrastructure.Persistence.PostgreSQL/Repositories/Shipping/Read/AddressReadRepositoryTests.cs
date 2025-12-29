using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Shipping.Enums;
using Shopilent.Domain.Shipping.Repositories.Read;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Shipping.Read;

[Collection("IntegrationTests")]
public class AddressReadRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;
    private IAddressReadRepository _addressReadRepository;

    public AddressReadRepositoryTests(IntegrationTestFixture fixture) : base(fixture) { }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _userWriteRepository = GetService<IUserWriteRepository>();
        _addressReadRepository = GetService<IAddressReadRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetByIdAsync_ExistingAddress_ShouldReturnAddress()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var address = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _addressReadRepository.GetByIdAsync(address.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(address.Id);
        result.UserId.Should().Be(user.Id);
        result.AddressLine1.Should().Be(address.AddressLine1);
        result.AddressLine2.Should().Be(address.AddressLine2);
        result.City.Should().Be(address.City);
        result.State.Should().Be(address.State);
        result.Country.Should().Be(address.Country);
        result.PostalCode.Should().Be(address.PostalCode);
        result.Phone.Should().Be(address.Phone?.Value);
        result.IsDefault.Should().Be(address.IsDefault);
        result.AddressType.Should().Be(address.AddressType);
        result.CreatedAt.Should().BeCloseTo(address.CreatedAt, TimeSpan.FromMilliseconds(100));
        result.UpdatedAt.Should().BeCloseTo(address.UpdatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentAddress_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _addressReadRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAllAsync_EmptyRepository_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _addressReadRepository.ListAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAllAsync_HasAddresses_ShouldReturnAllAddresses()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user1 = new UserBuilder().Build();
        var user2 = new UserBuilder().Build();

        var address1 = new AddressBuilder()
            .WithUser(user1)
            .WithUniqueData()
            .AsShipping()
            .Build();

        var address2 = new AddressBuilder()
            .WithUser(user2)
            .WithUniqueData()
            .AsBilling()
            .Build();

        await _userWriteRepository.AddAsync(user1);
        await _userWriteRepository.AddAsync(user2);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _addressReadRepository.ListAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Select(a => a.Id).Should().Contain(new[] { address1.Id, address2.Id });
    }

    [Fact]
    public async Task GetByUserIdAsync_ExistingUser_ShouldReturnUserAddresses()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user1 = new UserBuilder().Build();
        var user2 = new UserBuilder().Build();

        var address1 = new AddressBuilder()
            .WithUser(user1)
            .WithUniqueData()
            .IsDefault()
            .Build();

        var address2 = new AddressBuilder()
            .WithUser(user1)
            .WithUniqueData()
            .IsNotDefault()
            .Build();

        var address3 = new AddressBuilder()
            .WithUser(user2)
            .WithUniqueData()
            .Build();

        await _userWriteRepository.AddAsync(user1);
        await _userWriteRepository.AddAsync(user2);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _addressReadRepository.GetByUserIdAsync(user1.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.UserId == user1.Id);
        result.Select(a => a.Id).Should().Contain(new[] { address1.Id, address2.Id });

        // Should be ordered by IsDefault DESC, then CreatedAt DESC
        result.First().IsDefault.Should().BeTrue(); // address1 should be first
        result.First().Id.Should().Be(address1.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_NonExistentUser_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _addressReadRepository.GetByUserIdAsync(nonExistentUserId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDefaultAddressAsync_HasDefaultShippingAddress_ShouldReturnDefaultAddress()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var defaultAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsShipping()
            .IsDefault()
            .Build();

        var nonDefaultAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsShipping()
            .IsNotDefault()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _addressReadRepository.GetDefaultAddressAsync(user.Id, AddressType.Shipping);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(defaultAddress.Id);
        result.IsDefault.Should().BeTrue();
        result.AddressType.Should().Be(AddressType.Shipping);
    }

    [Fact]
    public async Task GetDefaultAddressAsync_HasDefaultBillingAddress_ShouldReturnDefaultAddress()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var defaultAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsBilling()
            .IsDefault()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _addressReadRepository.GetDefaultAddressAsync(user.Id, AddressType.Billing);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(defaultAddress.Id);
        result.IsDefault.Should().BeTrue();
        result.AddressType.Should().Be(AddressType.Billing);
    }

    [Fact]
    public async Task GetDefaultAddressAsync_HasDefaultBothTypeAddress_ShouldReturnForAnyType()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var defaultBothAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsBoth()
            .IsDefault()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Act - Should return the "Both" address for shipping requests
        var shippingResult = await _addressReadRepository.GetDefaultAddressAsync(user.Id, AddressType.Shipping);

        // Act - Should return the "Both" address for billing requests
        var billingResult = await _addressReadRepository.GetDefaultAddressAsync(user.Id, AddressType.Billing);

        // Assert
        shippingResult.Should().NotBeNull();
        shippingResult!.Id.Should().Be(defaultBothAddress.Id);
        shippingResult.AddressType.Should().Be(AddressType.Both);

        billingResult.Should().NotBeNull();
        billingResult!.Id.Should().Be(defaultBothAddress.Id);
        billingResult.AddressType.Should().Be(AddressType.Both);
    }

    [Fact]
    public async Task GetDefaultAddressAsync_NoDefaultAddress_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var nonDefaultAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsShipping()
            .IsNotDefault()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _addressReadRepository.GetDefaultAddressAsync(user.Id, AddressType.Shipping);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDefaultAddressAsync_OnlyOneDefaultAllowed_ShouldReturnLatestDefault()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();

        // Create first default "Both" address
        var defaultBothAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsBoth()
            .IsDefault()
            .Build();

        // Create second default "Shipping" address (should become the only default)
        var defaultShippingAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsShipping()
            .IsDefault()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Act - Request default address for shipping
        var result = await _addressReadRepository.GetDefaultAddressAsync(user.Id, AddressType.Shipping);

        // Assert - Should return the latest default address (Shipping), not the Both address
        result.Should().NotBeNull();
        result!.AddressType.Should().Be(AddressType.Shipping);
        result.Id.Should().Be(defaultShippingAddress.Id);

        // Verify only one default address exists
        var allAddresses = await _addressReadRepository.GetByUserIdAsync(user.Id);
        allAddresses.Count(a => a.IsDefault).Should().Be(1);
        allAddresses.First(a => a.Id == defaultBothAddress.Id).IsDefault.Should().BeFalse();
        allAddresses.First(a => a.Id == defaultShippingAddress.Id).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task GetByAddressTypeAsync_ShippingAddresses_ShouldReturnShippingAndBothTypes()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();

        var shippingAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsShipping()
            .IsDefault()
            .Build();

        var billingAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsBilling()
            .Build();

        var bothAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsBoth()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _addressReadRepository.GetByAddressTypeAsync(user.Id, AddressType.Shipping);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.Id == shippingAddress.Id && a.AddressType == AddressType.Shipping);
        result.Should().Contain(a => a.Id == bothAddress.Id && a.AddressType == AddressType.Both);
        result.Should().NotContain(a => a.Id == billingAddress.Id);

        // Should be ordered by IsDefault DESC, then CreatedAt DESC
        result.First().IsDefault.Should().BeTrue(); // shippingAddress should be first
        result.First().Id.Should().Be(shippingAddress.Id);
    }

    [Fact]
    public async Task GetByAddressTypeAsync_BillingAddresses_ShouldReturnBillingAndBothTypes()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();

        var shippingAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsShipping()
            .Build();

        var billingAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsBilling()
            .IsDefault()
            .Build();

        var bothAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsBoth()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _addressReadRepository.GetByAddressTypeAsync(user.Id, AddressType.Billing);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.Id == billingAddress.Id && a.AddressType == AddressType.Billing);
        result.Should().Contain(a => a.Id == bothAddress.Id && a.AddressType == AddressType.Both);
        result.Should().NotContain(a => a.Id == shippingAddress.Id);

        // Should be ordered by IsDefault DESC, then CreatedAt DESC
        result.First().IsDefault.Should().BeTrue(); // billingAddress should be first
        result.First().Id.Should().Be(billingAddress.Id);
    }

    [Fact]
    public async Task GetByAddressTypeAsync_BothType_ShouldReturnOnlyBothType()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();

        var shippingAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsShipping()
            .Build();

        var billingAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsBilling()
            .Build();

        var bothAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsBoth()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _addressReadRepository.GetByAddressTypeAsync(user.Id, AddressType.Both);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(bothAddress.Id);
        result.First().AddressType.Should().Be(AddressType.Both);
    }

    [Fact]
    public async Task GetByAddressTypeAsync_NoMatchingAddresses_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();

        var billingAddress = new AddressBuilder()
            .WithUser(user)
            .WithUniqueData()
            .AsBilling()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        // Act - Request shipping addresses when only billing exists
        var result = await _addressReadRepository.GetByAddressTypeAsync(user.Id, AddressType.Shipping);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByAddressTypeAsync_NonExistentUser_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _addressReadRepository.GetByAddressTypeAsync(nonExistentUserId, AddressType.Shipping);

        // Assert
        result.Should().BeEmpty();
    }
}
