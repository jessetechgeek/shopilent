using System.Text;
using Microsoft.Extensions.Options;
using Shopilent.Application.Abstractions.S3Storage;
using Shopilent.Domain.Common.Errors;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.S3ObjectStorage.Settings;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.S3ObjectStorage.Services;

[Collection("IntegrationTests")]
public class S3StorageServiceTests : IntegrationTestBase
{
    private IS3StorageService _s3StorageService = null!;
    private S3Settings _s3Settings = null!;
    private readonly string _testBucket = "test-bucket";

    public S3StorageServiceTests(IntegrationTestFixture integrationTestFixture) : base(integrationTestFixture)
    {
    }

    protected override async Task InitializeTestServices()
    {
        _s3StorageService = GetService<IS3StorageService>();
        _s3Settings = GetService<IOptions<S3Settings>>().Value;

        // Ensure test bucket exists for S3 operations
        await EnsureTestBucketExists();
    }

    private async Task EnsureTestBucketExists()
    {
        // Create the test bucket if it doesn't exist
        try
        {
            var existsResult = await _s3StorageService.ListFilesAsync();
            if (existsResult.IsFailure)
            {
                // Bucket might not exist, try to create it through the AWS S3 client
                var s3Client = GetService<Amazon.S3.IAmazonS3>();
                await s3Client.PutBucketAsync(_testBucket);
            }
        }
        catch
        {
            // If we can't create the bucket, the tests will fail with more descriptive errors
        }
    }

    [Fact]
    public async Task UploadFileAsync_WithValidFile_ShouldReturnSuccessWithUrl()
    {
        // Arrange
        var key = $"test-files/{Guid.NewGuid()}.txt";
        var content = "Hello, S3 Integration Test!";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(contentBytes);
        var contentType = "text/plain";

        // Act
        var result = await _s3StorageService.UploadFileAsync(key, stream, contentType);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        result.Value.Should().Contain(key);
    }

