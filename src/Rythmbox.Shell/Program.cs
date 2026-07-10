using Avalonia;
using Avalonia.LinuxFramebuffer;
using Avalonia.LinuxFramebuffer.Input.LibInput;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using System.Globalization;
using RythmboxApp = Rythmbox.App;

namespace Rythmbox.Shell;

/// <summary>
/// Entry point for the embedded Linux kiosk. Renders the Rythmbox UI directly on
/// DRM/KMS (no X11/Wayland) and drives input through libinput, which supplies both
/// touchscreen and mouse events. A software cursor is drawn by the UI itself because
/// the framebuffer backend has no hardware/compositor pointer.
/// </summary>


internal static class Program
{
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Console.Error.WriteLine($"[FATAL] {e.ExceptionObject}");

        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine(
                "Rythmbox.Shell is the embedded Linux DRM/KMS host and only runs on Linux. " +
                "Use Rythmbox.App for desktop platforms.");
            return 1;
        }

        try
        {
            return StartEmbedded(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL] {ex}");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<RythmboxApp.App>()
            .UsePlatformDetect()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = RythmboxApp.Localization.LocalizationService.BarlowAnuphanFont,
            })
            // Cheaper redraws on the weaker GPUs found in embedded/kiosk hardware.
            .With(new CompositionOptions
            {
                UseRegionDirtyRectClipping = true,
            })
            .LogToTrace();

    /// <summary>
    /// Renders on DRM/KMS + EGL with a libinput backend (touch + mouse + keyboard).
    ///   RYTHMBOX_DRM_CARD     - DRM device node (default: auto-detect, e.g. /dev/dri/card0)
    ///   RYTHMBOX_DRM_SCALE    - output scaling factor (default 1.0)
    ///   RYTHMBOX_DRM_ROTATION - screen rotation in degrees: 0, 90, 180, 270 (default 0)
    /// </summary>
    private static int StartEmbedded(string[] args)
    {
        // Tell the shared UI it is on the framebuffer so it renders its own cursor.
        RythmboxApp.EmbeddedRuntime.IsDrm = true;

        // On a TTY the kernel console keeps a blinking cursor and echoes keystrokes to
        // the shell. Hide the cursor and drain console input on a background thread so
        // nothing bleeds through. Avalonia still gets keys via libinput.
        SilenceConsole();

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

        Console.Error.WriteLine(
            $"[DRM] Starting on {card ?? "auto-detect"} " +
            $"(scaling {options.Scaling.ToString(CultureInfo.InvariantCulture)}, orientation {options.Orientation}, input libinput)");

        return BuildAvaloniaApp().StartLinuxDrm(args, card, options: options, inputBackend: new LibInputBackend());
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
