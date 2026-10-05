namespace EatWeb.Services.Storage;

public interface IStorageService
{
    bool EstaConfigurado { get; }

    Task SubirAsync(
        Stream contenido,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default);

    Task EliminarAsync(
        string? objectKey,
        CancellationToken cancellationToken = default);

    string? ObtenerUrlPublica(string? objectKey);
}
