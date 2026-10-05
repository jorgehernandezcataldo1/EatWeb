using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace EatWeb.Services.Storage;

public sealed class S3StorageService : IStorageService, IDisposable
{
    private readonly StorageOptions _options;
    private readonly AmazonS3Client? _client;

    public S3StorageService(IOptions<StorageOptions> options)
    {
        _options = options.Value;

        if (!string.Equals(_options.Provider, "S3", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(_options.ServiceUrl) ||
            string.IsNullOrWhiteSpace(_options.Region) ||
            string.IsNullOrWhiteSpace(_options.AccessKey) ||
            string.IsNullOrWhiteSpace(_options.SecretKey) ||
            string.IsNullOrWhiteSpace(_options.Bucket))
        {
            return;
        }

        var config = new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl.TrimEnd('/'),
            ForcePathStyle = true,
            AuthenticationRegion = _options.Region
        };

        _client = new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            config);
    }

    public bool EstaConfigurado => _client is not null;

    public async Task SubirAsync(
        Stream contenido,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var client = RequerirCliente();

        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = NormalizarKey(objectKey),
            InputStream = contenido,
            ContentType = contentType,
            AutoCloseStream = false
        };

        request.Headers.CacheControl = "public, max-age=31536000, immutable";

        await client.PutObjectAsync(request, cancellationToken);
    }

    public async Task EliminarAsync(
        string? objectKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || EsUrlAbsoluta(objectKey))
            return;

        var client = RequerirCliente();

        await client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _options.Bucket,
            Key = NormalizarKey(objectKey)
        }, cancellationToken);
    }

    public string? ObtenerUrlPublica(string? objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return null;

        if (EsUrlAbsoluta(objectKey))
            return objectKey;

        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            return null;

        var keyCodificada = string.Join(
            "/",
            NormalizarKey(objectKey)
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{keyCodificada}";
    }

    private AmazonS3Client RequerirCliente() =>
        _client ?? throw new InvalidOperationException(
            "El almacenamiento no está configurado. Configura Storage:ServiceUrl, Region, AccessKey, SecretKey y Bucket.");

    private static string NormalizarKey(string objectKey) =>
        objectKey.Replace('\\', '/').TrimStart('/');

    private static bool EsUrlAbsoluta(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public void Dispose()
    {
        _client?.Dispose();
    }
}
