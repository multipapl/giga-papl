using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed record RenderPreviewLoadResult(BitmapSource? ImageSource, string StatusText);

public sealed class RenderPreviewLoader
{
    private const double ExrDisplayGamma = 1.0 / 2.2;
    private const uint MaxPreviewHeight = 360;
    private const uint MaxPreviewWidth = 640;

    public Task<RenderPreviewLoadResult> LoadAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Load(imagePath), cancellationToken);
    }

    private static RenderPreviewLoadResult Load(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return new RenderPreviewLoadResult(null, "Preview will appear after the first saved frame.");
        }

        if (!File.Exists(imagePath))
        {
            return new RenderPreviewLoadResult(null, "Preview file was not found on disk.");
        }

        Exception? lastError = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var image = new MagickImage(imagePath);
                image.AutoOrient();

                if (IsExr(imagePath))
                {
                    // EXR frames are usually linear/HDR, so compress the range for a readable UI preview.
                    image.AutoLevel(Channels.All);
                    image.GammaCorrect(ExrDisplayGamma, Channels.All);
                }

                ResizeForPreview(image);

                var bitmap = CreateBitmap(image.ToByteArray(MagickFormat.Png));
                return new RenderPreviewLoadResult(bitmap, $"Updated from {Path.GetFileName(imagePath)}");
            }
            catch (Exception ex) when (attempt < 2)
            {
                lastError = ex;
                Thread.Sleep(150);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        return new RenderPreviewLoadResult(null, $"Preview unavailable: {lastError?.Message ?? "Unknown error"}");
    }

    private static BitmapSource CreateBitmap(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }

    private static bool IsExr(string imagePath)
    {
        return string.Equals(Path.GetExtension(imagePath), ".exr", StringComparison.OrdinalIgnoreCase);
    }

    private static void ResizeForPreview(MagickImage image)
    {
        if (image.Width <= MaxPreviewWidth && image.Height <= MaxPreviewHeight)
        {
            return;
        }

        var widthScale = MaxPreviewWidth / (double)image.Width;
        var heightScale = MaxPreviewHeight / (double)image.Height;
        var scale = Math.Min(widthScale, heightScale);

        var targetWidth = Math.Max(1u, (uint)Math.Round(image.Width * scale));
        var targetHeight = Math.Max(1u, (uint)Math.Round(image.Height * scale));
        image.Resize(targetWidth, targetHeight);
    }
}
