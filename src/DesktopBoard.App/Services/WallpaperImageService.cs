using System.Runtime.InteropServices.WindowsRuntime;
using DesktopBoard.Core.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace DesktopBoard.App.Services;

/// <summary>
/// Decodes the wallpaper once at a modest size and applies a cheap CPU box blur (three
/// passes approximate a Gaussian). A pre-blurred bitmap costs nothing at render time,
/// which matters for an app that lives on the desktop all day.
/// </summary>
public sealed class WallpaperImageService
{
    public sealed record DecodedImage(byte[] Bgra, int Width, int Height);

    /// <summary>Max width of the decoded bitmap. The desktop is upscaled from this; blur hides the difference.</summary>
    private const int TargetWidth = 1280;

    public async Task<DecodedImage?> LoadAsync(string path, double blur)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var ras = stream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(ras);

            var scale = Math.Min(1.0, (double)TargetWidth / decoder.PixelWidth);
            var w = Math.Max(1, (int)Math.Round(decoder.PixelWidth * scale));
            var h = Math.Max(1, (int)Math.Round(decoder.PixelHeight * scale));

            var transform = new BitmapTransform { ScaledWidth = (uint)w, ScaledHeight = (uint)h, InterpolationMode = BitmapInterpolationMode.Fant };
            var data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
                ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
            var pixels = data.DetachPixelData();

            var radius = (int)Math.Round(Math.Clamp(blur, 0, 1) * 18);
            if (radius > 0)
                await Task.Run(() => BoxBlur(pixels, w, h, radius, passes: 3));

            return new DecodedImage(pixels, w, h);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Wallpaper load failed for '{path}': {ex.Message}");
            return null;
        }
    }

    /// <summary>Must be called on the UI thread.</summary>
    public static WriteableBitmap ToBitmap(DecodedImage image)
    {
        var bmp = new WriteableBitmap(image.Width, image.Height);
        using (var s = bmp.PixelBuffer.AsStream())
        {
            s.Write(image.Bgra, 0, image.Bgra.Length);
        }
        bmp.Invalidate();
        return bmp;
    }

    /// <summary>In-place separable box blur on premultiplied BGRA.</summary>
    private static void BoxBlur(byte[] px, int w, int h, int r, int passes)
    {
        var tmp = new byte[px.Length];
        for (var p = 0; p < passes; p++)
        {
            BlurHorizontal(px, tmp, w, h, r);
            BlurVertical(tmp, px, w, h, r);
        }
    }

    private static void BlurHorizontal(byte[] src, byte[] dst, int w, int h, int r)
    {
        var div = 2 * r + 1;
        Parallel.For(0, h, y =>
        {
            var row = y * w * 4;
            var sum = new int[4];
            for (var x = -r; x <= r; x++)
            {
                var cx = Math.Clamp(x, 0, w - 1);
                for (var c = 0; c < 4; c++) sum[c] += src[row + cx * 4 + c];
            }
            for (var x = 0; x < w; x++)
            {
                for (var c = 0; c < 4; c++) dst[row + x * 4 + c] = (byte)(sum[c] / div);
                var addX = Math.Clamp(x + r + 1, 0, w - 1);
                var subX = Math.Clamp(x - r, 0, w - 1);
                for (var c = 0; c < 4; c++) sum[c] += src[row + addX * 4 + c] - src[row + subX * 4 + c];
            }
        });
    }

    private static void BlurVertical(byte[] src, byte[] dst, int w, int h, int r)
    {
        var div = 2 * r + 1;
        var stride = w * 4;
        Parallel.For(0, w, x =>
        {
            var col = x * 4;
            var sum = new int[4];
            for (var y = -r; y <= r; y++)
            {
                var cy = Math.Clamp(y, 0, h - 1);
                for (var c = 0; c < 4; c++) sum[c] += src[cy * stride + col + c];
            }
            for (var y = 0; y < h; y++)
            {
                for (var c = 0; c < 4; c++) dst[y * stride + col + c] = (byte)(sum[c] / div);
                var addY = Math.Clamp(y + r + 1, 0, h - 1);
                var subY = Math.Clamp(y - r, 0, h - 1);
                for (var c = 0; c < 4; c++) sum[c] += src[addY * stride + col + c] - src[subY * stride + col + c];
            }
        });
    }
}
