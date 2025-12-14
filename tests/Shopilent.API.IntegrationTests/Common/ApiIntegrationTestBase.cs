using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;
using Shopilent.API.Common.Models;
using MediatR;
using Shopilent.Application.Features.Identity.Commands.Register.V1;
using Shopilent.Application.Features.Identity.Commands.ChangeUserRole.V1;
using Shopilent.Domain.Identity.Enums;
using Shopilent.Application.Abstractions.Search;

namespace Shopilent.API.IntegrationTests.Common;

[Collection("ApiIntegration")]
public abstract class ApiIntegrationTestBase : IAsyncLifetime
{
    protected readonly ApiIntegrationTestWebFactory Factory;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions;

    protected ApiIntegrationTestBase(ApiIntegrationTestWebFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        Client.BaseAddress = new Uri(Client.BaseAddress!, "api/");

        // Set mobile client header for integration tests to receive tokens in response body
        // This ensures tests continue to work without cookie handling complexity
        // Web client (cookie) behavior should be tested separately if needed
        Client.DefaultRequestHeaders.Add("X-Client-Platform", "mobile");

        JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true
        };
    }

    public virtual async Task InitializeAsync()
    {
        // Reset database before each test
        await Factory.ResetDatabaseAsync();
    }

    public virtual Task DisposeAsync()
    {
        // Cleanup can be done here if needed
        return Task.CompletedTask;
    }

    protected async Task<T?> GetAsync<T>(string endpoint)
    {
        var response = await Client.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    protected async Task<HttpResponseMessage> PostAsync<T>(string endpoint, T data)
    {
        return await Client.PostAsJsonAsync(endpoint, data, JsonOptions);
    }

    protected async Task<HttpResponseMessage> PutAsync<T>(string endpoint, T data)
    {
        return await Client.PutAsJsonAsync(endpoint, data, JsonOptions);
    }

    protected async Task<HttpResponseMessage> DeleteAsync(string endpoint)
    {
        return await Client.DeleteAsync(endpoint);
    }

    protected async Task<TResponse?> PostAndGetAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        var response = await Client.PostAsJsonAsync(endpoint, request, JsonOptions);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TResponse>(json, JsonOptions);
    }

    protected async Task<TResponse?> PutAndGetAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        var response = await Client.PutAsJsonAsync(endpoint, request, JsonOptions);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TResponse>(json, JsonOptions);
    }

    protected ApplicationDbContext GetDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    protected async Task ExecuteDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(dbContext);
    }

    protected async Task<T> ExecuteDbContextAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(dbContext);
    }

    // Authentication helpers
    protected async Task<string> AuthenticateAsync(string email = "admin@shopilent.com", string password = "Admin123!")
    {
        var loginRequest = new { Email = email, Password = password };

        var response = await Client.PostAsJsonAsync("v1/auth/login", loginRequest, JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Authentication failed: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(content, JsonOptions);

        return loginResponse?.Data?.AccessToken ??
               throw new InvalidOperationException("Access token not found in response");
    }

    protected async Task<string> AuthenticateAsAdminAsync()
    {
        await EnsureAdminUserExistsAsync();
        return await AuthenticateAsync("admin@shopilent.com", "Admin123!");
    }

    protected async Task<string> AuthenticateAsCustomerAsync()
    {
        await EnsureCustomerUserExistsAsync();
        return await AuthenticateAsync("customer@shopilent.com", "Customer123!");
    }

    protected async Task<string> AuthenticateAsManagerAsync()
    {
        await EnsureManagerUserExistsAsync();
        return await AuthenticateAsync("manager@shopilent.com", "Manager123!");
    }

    protected void SetAuthenticationHeader(string accessToken)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    protected void ClearAuthenticationHeader()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    // Enhanced HTTP helpers with ApiResponse unwrapping
    protected async Task<ApiResponse<T>?> GetApiResponseAsync<T>(string endpoint) where T : class
    {
        var response = await Client.GetAsync(endpoint);
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions);
    }

    protected async Task<ApiResponse<T>?> PostApiResponseAsync<TRequest, T>(string endpoint, TRequest data)
        where T : class
    {
        var response = await Client.PostAsJsonAsync(endpoint, data, JsonOptions);
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions);
    }

    protected async Task<ApiResponse<T>?> PutApiResponseAsync<TRequest, T>(string endpoint, TRequest data)
        where T : class
    {
        var response = await Client.PutAsJsonAsync(endpoint, data, JsonOptions);
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions);
    }

    protected async Task<HttpResponseMessage> DeleteApiResponseAsync(string endpoint)
    {
        return await Client.DeleteAsync(endpoint);
    }

    protected async Task<ApiResponse<TestDataTableResult<T>>?> PostDataTableResponseAsync<T>(string endpoint,
        object request)
    {
        var response = await Client.PostAsJsonAsync(endpoint, request, JsonOptions);
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<TestDataTableResult<T>>>(json, JsonOptions);
    }

    // Multipart/form-data helpers for file upload endpoints
    protected async Task<ApiResponse<T>?> PostMultipartApiResponseAsync<T>(string endpoint, object data, List<(string name, Stream content, string fileName)>? files = null)
        where T : class
    {
        var response = await PostMultipartAsync(endpoint, data, files);
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions);
    }

    protected async Task<HttpResponseMessage> PostMultipartAsync(string endpoint, object data, List<(string name, Stream content, string fileName)>? files = null)
    {
        using var formContent = new MultipartFormDataContent();

        // Serialize the object to a dictionary of key-value pairs
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);

        if (dictionary != null)
        {
            foreach (var kvp in dictionary)
            {
                var value = kvp.Value;

                // Handle arrays/lists - serialize as JSON string (matches React app behavior)
                if (value.ValueKind == JsonValueKind.Array)
                {
                    var array = value.EnumerateArray().ToList();

                    // If empty array, skip
                    if (!array.Any())
                    {
                        continue;
                    }

                    // Serialize all arrays as JSON strings to match React app behavior
                    var arrayJson = JsonSerializer.Serialize(value, JsonOptions);
                    formContent.Add(new StringContent(arrayJson), kvp.Key);
                    continue;
                }

                // Handle objects (serialize as JSON string)
                if (value.ValueKind == JsonValueKind.Object)
                {
                    var objectJson = JsonSerializer.Serialize(value, JsonOptions);
                    formContent.Add(new StringContent(objectJson), kvp.Key);
                    continue;
                }

                // Handle null
                if (value.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                // Handle primitives
                var stringValue = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString() ?? string.Empty,
                    JsonValueKind.Number => value.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => value.ToString()
                };

                formContent.Add(new StringContent(stringValue), kvp.Key);
            }
        }

        // Add files if provided
        if (files != null)
        {
            foreach (var file in files)
            {
                var streamContent = new StreamContent(file.content);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                formContent.Add(streamContent, file.name, file.fileName);
            }
        }

        return await Client.PostAsync(endpoint, formContent);
    }

    protected async Task<ApiResponse<T>?> PutMultipartApiResponseAsync<T>(string endpoint, object data, List<(string name, Stream content, string fileName)>? files = null)
        where T : class
    {
        var response = await PutMultipartAsync(endpoint, data, files);
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions);
    }

    protected async Task<HttpResponseMessage> PutMultipartAsync(string endpoint, object data, List<(string name, Stream content, string fileName)>? files = null)
    {
        using var formContent = new MultipartFormDataContent();

        // Serialize the object to a dictionary of key-value pairs
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);

        if (dictionary != null)
        {
            foreach (var kvp in dictionary)
            {
                var value = kvp.Value;

                // Handle arrays/lists - serialize as JSON string (matches React app behavior)
                if (value.ValueKind == JsonValueKind.Array)
                {
                    var array = value.EnumerateArray().ToList();

                    // If empty array, skip
                    if (!array.Any())
                    {
                        continue;
                    }

                    // Serialize all arrays as JSON strings to match React app behavior
                    var arrayJson = JsonSerializer.Serialize(value, JsonOptions);
                    formContent.Add(new StringContent(arrayJson), kvp.Key);
                    continue;
                }

                // Handle objects (serialize as JSON string)
                if (value.ValueKind == JsonValueKind.Object)
                {
                    var objectJson = JsonSerializer.Serialize(value, JsonOptions);
                    formContent.Add(new StringContent(objectJson), kvp.Key);
                    continue;
                }

                // Handle null
                if (value.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                // Handle primitives
                var stringValue = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString() ?? string.Empty,
                    JsonValueKind.Number => value.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => value.ToString()
                };

                formContent.Add(new StringContent(stringValue), kvp.Key);
            }
        }

        // Add files if provided
        if (files != null)
        {
            foreach (var file in files)
            {
                var streamContent = new StreamContent(file.content);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                formContent.Add(streamContent, file.name, file.fileName);
            }
        }

        return await Client.PutAsync(endpoint, formContent);
    }

    // Assertion helpers
    protected static void AssertApiSuccess<T>(ApiResponse<T>? response) where T : class
    {
        response.Should().NotBeNull();
        response!.Succeeded.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Errors.Should().BeEmpty();
    }

    protected static void AssertApiFailure<T>(ApiResponse<T>? response, string? expectedError = null) where T : class
    {
        response.Should().NotBeNull();
        response!.Succeeded.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Errors.Should().NotBeEmpty();

        if (!string.IsNullOrEmpty(expectedError))
        {
            response.Errors.Should().Contain(expectedError);
        }
    }

    // Helper methods for test user creation
    protected async Task EnsureTestUsersExistAsync()
    {
        await EnsureAdminUserExistsAsync();
        await EnsureCustomerUserExistsAsync();
        await EnsureManagerUserExistsAsync();
    }

    protected async Task EnsureAdminUserExistsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // First check if admin user already exists with proper role
        var existingUser = await ExecuteDbContextAsync(async context =>
        {
            var user = context.Users.FirstOrDefault(u => u.Email.Value == "admin@shopilent.com");
            return user;
        });

        if (existingUser != null)
        {
            // User exists, ensure they have admin role
            if (existingUser.Role != UserRole.Admin)
            {
                var changeRoleCommand = new ChangeUserRoleCommandV1
                {
                    UserId = existingUser.Id, NewRole = UserRole.Admin
                };

                await mediator.Send(changeRoleCommand);
            }

            return;
        }

        // Create new admin user using register command
        var registerCommand = new RegisterCommandV1
        {
            Email = "admin@shopilent.com",
            Password = "Admin123!",
            FirstName = "Admin",
            LastName = "User",
            Phone = "",
            IpAddress = "127.0.0.1",
            UserAgent = "Integration Test"
        };

        try
        {
            var registerResult = await mediator.Send(registerCommand);

            if (registerResult.IsSuccess && registerResult.Value != null)
            {
                // Set role to Admin after registration
                var changeRoleCommand = new ChangeUserRoleCommandV1
                {
                    UserId = registerResult.Value.User.Id, NewRole = UserRole.Admin
                };

                await mediator.Send(changeRoleCommand);
            }
        }
        catch (Exception)
        {
            // Ignore if user already exists - that's expected after first test
        }
    }

    protected async Task EnsureCustomerUserExistsAsync()
    {
        var registerRequest = new
        {
            Email = "customer@shopilent.com", Password = "Customer123!", FirstName = "Customer", LastName = "User"
        };

        var response = await PostAsync("v1/auth/register", registerRequest);
        // Ignore if user already exists (409 Conflict) - that's expected after first test
    }

    protected async Task EnsureManagerUserExistsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // First check if manager user already exists with proper role
        var existingUser = await ExecuteDbContextAsync(async context =>
        {
            var user = context.Users.FirstOrDefault(u => u.Email.Value == "manager@shopilent.com");
            return user;
        });

        if (existingUser != null)
        {
            // User exists, ensure they have manager role
            if (existingUser.Role != UserRole.Manager)
            {
                var changeRoleCommand = new ChangeUserRoleCommandV1
                {
                    UserId = existingUser.Id, NewRole = UserRole.Manager
                };

                await mediator.Send(changeRoleCommand);
            }

            return;
        }

        // Create new manager user using register command
        var registerCommand = new RegisterCommandV1
        {
            Email = "manager@shopilent.com",
            Password = "Manager123!",
            FirstName = "Manager",
            LastName = "User",
            Phone = "",
            IpAddress = "127.0.0.1",
            UserAgent = "Integration Test"
        };

        try
        {
            var registerResult = await mediator.Send(registerCommand);

            if (registerResult.IsSuccess && registerResult.Value != null)
            {
                // Set role to Manager after registration
                var changeRoleCommand = new ChangeUserRoleCommandV1
                {
                    UserId = registerResult.Value.User.Id, NewRole = UserRole.Manager
                };

                await mediator.Send(changeRoleCommand);
            }
        }
        catch (Exception)
        {
            // Ignore if user already exists - that's expected after first test
        }
    }

    // Outbox processing helper for deterministic test behavior
    protected async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Factory.Services.CreateScope();
        var outboxService = scope.ServiceProvider
            .GetRequiredService<Shopilent.Application.Abstractions.Outbox.IOutboxService>();
        await outboxService.ProcessMessagesAsync(cancellationToken);
    }

    // Tracks if search indexes have been initialized to avoid redundant setup
    private static bool _searchIndexesInitialized = false;
    private static readonly object _searchInitLock = new object();

    /// <summary>
    /// Initializes Meilisearch indexes with test data. Call this explicitly in tests that require search functionality.
    /// This is NOT called automatically to avoid ~2s overhead on tests that don't use search.
    /// </summary>
    protected async Task InitializeSearchIndexesAsync(bool initializeSearchService = true)
    {
        // Skip if already initialized (one-time setup)
        lock (_searchInitLock)
        {
            if (_searchIndexesInitialized)
            {
                return;
            }

            _searchIndexesInitialized = true;
        }

        try
        {
            using var scope = Factory.Services.CreateScope();
            var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();

            // Initialize basic index structure
            await searchService.InitializeIndexesAsync();

            // Index some basic test products with attributes to configure filterable attributes
            if (initializeSearchService)
                await IndexBasicTestProductsAsync(searchService);
        }
        catch (Exception)
        {
            // Reset flag on failure so next test can retry
            lock (_searchInitLock)
            {
                _searchIndexesInitialized = false;
            }

            // Ignore search initialization errors in tests - tests should still work
            // even if search service is not available
        }
    }

    // Helper to create and index basic test products with attributes
    private async Task IndexBasicTestProductsAsync(ISearchService searchService)
    {
        try
        {
            // Create comprehensive test products with all attribute types used in tests
            var testProducts = new List<ProductSearchDocument>
            {
                new ProductSearchDocument
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product 1",
                    Description = "Test product for search",
                    SKU = "TEST-001",
                    Slug = "test-product-1",
                    BasePrice = 99.99m,
                    IsActive = true,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    HasStock = true,
                    TotalStock = 10,
                    Categories = [],
                    CategorySlugs = [],
                    VariantSKUs = [],
                    PriceRange = new ProductSearchPriceRange { Min = 99.99m, Max = 99.99m },
                    FlatAttributes = CreateComprehensiveTestAttributes()
                }
            };

            await searchService.IndexProductsAsync(testProducts);
        }
        catch (Exception)
        {
            // Ignore if test product indexing fails
        }
    }

    // Helper to create comprehensive test attributes covering all test scenarios
    private static Dictionary<string, string[]> CreateComprehensiveTestAttributes()
    {
        var attributes = new Dictionary<string, string[]>
        {
            // Basic attributes from test data
            ["attr-color"] = new[] { "red", "blue", "silver", "black", "green" },
            ["attr-size"] = new[] { "small", "medium", "large" },
            ["attr-brand"] = new[] { "apple", "samsung", "google", "dell" },
            ["attr-material"] = new[] { "metal", "plastic", "glass" },

            // Computer-specific attributes
            ["attr-screen_size"] = new[] { "13", "15" }
        };

        // Add maximum test attributes (attribute_1 to attribute_10)
        for (int i = 1; i <= 10; i++)
        {
            attributes[$"attr-attribute_{i}"] = new[] { $"value_{i}_1", $"value_{i}_2" };
        }

        // Add complex filter attributes (attr_1 to attr_5)
        for (int i = 1; i <= 5; i++)
        {
            attributes[$"attr-attr_{i}"] = Enumerable.Range(1, 3).Select(j => $"value_{i}_{j}").ToArray();
        }

        return attributes;
    }

    // Helper classes for common responses
    public class LoginResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
