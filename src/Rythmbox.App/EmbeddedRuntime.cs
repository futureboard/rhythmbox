namespace Rythmbox.App;

/// <summary>
/// Cross-cutting flags describing how the app is being hosted. Set once at startup
/// (before the UI is built) by the embedded host (Rythmbox.Shell); the desktop entry
/// point leaves the defaults. Used by shared views to enable embedded-only behavior
/// such as the software mouse cursor, which the DRM/KMS backend does not draw itself.
/// </summary>
public static class EmbeddedRuntime
{
    /// <summary>
    /// True when running on the Linux DRM/KMS framebuffer (no X11/Wayland). In that
    /// mode there is no compositor-drawn pointer, so the UI renders its own.
    /// </summary>
    public static bool IsDrm { get; set; }
}
