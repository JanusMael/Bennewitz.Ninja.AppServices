using AppServices.Tests.Headless;
using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]

namespace AppServices.Tests.Headless;

/// <summary>
/// The Avalonia application behind this assembly's headless tests: dispatcher, layout and input,
/// no rendering, and no theme.
/// </summary>
/// <remarks>
/// ⓘ No theme, unlike ScopedEditors' test app. The dialogs here are walked as built and never
/// shown, so no control template is applied and a theme would change nothing that is asserted.
/// </remarks>
public sealed class HeadlessTestApp : Application
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<HeadlessTestApp>()
                         .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
    }
}
