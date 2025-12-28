using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Email;
using Shopilent.Application.Abstractions.Events;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Imaging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Abstractions.S3Storage;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Domain.Sales.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Write;
using Shopilent.Domain.Shipping.Repositories.Read;
using Shopilent.Domain.Shipping.Repositories.Write;

namespace Shopilent.Application.UnitTests.Testing;

/// <summary>
/// Test fixture for setting up shared resources for unit tests
/// </summary>
public class TestFixture
{
    // Mock services
    public Mock<IUnitOfWork> MockUnitOfWork { get; private set; }
    public Mock<ICurrentUserContext> MockCurrentUserContext { get; private set; }
    public Mock<ICacheService> MockCacheService { get; private set; }
    public Mock<IAuthenticationService> MockAuthenticationService { get; private set; }
    public Mock<IEmailService> MockEmailService { get; private set; }
    public Mock<IDomainEventService> MockDomainEventService { get; private set; }
    public Mock<IImageService> MockImageService { get; private set; }
    public Mock<IPaymentService> MockPaymentService { get; private set; }
    public Mock<IS3StorageService> MockS3StorageService { get; private set; }
    public Mock<ISearchService> MockSearchService { get; private set; }

    // Catalog repositories
    public Mock<ICategoryReadRepository> MockCategoryReadRepository { get; private set; }
    public Mock<ICategoryWriteRepository> MockCategoryWriteRepository { get; private set; }
    public Mock<IProductReadRepository> MockProductReadRepository { get; private set; }
    public Mock<IProductWriteRepository> MockProductWriteRepository { get; private set; }
    public Mock<IProductVariantReadRepository> MockProductVariantReadRepository { get; private set; }
    public Mock<IProductVariantWriteRepository> MockProductVariantWriteRepository { get; private set; }
    public Mock<IAttributeReadRepository> MockAttributeReadRepository { get; private set; }
    public Mock<IAttributeWriteRepository> MockAttributeWriteRepository { get; private set; }

    // Identity repositories
    public Mock<IUserReadRepository> MockUserReadRepository { get; private set; }
    public Mock<IUserWriteRepository> MockUserWriteRepository { get; private set; }
    public Mock<IRefreshTokenReadRepository> MockRefreshTokenReadRepository { get; private set; }
    public Mock<IRefreshTokenWriteRepository> MockRefreshTokenWriteRepository { get; private set; }

    // Sales repositories
    public Mock<ICartReadRepository> MockCartReadRepository { get; private set; }
    public Mock<ICartWriteRepository> MockCartWriteRepository { get; private set; }
    public Mock<IOrderReadRepository> MockOrderReadRepository { get; private set; }
    public Mock<IOrderWriteRepository> MockOrderWriteRepository { get; private set; }

    // Payment repositories
    public Mock<IPaymentReadRepository> MockPaymentReadRepository { get; private set; }
    public Mock<IPaymentWriteRepository> MockPaymentWriteRepository { get; private set; }
    public Mock<IPaymentMethodReadRepository> MockPaymentMethodReadRepository { get; private set; }
    public Mock<IPaymentMethodWriteRepository> MockPaymentMethodWriteRepository { get; private set; }

    // Shipping repositories
    public Mock<IAddressReadRepository> MockAddressReadRepository { get; private set; }
    public Mock<IAddressWriteRepository> MockAddressWriteRepository { get; private set; }

    // Generic mocks for different logger types
    private readonly Dictionary<Type, object> _loggers = new();

    public TestFixture()
    {
        SetUpMocks();
    }

