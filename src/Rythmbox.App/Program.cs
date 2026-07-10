using Avalonia;
using Avalonia.Media;
using Avalonia.Win32;
using System;
using System.Globalization;

namespace Rythmbox.App;

sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Console.Error.WriteLine($"[FATAL] {e.ExceptionObject}");

        try
        {
            if (ShouldUseLinuxDrm(args))
            {
                return StartLinuxDrm(args);
            }

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL] {ex}");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = Localization.LocalizationService.BarlowAnuphanFont,
            })
            .LogToTrace();

        if (OperatingSystem.IsWindows())
        {
            builder = builder.With(new Win32PlatformOptions
            {
                RenderingMode =
                [
                    Win32RenderingMode.AngleEgl,
                    Win32RenderingMode.Wgl,
                    Win32RenderingMode.Software,
                ],
            });
        }

        return builder;
    }

    /// <summary>
    /// DRM/KMS is the direct-rendering path for embedded Linux with no X11/Wayland
    /// display server. Enabled with the <c>--drm</c> argument or <c>RYTHMBOX_DRM=1</c>.
    /// </summary>
    private static bool ShouldUseLinuxDrm(string[] args)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        foreach (var arg in args)
        {
            if (string.Equals(arg, "--drm", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return string.Equals(Environment.GetEnvironmentVariable("RYTHMBOX_DRM"), "1", StringComparison.Ordinal);
    }

    /// <summary>
    /// Renders straight to the Linux framebuffer via DRM/KMS + EGL, replacing X11 on
    /// embedded/kiosk devices. The card node and output scaling are configurable so the
    /// same binary can target different panels.
    ///   RYTHMBOX_DRM_CARD  - DRM device node (default /dev/dri/card0)
    ///   RYTHMBOX_DRM_SCALE - output scaling factor (default 1.0)
    /// </summary>
    private static int StartLinuxDrm(string[] args)
    {
        var card = Environment.GetEnvironmentVariable("RYTHMBOX_DRM_CARD");
        if (string.IsNullOrWhiteSpace(card))
        {
            card = "/dev/dri/card0";
        }

        var scaling = 1.0;
        var scaleText = Environment.GetEnvironmentVariable("RYTHMBOX_DRM_SCALE");
        if (!string.IsNullOrWhiteSpace(scaleText)
            && double.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScale)
            && parsedScale > 0)
        {
            scaling = parsedScale;
        }

        Console.Error.WriteLine($"[DRM] Starting on {card} (scaling {scaling.ToString(CultureInfo.InvariantCulture)})");
        return BuildAvaloniaApp().StartLinuxDrm(args, card, scaling);
    }
}
