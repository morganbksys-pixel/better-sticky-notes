using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace BetterStickyNotes;

public static class ScreenCapture
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();

    public static Rectangle SelectScreen(Rectangle window, IReadOnlyList<Rectangle> screens)
    {
        if (screens.Count == 0) throw new InvalidOperationException("Aucun écran disponible.");
        return screens.OrderByDescending(screen =>
        {
            var overlap = Rectangle.Intersect(window, screen);
            return (long)Math.Max(0, overlap.Width) * Math.Max(0, overlap.Height);
        }).ThenBy(screen =>
        {
            var dx = Math.Max(0L, Math.Max((long)screen.Left - window.Right, (long)window.Left - screen.Right));
            var dy = Math.Max(0L, Math.Max((long)screen.Top - window.Bottom, (long)window.Top - screen.Bottom));
            return dx * dx + dy * dy;
        }).First();
    }

    // The optional pixel reader lets tests exercise restoration without capturing the desktop.
    public static async Task<BitmapSource> CaptureAsync(Window source, IEnumerable<Window> windows, Func<Rectangle, BitmapSource>? readPixels = null)
    {
        var handle = new WindowInteropHelper(source).Handle;
        if (!source.IsVisible || handle == IntPtr.Zero || !GetWindowRect(handle, out var rect))
            throw new InvalidOperationException("La note doit être affichée pour capturer son écran.");
        var screen = SelectScreen(Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom), Forms.Screen.AllScreens.Select(s => s.Bounds).ToArray());
        var masked = windows.Where(w => w.IsVisible).Distinct().Where(w =>
        {
            var native = new WindowInteropHelper(w).Handle;
            return native != IntPtr.Zero && GetWindowRect(native, out var bounds) && screen.IntersectsWith(Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
        }).Select(w => (Window: w, w.Opacity, w.IsHitTestVisible)).ToList();
        try
        {
            foreach (var state in masked) { state.Window.Opacity = 0; state.Window.IsHitTestVisible = false; }
            await source.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(120);
            return await source.Dispatcher.InvokeAsync(() =>
            {
                DwmFlush();
                return (readPixels ?? ReadPixels)(screen);
            });
        }
        finally
        {
            await source.Dispatcher.InvokeAsync(() =>
            {
                foreach (var state in masked)
                {
                    state.Window.Opacity = state.Opacity;
                    state.Window.IsHitTestVisible = state.IsHitTestVisible;
                }
            });
        }
    }

    private static BitmapSource ReadPixels(Rectangle screen)
    {
        using var bitmap = new Bitmap(screen.Width, screen.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(screen.Left, screen.Top, 0, 0, screen.Size, CopyPixelOperation.SourceCopy);
        var handle = bitmap.GetHbitmap();
        try
        {
            var image = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze(); return image;
        }
        finally { DeleteObject(handle); }
    }
}
