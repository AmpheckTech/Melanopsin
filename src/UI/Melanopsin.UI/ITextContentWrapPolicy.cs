namespace Melanopsin.UI;

/// <summary>
/// Overrides the default wrapping behavior for text content controls.
/// </summary>
public interface ITextContentWrapPolicy {
    bool WrapText { get; }
}
