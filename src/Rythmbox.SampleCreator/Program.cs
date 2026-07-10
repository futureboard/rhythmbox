using Avalonia;
using Avalonia.Media;
using System;

namespace Rythmbox.SampleCreator;

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
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://Rythmbox.SampleCreator/Assets/Fonts#Barlow, avares://Rythmbox.SampleCreator/Assets/Fonts#Anuphan",
            })
            .LogToTrace();
}
