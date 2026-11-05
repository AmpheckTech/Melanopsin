namespace Melanopsin.UI.Layout;

/// <summary>
/// Allows a layout participant to declare whether its measure result depends on
/// the current viewport size. Participants that do not implement this contract
/// are treated as viewport-dependent so custom controls remain correct.
/// </summary>
public interface ILayoutViewportDependency {
    /// <summary>
    /// Whether measurement or arrangement reads viewport-relative metrics.
    /// </summary>
    bool UsesViewportUnits { get; }
}
