using Avalonia;
using Avalonia.Media;
using System;

namespace Rythmbox.Editor;

sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Console.Error.WriteLine($"[FATAL] {e.ExceptionObject}");

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL] {ex}");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://Rythmbox.Editor/Assets/Fonts#Barlow",
            })
            .LogToTrace();
}
