using Avalonia;
using Avalonia.LinuxFramebuffer;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Win32;
using System;
using System.Globalization;
using System.Threading;

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
            // Region dirty-rect clipping keeps redraws cheap on the weaker GPUs found
            // in embedded/kiosk hardware (recommended by the Avalonia embedded guide).
            .With(new CompositionOptions
            {
                UseRegionDirtyRectClipping = true,
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
    /// embedded/kiosk devices. Input arrives through libinput independently of the TTY.
    ///   RYTHMBOX_DRM_CARD     - DRM device node (default: auto-detect, e.g. /dev/dri/card0)
    ///   RYTHMBOX_DRM_SCALE    - output scaling factor (default 1.0)
    ///   RYTHMBOX_DRM_ROTATION - screen rotation in degrees: 0, 90, 180, 270 (default 0)
    /// </summary>
    private static int StartLinuxDrm(string[] args)
    {
        // A null card lets Avalonia auto-detect the primary connected output, which is
        // more robust across devices than hard-coding card0. Override only if needed.
        var card = Environment.GetEnvironmentVariable("RYTHMBOX_DRM_CARD");
        if (string.IsNullOrWhiteSpace(card))
        {
            card = null;
        }

        var options = new DrmOutputOptions
        {
            Scaling = ParseScaling(),
            Orientation = ParseOrientation(),
        };

        // On a TTY the kernel console keeps a blinking cursor and echoes keystrokes to
        // the shell. Hide the cursor and drain console input on a background thread so
        // nothing bleeds through the app. Avalonia still receives keys via libinput.
        SilenceConsole();

        Console.Error.WriteLine(
            $"[DRM] Starting on {card ?? "auto-detect"} " +
            $"(scaling {options.Scaling.ToString(CultureInfo.InvariantCulture)}, orientation {options.Orientation})");

        return BuildAvaloniaApp().StartLinuxDrm(args, card, options: options);
    }

    private static double ParseScaling()
    {
        var scaleText = Environment.GetEnvironmentVariable("RYTHMBOX_DRM_SCALE");
        if (!string.IsNullOrWhiteSpace(scaleText)
            && double.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return 1.0;
    }

    private static SurfaceOrientation ParseOrientation() =>
        Environment.GetEnvironmentVariable("RYTHMBOX_DRM_ROTATION")?.Trim() switch
        {
            "90" => SurfaceOrientation.Rotation90,
            "180" => SurfaceOrientation.Rotation180,
            "270" => SurfaceOrientation.Rotation270,
            _ => SurfaceOrientation.Rotation0,
        };

    private static void SilenceConsole()
    {
        try
        {
            Console.CursorVisible = false;
        }
        catch
        {
            // Not attached to a real console (e.g. output redirected); nothing to hide.
        }

        if (Console.IsInputRedirected)
        {
            return;
        }

        var drain = new Thread(() =>
        {
            try
            {
                while (true)
                {
                    Console.ReadKey(intercept: true);
                }
            }
            catch
            {
                // No console input available; stop draining.
            }
        })
        {
            IsBackground = true,
            Name = "ConsoleSilencer",
        };
        drain.Start();
    }
}
