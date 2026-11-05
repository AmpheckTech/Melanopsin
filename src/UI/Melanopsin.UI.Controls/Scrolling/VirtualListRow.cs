using Melanopsin.UI.Accessibility;

namespace Melanopsin.UI.Controls;

/// <summary>
/// Wraps a template-produced node so the virtual list can own row identity,
/// height and selection independently of the user template root.
/// </summary>
public sealed class VirtualListRow : Control {
    public VirtualListRow()
        : base("VirtualListRow") {
        Accessibility.Role = AccessibilityRole.ListItem;
    }
}
