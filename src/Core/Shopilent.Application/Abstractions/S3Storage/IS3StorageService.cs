using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Abstractions.S3Storage;

public interface IS3StorageService
{
    Task<Result<string>> UploadFileAsync(
        string key,
        Stream fileStream,
        string contentType,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task<Result<Stream>> DownloadFileAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteFileAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> FileExistsAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<string>> GetPresignedUrlAsync(
        string key,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<S3ObjectInfo>>> ListFilesAsync(
        string? prefix = null,
        CancellationToken cancellationToken = default);
}
