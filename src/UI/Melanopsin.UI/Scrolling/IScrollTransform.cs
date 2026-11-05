namespace Melanopsin.UI;

/// <summary>
/// Exposes a scroll translation to composition and hit testing without
/// requiring a control to derive from <see cref="ScrollViewport"/>.
/// </summary>
public interface IScrollTransform {
    float OffsetX { get; }

    float OffsetY { get; }
}
