using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement("Tab")]
public sealed class TabItem : Control, IAccessibilityActionProvider {
    public TabItem()
        : base("TabItem") {
        SetVisible(false);
        Accessibility.Role = AccessibilityRole.Tab;
    }

    [MarkupAttribute("title")]
    public string Title { get; private set; } = string.Empty;

    public AccessibilityAction SupportedActions =>
        IsEnabled ? AccessibilityAction.Select : AccessibilityAction.None;

    public bool PerformAccessibilityAction(AccessibilityAction action) {
        if (action != AccessibilityAction.Select ||
            !IsEnabled ||
            Parent is not TabControl tabs) {
            return false;
        }

        for (var index = 0; index < tabs.Tabs.Count; index++) {
            if (ReferenceEquals(tabs.Tabs[index], this)) {
                tabs.Select(index);
                return true;
            }
        }

        return false;
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        if (name == "title") {
            Title = value?.ToString() ?? string.Empty;
            Accessibility.Name = Title;
        }
    }
}
