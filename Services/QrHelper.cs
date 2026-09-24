using QRCoder;

namespace EatWeb.Services;

public static class QrHelper
{
    /// <summary>Código aleatorio de 32 caracteres hex, seguro para URLs.</summary>
    public static string GenerarCodigo() => Guid.NewGuid().ToString("N");

    /// <summary>Genera la imagen PNG del QR para una URL.</summary>
    public static byte[] GenerarPng(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(20);
    }
}