    [Fact]
    public async Task UploadFileAsync_WithMetadata_ShouldReturnSuccessWithUrl()
    {
        // Arrange
        var key = $"test-files/{Guid.NewGuid()}.txt";
        var content = "Hello, S3 with metadata!";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(contentBytes);
        var contentType = "text/plain";
        var metadata = new Dictionary<string, string>
        {
            { "test-key", "test-value" }, { "uploaded-by", "integration-test" }
        };

        // Act
        var result = await _s3StorageService.UploadFileAsync(key, stream, contentType, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        result.Value.Should().Contain(key);
    }

    [Fact]
    public async Task UploadFileAsync_WithEmptyStream_ShouldReturnSuccess()
    {
        // Arrange
        var key = $"test-files/{Guid.NewGuid()}.txt";
        using var stream = new MemoryStream();
        var contentType = "text/plain";

        // Act
        var result = await _s3StorageService.UploadFileAsync(key, stream, contentType);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        result.Value.Should().Contain(key);
    }

    [Fact]
    public async Task DownloadFileAsync_WithExistingFile_ShouldReturnStreamWithContent()
    {
        // Arrange
        var key = $"test-files/{Guid.NewGuid()}.txt";
        var content = "Hello, Download Test!";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        using var uploadStream = new MemoryStream(contentBytes);

        // First upload the file
        var uploadResult = await _s3StorageService.UploadFileAsync(key, uploadStream, "text/plain");
        if (uploadResult.IsFailure)
        {
            throw new InvalidOperationException($"Upload failed: {uploadResult.Error.Message}");
        }

        uploadResult.IsSuccess.Should().BeTrue();

        // Act
        var downloadResult = await _s3StorageService.DownloadFileAsync(key);

        // Assert
        downloadResult.IsSuccess.Should().BeTrue();
        downloadResult.Value.Should().NotBeNull();

        using var reader = new StreamReader(downloadResult.Value);
        var downloadedContent = await reader.ReadToEndAsync();
        downloadedContent.Should().Be(content);
    }

    [Fact]
    public async Task DownloadFileAsync_WithNonExistentFile_ShouldReturnNotFoundError()
    {
        // Arrange
        var key = $"non-existent/{Guid.NewGuid()}.txt";

        // Act
        var result = await _s3StorageService.DownloadFileAsync(key);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Message.Should().Be("File not found");
    }

    [Fact]
    public async Task DeleteFileAsync_WithExistingFile_ShouldReturnSuccess()
    {
        // Arrange
        var key = $"test-files/{Guid.NewGuid()}.txt";
        var content = "Hello, Delete Test!";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(contentBytes);

        // First upload the file
        var uploadResult = await _s3StorageService.UploadFileAsync(key, stream, "text/plain");
        uploadResult.IsSuccess.Should().BeTrue();

        // Act
        var deleteResult = await _s3StorageService.DeleteFileAsync(key);

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();
        deleteResult.Value.Should().BeTrue();

        // Verify file is actually deleted
        var existsResult = await _s3StorageService.FileExistsAsync(key);
        existsResult.IsSuccess.Should().BeTrue();
        existsResult.Value.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFileAsync_WithNonExistentFile_ShouldReturnSuccess()
    {
        // Arrange
        var key = $"non-existent/{Guid.NewGuid()}.txt";

        // Act
        var result = await _s3StorageService.DeleteFileAsync(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task FileExistsAsync_WithExistingFile_ShouldReturnTrue()
    {
        // Arrange
        var key = $"test-files/{Guid.NewGuid()}.txt";
        var content = "Hello, Exists Test!";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(contentBytes);

        // First upload the file
        var uploadResult = await _s3StorageService.UploadFileAsync(key, stream, "text/plain");
        uploadResult.IsSuccess.Should().BeTrue();

        // Act
        var result = await _s3StorageService.FileExistsAsync(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task FileExistsAsync_WithNonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var key = $"non-existent/{Guid.NewGuid()}.txt";

        // Act
        var result = await _s3StorageService.FileExistsAsync(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task GetPresignedUrlAsync_WithValidFile_ShouldReturnValidUrl()
    {
        // Arrange
        var key = $"test-files/{Guid.NewGuid()}.txt";
        var content = "Hello, Presigned URL Test!";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(contentBytes);
        var expiry = TimeSpan.FromHours(1);

        // First upload the file
        var uploadResult = await _s3StorageService.UploadFileAsync(key, stream, "text/plain");
        uploadResult.IsSuccess.Should().BeTrue();

        // Act
        var result = await _s3StorageService.GetPresignedUrlAsync(key, expiry);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        result.Value.Should().Contain(key);

        // For MinIO in integration tests, URL should be modified to localhost
        if (_s3Settings.Provider == "MinIO")
        {
            result.Value.Should().Contain("localhost:9858");
        }
    }

    [Fact]
    public async Task GetPresignedUrlAsync_WithNonExistentFile_ShouldReturnValidUrl()
    {
        // Arrange
        var key = $"non-existent/{Guid.NewGuid()}.txt";
        var expiry = TimeSpan.FromHours(1);

        // Act
        var result = await _s3StorageService.GetPresignedUrlAsync(key, expiry);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        result.Value.Should().Contain(key);
    }

    [Fact]
    public async Task ListFilesAsync_WithEmptyBucket_ShouldReturnEmptyList()
    {
        // Arrange
        var prefix = $"empty-test/{Guid.NewGuid()}";

        // Act
        var result = await _s3StorageService.ListFilesAsync(prefix);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ListFilesAsync_WithMultipleFiles_ShouldReturnAllFiles()
    {
        // Arrange
        var prefix = $"list-test/{Guid.NewGuid()}";
        var files = new List<string> { $"{prefix}/file1.txt", $"{prefix}/file2.txt", $"{prefix}/subfolder/file3.txt" };

        // Upload test files
        foreach (var file in files)
        {
            var content = $"Content for {file}";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(contentBytes);
            var uploadResult = await _s3StorageService.UploadFileAsync(file, stream, "text/plain");
            uploadResult.IsSuccess.Should().BeTrue();
        }

        // Act
        var result = await _s3StorageService.ListFilesAsync(prefix);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);

        var fileKeys = result.Value.Select(f => f.Key).ToList();
        fileKeys.Should().Contain(files);

        // Verify object properties
        foreach (var objectInfo in result.Value)
        {
            objectInfo.Key.Should().NotBeNullOrEmpty();
            objectInfo.Size.Should().BeGreaterThan(0);
            objectInfo.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromHours(6));
            objectInfo.ETag.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task ListFilesAsync_WithPrefix_ShouldReturnFilteredFiles()
    {
        // Arrange
        var basePrefix = $"prefix-test/{Guid.NewGuid()}";
        var targetPrefix = $"{basePrefix}/target";
        var otherPrefix = $"{basePrefix}/other";

        var targetFiles = new List<string> { $"{targetPrefix}/file1.txt", $"{targetPrefix}/file2.txt" };

        var otherFiles = new List<string> { $"{otherPrefix}/file3.txt" };

        // Upload all files
        foreach (var file in targetFiles.Concat(otherFiles))
        {
            var content = $"Content for {file}";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(contentBytes);
            var uploadResult = await _s3StorageService.UploadFileAsync(file, stream, "text/plain");
            uploadResult.IsSuccess.Should().BeTrue();
        }

        // Act
        var result = await _s3StorageService.ListFilesAsync(targetPrefix);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);

        var fileKeys = result.Value.Select(f => f.Key).ToList();
        fileKeys.Should().Contain(targetFiles);
        fileKeys.Should().NotContain(otherFiles);
    }

    [Fact]
    public async Task ListFilesAsync_WithoutPrefix_ShouldReturnAllFilesInBucket()
    {
        // Arrange
        var prefix = $"no-prefix-test/{Guid.NewGuid()}";
        var files = new List<string> { $"{prefix}/file1.txt", $"{prefix}/file2.txt" };

        // Upload test files
        foreach (var file in files)
        {
            var content = $"Content for {file}";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(contentBytes);
            var uploadResult = await _s3StorageService.UploadFileAsync(file, stream, "text/plain");
            uploadResult.IsSuccess.Should().BeTrue();
        }

        // Act
        var result = await _s3StorageService.ListFilesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().NotBeEmpty();

        // Should contain our uploaded files plus potentially others from other tests
        var fileKeys = result.Value.Select(f => f.Key).ToList();
        foreach (var file in files)
        {
            fileKeys.Should().Contain(file);
        }
    }

    [Theory]
    [InlineData("MinIO")]
    [InlineData("DigitalOcean")]
    [InlineData("Backblaze")]
    [InlineData("AWS")]
    public async Task UploadFileAsync_WithDifferentProviders_ShouldGenerateCorrectUrl(string provider)
    {
        // Arrange
        var key = $"provider-test/{Guid.NewGuid()}.txt";
        var content = $"Hello, {provider} test!";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(contentBytes);

        // Temporarily modify the provider setting
        var originalProvider = _s3Settings.Provider;
        var originalServiceUrl = _s3Settings.ServiceUrl;

        try
        {
            // Use reflection to modify the settings for this test
            var providerProperty = typeof(S3Settings).GetProperty(nameof(S3Settings.Provider));
            var serviceUrlProperty = typeof(S3Settings).GetProperty(nameof(S3Settings.ServiceUrl));

            providerProperty?.SetValue(_s3Settings, provider);

            // Set appropriate service URLs for different providers
            switch (provider)
            {
                case "DigitalOcean":
                    serviceUrlProperty?.SetValue(_s3Settings, "https://nyc3.digitaloceanspaces.com");
                    break;
                case "Backblaze":
                    serviceUrlProperty?.SetValue(_s3Settings, "https://s3.us-west-002.backblazeb2.com");
                    break;
                case "AWS":
                    serviceUrlProperty?.SetValue(_s3Settings, "https://s3.amazonaws.com");
                    break;
                default: // MinIO
                    serviceUrlProperty?.SetValue(_s3Settings,
                        IntegrationTestFixture.MinioContainer.GetConnectionString());
                    break;
            }

            // Act
            var result = await _s3StorageService.UploadFileAsync(key, stream, "text/plain");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNullOrEmpty();
            result.Value.Should().Contain(key);

            // Verify URL format based on provider
            switch (provider)
            {
                case "DigitalOcean":
                    result.Value.Should().Contain($"{_testBucket}.nyc3.digitaloceanspaces.com");
                    break;
                case "Backblaze":
                    result.Value.Should().Contain("s3.us-west-002.backblazeb2.com");
                    break;
                default:
                    result.Value.Should().Contain(_s3Settings.ServiceUrl);
                    break;
            }
        }
        finally
        {
            // Restore original settings
            var providerProperty = typeof(S3Settings).GetProperty(nameof(S3Settings.Provider));
            var serviceUrlProperty = typeof(S3Settings).GetProperty(nameof(S3Settings.ServiceUrl));
            providerProperty?.SetValue(_s3Settings, originalProvider);
            serviceUrlProperty?.SetValue(_s3Settings, originalServiceUrl);
        }
    }
}