    private void SetUpMocks()
    {
        // Initialize service mocks
        MockUnitOfWork = new Mock<IUnitOfWork>();
        MockCurrentUserContext = new Mock<ICurrentUserContext>();
        MockCacheService = new Mock<ICacheService>();
        MockAuthenticationService = new Mock<IAuthenticationService>();
        MockEmailService = new Mock<IEmailService>();
        MockDomainEventService = new Mock<IDomainEventService>();
        MockImageService = new Mock<IImageService>();
        MockPaymentService = new Mock<IPaymentService>();
        MockS3StorageService = new Mock<IS3StorageService>();
        MockSearchService = new Mock<ISearchService>();

        // Initialize catalog repository mocks
        MockCategoryReadRepository = new Mock<ICategoryReadRepository>();
        MockCategoryWriteRepository = new Mock<ICategoryWriteRepository>();
        MockProductReadRepository = new Mock<IProductReadRepository>();
        MockProductWriteRepository = new Mock<IProductWriteRepository>();
        MockProductVariantReadRepository = new Mock<IProductVariantReadRepository>();
        MockProductVariantWriteRepository = new Mock<IProductVariantWriteRepository>();
        MockAttributeReadRepository = new Mock<IAttributeReadRepository>();
        MockAttributeWriteRepository = new Mock<IAttributeWriteRepository>();

        // Initialize identity repository mocks
        MockUserReadRepository = new Mock<IUserReadRepository>();
        MockUserWriteRepository = new Mock<IUserWriteRepository>();
        MockRefreshTokenReadRepository = new Mock<IRefreshTokenReadRepository>();
        MockRefreshTokenWriteRepository = new Mock<IRefreshTokenWriteRepository>();

        // Initialize sales repository mocks
        MockCartReadRepository = new Mock<ICartReadRepository>();
        MockCartWriteRepository = new Mock<ICartWriteRepository>();
        MockOrderReadRepository = new Mock<IOrderReadRepository>();
        MockOrderWriteRepository = new Mock<IOrderWriteRepository>();

        // Initialize payment repository mocks
        MockPaymentReadRepository = new Mock<IPaymentReadRepository>();
        MockPaymentWriteRepository = new Mock<IPaymentWriteRepository>();
        MockPaymentMethodReadRepository = new Mock<IPaymentMethodReadRepository>();
        MockPaymentMethodWriteRepository = new Mock<IPaymentMethodWriteRepository>();

        // Initialize shipping repository mocks
        MockAddressReadRepository = new Mock<IAddressReadRepository>();
        MockAddressWriteRepository = new Mock<IAddressWriteRepository>();

        // Set up Unit of Work to return the repository mocks
        SetupUnitOfWorkRepositories();

        // Setup default save changes to return success
        MockUnitOfWork.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Set up basic CurrentUserContext behaviors
        MockCurrentUserContext.Setup(ctx => ctx.UserId).Returns((Guid?)null);
        MockCurrentUserContext.Setup(ctx => ctx.IsAuthenticated).Returns(false);
    }

    private void SetupUnitOfWorkRepositories()
    {
        MockUnitOfWork.Setup(uow => uow.ProductReader).Returns(MockProductReadRepository.Object);
        MockUnitOfWork.Setup(uow => uow.ProductWriter).Returns(MockProductWriteRepository.Object);
        MockUnitOfWork.Setup(uow => uow.ProductVariantReader).Returns(MockProductVariantReadRepository.Object);
        MockUnitOfWork.Setup(uow => uow.ProductVariantWriter).Returns(MockProductVariantWriteRepository.Object);
    }

    /// <summary>
    /// Get a mock logger for the specified type
    /// </summary>
    public ILogger<T> GetLogger<T>()
    {
        var type = typeof(T);
        if (!_loggers.ContainsKey(type))
        {
            // Create a NullLogger instead of a mocked logger
            _loggers[type] = NullLoggerFactory.Instance.CreateLogger<T>();
        }

        return (ILogger<T>)_loggers[type];
    }

    /// <summary>
    /// Set the current user context for tests requiring an authenticated user
    /// </summary>
    public void SetAuthenticatedUser(Guid userId, string email = "test@example.com", bool isAdmin = false)
    {
        MockCurrentUserContext.Setup(ctx => ctx.UserId).Returns(userId);
        MockCurrentUserContext.Setup(ctx => ctx.Email).Returns(email);
        MockCurrentUserContext.Setup(ctx => ctx.IsAuthenticated).Returns(true);
        MockCurrentUserContext.Setup(ctx => ctx.IsInRole("Admin")).Returns(isAdmin);
    }

    /// <summary>
    /// Reset all mocks to their initial state
    /// </summary>
    public void ResetMocks()
    {
        // Reset all service mocks
        MockUnitOfWork.Reset();
        MockCurrentUserContext.Reset();
        MockCacheService.Reset();
        MockAuthenticationService.Reset();
        MockEmailService.Reset();
        MockDomainEventService.Reset();
        MockImageService.Reset();
        MockPaymentService.Reset();
        MockS3StorageService.Reset();
        MockSearchService.Reset();

        // Reset all repository mocks
        MockCategoryReadRepository.Reset();
        MockCategoryWriteRepository.Reset();
        MockProductReadRepository.Reset();
        MockProductWriteRepository.Reset();
        MockProductVariantReadRepository.Reset();
        MockProductVariantWriteRepository.Reset();
        MockAttributeReadRepository.Reset();
        MockAttributeWriteRepository.Reset();
        MockUserReadRepository.Reset();
        MockUserWriteRepository.Reset();
        MockRefreshTokenReadRepository.Reset();
        MockRefreshTokenWriteRepository.Reset();
        MockCartReadRepository.Reset();
        MockCartWriteRepository.Reset();
        MockOrderReadRepository.Reset();
        MockOrderWriteRepository.Reset();
        MockPaymentReadRepository.Reset();
        MockPaymentWriteRepository.Reset();
        MockPaymentMethodReadRepository.Reset();
        MockPaymentMethodWriteRepository.Reset();
        MockAddressReadRepository.Reset();
        MockAddressWriteRepository.Reset();

        // Re-setup the mocks with default behavior
        SetUpMocks();
    }
}
