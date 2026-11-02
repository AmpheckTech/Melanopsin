namespace Melanopsin.UI.Hosting;

public sealed class WindowOptions {
    public required string Title { get; init; }

    /// <summary>
    /// The density-independent client size. The platform converts it to
    /// physical pixels using the current display scale.
    /// </summary>
    public required PixelSize Size { get; init; }
}
