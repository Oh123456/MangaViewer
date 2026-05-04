using SixLabors.ImageSharp.PixelFormats;

namespace Viewer;

public static class ImageLoader
{
    public static Bitmap LoadBitmapCopy(string path)
    {
        try
        {
            return LoadWithSystemDrawing(path);
        }
        catch (Exception systemDrawingException)
        {
            try
            {
                return LoadWithImageSharp(path);
            }
            catch (Exception imageSharpException)
            {
                throw new ImageLoadException(
                    $"System.Drawing 실패: {systemDrawingException.Message} / ImageSharp 실패: {imageSharpException.Message}",
                    imageSharpException);
            }
        }
    }

    public static ImageLoadFailure CreateFailure(string path, Exception exception)
    {
        return new ImageLoadFailure
        {
            Path = path,
            Extension = Path.GetExtension(path),
            FileSize = GetFileSize(path),
            MagicBytes = GetMagicBytes(path),
            Message = exception.Message
        };
    }

    public static ImageLoadFailure CreateFailure(string path, string message)
    {
        return new ImageLoadFailure
        {
            Path = path,
            Extension = Path.GetExtension(path),
            FileSize = GetFileSize(path),
            MagicBytes = GetMagicBytes(path),
            Message = message
        };
    }

    public static void LogFailure(string logNamePrefix, ImageLoadFailure failure)
    {
        try
        {
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, $"{logNamePrefix}_{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllLines(logPath,
            [
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {failure.Path} / ext={failure.Extension} / size={failure.FileSize} / magic={failure.MagicBytes} / {failure.Message}"
            ]);
        }
        catch
        {
        }
    }

    private static long GetFileSize(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : -1;
        }
        catch
        {
            return -1;
        }
    }

    private static Bitmap LoadWithSystemDrawing(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sourceImage = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
        return new Bitmap(sourceImage);
    }

    private static Bitmap LoadWithImageSharp(string path)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(path);
        var bitmap = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(rectangle, System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);

        try
        {
            var stride = bitmapData.Stride;
            var rowBytes = image.Width * 4;
            var buffer = new byte[rowBytes];
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < image.Height; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);
                    for (var x = 0; x < pixelRow.Length; x++)
                    {
                        var pixel = pixelRow[x];
                        var bufferIndex = x * 4;
                        buffer[bufferIndex] = pixel.B;
                        buffer[bufferIndex + 1] = pixel.G;
                        buffer[bufferIndex + 2] = pixel.R;
                        buffer[bufferIndex + 3] = pixel.A;
                    }

                    System.Runtime.InteropServices.Marshal.Copy(buffer, 0, bitmapData.Scan0 + y * stride, rowBytes);
                }
            });
        }
        catch
        {
            bitmap.UnlockBits(bitmapData);
            bitmap.Dispose();
            throw;
        }

        bitmap.UnlockBits(bitmapData);
        return bitmap;
    }

    private static string GetMagicBytes(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return "missing";
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = (int)Math.Min(16, stream.Length);
            var buffer = new byte[length];
            var read = stream.Read(buffer, 0, buffer.Length);
            return Convert.ToHexString(buffer.AsSpan(0, read));
        }
        catch (Exception exception)
        {
            return $"error:{exception.Message}";
        }
    }
}

public sealed class ImageLoadException(string message, Exception innerException) : Exception(message, innerException)
{
}

public sealed class ImageLoadFailure
{
    public string Path { get; init; } = "";
    public string Extension { get; init; } = "";
    public long FileSize { get; init; }
    public string MagicBytes { get; init; } = "";
    public string Message { get; init; } = "";
}
