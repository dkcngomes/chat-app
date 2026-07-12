using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace backend.Services;

/// <summary>
/// Handles file uploads to either Cloudflare R2 or local disk.
/// If R2 is configured in appsettings, files go to R2; otherwise local wwwroot/uploads/.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Upload a file and return (fileName, publicUrl).</summary>
    Task<(string fileName, string url)> UploadAsync(Stream fileStream, string fileName);
}

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;

    public LocalFileStorageService(IWebHostEnvironment env) => _env = env;

    public async Task<(string fileName, string url)> UploadAsync(Stream fileStream, string fileName)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var uniqueName = $"{Guid.NewGuid():N}-{fileName}";
        var filePath = Path.Combine(uploadsDir, uniqueName);

        await using var fs = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(fs);

        return (uniqueName, $"/uploads/{uniqueName}");
    }
}

public class R2StorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _publicUrlBase;
    private readonly ILogger<R2StorageService> _logger;

    public R2StorageService(IConfiguration config, ILogger<R2StorageService> logger)
    {
        _logger = logger;
        _bucketName = config["CloudStorage:R2:BucketName"] ?? throw new InvalidOperationException("CloudStorage:R2:BucketName is required");

        var accountId = config["CloudStorage:R2:AccountId"]
            ?? throw new InvalidOperationException("CloudStorage:R2:AccountId is required");
        var accessKey = config["CloudStorage:R2:AccessKey"]
            ?? throw new InvalidOperationException("CloudStorage:R2:AccessKey is required");
        var secretKey = config["CloudStorage:R2:SecretKey"]
            ?? throw new InvalidOperationException("CloudStorage:R2:SecretKey is required");

        // Public URL (for serving images). If not set, construct from bucket & account.
        _publicUrlBase = config["CloudStorage:R2:PublicUrl"]
            ?? $"https://{_bucketName}.{accountId}.r2.dev";

        var creds = new BasicAWSCredentials(accessKey, secretKey);
        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.USEast1, // R2 ignores region, use any
            ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true,
        };

        _s3Client = new AmazonS3Client(creds, s3Config);
        _logger.LogInformation("R2 storage configured. Bucket: {Bucket}, Public URL: {PublicUrl}",
            _bucketName, _publicUrlBase);
    }

    public async Task<(string fileName, string url)> UploadAsync(Stream fileStream, string fileName)
    {
        var uniqueName = $"{Guid.NewGuid():N}-{fileName}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = uniqueName,
            InputStream = fileStream,
            ContentType = GetContentType(fileName),
            DisablePayloadSigning = true, // Required for R2
        };

        await _s3Client.PutObjectAsync(request);
        _logger.LogDebug("Uploaded {Key} to R2 bucket {Bucket}", uniqueName, _bucketName);

        var publicUrl = $"{_publicUrlBase.TrimEnd('/')}/{uniqueName}";
        return (uniqueName, publicUrl);
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream",
        };
    }
}
