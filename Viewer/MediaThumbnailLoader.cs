using System.Runtime.InteropServices;

namespace Viewer;

public static class MediaThumbnailLoader
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".gif",
        ".webp"
    };

    public static Image LoadThumbnailCopy(string path, Size size)
    {
        if (ImageExtensions.Contains(Path.GetExtension(path)))
        {
            return ImageLoader.LoadBitmapCopy(path);
        }

        return LoadShellThumbnail(path, size);
    }

    private static Image LoadShellThumbnail(string path, Size size)
    {
        var factoryId = typeof(IShellItemImageFactory).GUID;
        SHCreateItemFromParsingName(path, IntPtr.Zero, ref factoryId, out var factory);
        factory.GetImage(new ShellSize(size.Width, size.Height), ShellImageFlags.ThumbnailOnly | ShellImageFlags.BiggerSizeOk, out var bitmapHandle);
        if (bitmapHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Shell thumbnail was empty.");
        }

        try
        {
            using var shellBitmap = Image.FromHbitmap(bitmapHandle);
            return new Bitmap(shellBitmap);
        }
        finally
        {
            DeleteObject(bitmapHandle);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr objectHandle);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(ShellSize size, ShellImageFlags flags, out IntPtr bitmapHandle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ShellSize
    {
        public ShellSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
    }

    [Flags]
    private enum ShellImageFlags
    {
        BiggerSizeOk = 0x00000001,
        ThumbnailOnly = 0x00000008
    }
}
