namespace Melanopsin.UI.App;

/// <summary>
/// Identifies the dimensions on which a resource contract may vary.
/// </summary>
[Flags]
public enum ResourceVariantKinds {
    None = 0,
    Culture = 1 << 0,
    Theme = 1 << 1,
    Style = 1 << 2,
    DisplayScale = 1 << 3,
}
