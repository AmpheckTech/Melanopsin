using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed partial class ComboBox :
    Control,
    IInputHandler,
    IKeyboardInputHandler,
    ILayoutParticipant,
    IRenderCommandProvider,
    IAccessibilityActionProvider {
    private readonly List<Option> _options = [];
    private bool _isOpen;

    public ComboBox()
        : base("ComboBox") {
        IsRepaintBoundary = true;
        AllowsRasterCache = false;
        InputHandler = this;
        IsFocusable = true;
        ClipToBounds = false;
        Accessibility.Role = AccessibilityRole.ComboBox;
        InlineStyle.Height = Length.Su(8);
        Invalidated += OnComboBoxInvalidated;
    }

    public event Action<int, string>? SelectionChanged;

    public IReadOnlyList<Option> Options => _options;

    [MarkupAttribute("selected-index", MarkupValueKind.Integer)]
    public int SelectedIndex { get; private set; } = -1;

    public bool IsOpen {
        get => _isOpen;
        private set {
            if (_isOpen == value) {
                return;
            }

            _isOpen = value;
            SetState(UiStateNames.Expanded, value);
        }
    }

    public string SelectedText =>
        SelectedIndex >= 0 && SelectedIndex < _options.Count
            ? _options[SelectedIndex].Text
            : "Select…";

    public string? SelectedValue =>
        SelectedIndex >= 0 && SelectedIndex < _options.Count
            ? _options[SelectedIndex].Value
            : null;

    internal float TriggerHeight { get; private set; }

    [MarkupAttribute("items")]
    public string ItemList { get; private set; } = string.Empty;

    public AccessibilityAction SupportedActions => IsEnabled
        ? AccessibilityAction.Default |
          AccessibilityAction.Expand |
          AccessibilityAction.Collapse
        : AccessibilityAction.None;

    public bool PerformAccessibilityAction(AccessibilityAction action) {
        if (!IsEnabled) {
            return false;
        }

        switch (action) {
            case AccessibilityAction.Default:
                SetExpanded(!IsOpen);
                return true;
            case AccessibilityAction.Expand:
                SetExpanded(true);
                return true;
            case AccessibilityAction.Collapse:
                SetExpanded(false);
                return true;
            default:
                return false;
        }
    }

    public void SetExpanded(bool expanded) {
        if (IsOpen != expanded) {
            IsOpen = expanded;
            InvalidateLayout();
        }

        SyncPopup();
    }

    protected internal override void OnFocusChanged(bool focused) {
        base.OnFocusChanged(focused);
        if (!focused) {
            SetExpanded(false);
        }
    }

    public void SetItems(IReadOnlyList<string> items) {
        ArgumentNullException.ThrowIfNull(items);
        var options = new List<Node>(items.Count);
        foreach (var item in items) {
            var option = new Option();
            option.SetAttributeValue("text", item);
            option.SetAttributeValue("value", item);
            options.Add(option);
        }

        var selectedIndex = Math.Min(SelectedIndex, options.Count - 1);
        ReplaceChildren(options);
        SelectedIndex = selectedIndex;
        ApplySelection();
        UpdateAccessibility();
        InvalidateLayout();
    }

    public void Select(int index) {
        if (index < 0 || index >= _options.Count || index == SelectedIndex) {
            return;
        }

        SelectedIndex = index;
        ApplySelection();
        _popup?.EnsureRowVisible(SelectedIndex);
        UpdateAccessibility();
        InvalidateLayout();
        SelectionChanged?.Invoke(index, _options[index].Value ?? _options[index].Text);
    }

    public override void AppendChild(Node child) {
        EnsureOption(child);
        base.AppendChild(child);
    }

    public override void InsertChild(int index, Node child) {
        EnsureOption(child);
        base.InsertChild(index, child);
    }

    protected internal override InvalidationKind OnReconciledFrom(Element source) {
        _ = base.OnReconciledFrom(source);
        if (source is not ComboBox incoming) {
            return InvalidationKind.None;
        }

        var optionsChanged = !OptionsMatch(incoming._options);
        var selectionChanged = SelectedIndex != incoming.SelectedIndex;
        var expansionChanged = IsOpen != incoming.IsOpen;
        SelectedIndex = incoming.SelectedIndex;
        IsOpen = incoming.IsOpen;
        ApplySelection();
        UpdateAccessibility();
        SyncPopup();
        if (expansionChanged || optionsChanged) {
            return InvalidationKind.LayoutAndPaint;
        }

        return selectionChanged
            ? InvalidationKind.Paint
            : InvalidationKind.None;
    }

    LayoutSize ILayoutParticipant.Measure(
        LayoutMeasureContext context,
        LayoutConstraints constraints) {
        var style = context.GetStyle(this);
        var widthFallback = constraints.HasBoundedWidth
            ? constraints.MaxWidth
            : 0;
        var contentWidth = context.ResolveWidth(
            style,
            constraints,
            widthFallback);
        var width = constraints.ConstrainWidth(
            contentWidth + (style.Margin * 2));
        var fallbackHeight = context.Metrics.Space(8);
        var height = context.ResolveHeight(
            style,
            constraints,
            fallbackHeight);
        height = constraints.ConstrainHeight(height + (style.Margin * 2));
        return new LayoutSize(width, height);
    }

    void ILayoutParticipant.Arrange(
        LayoutArrangeContext context,
        Rect bounds) {
        UpdateTriggerBounds(bounds, context.ChildClipBounds);
        SyncPopup();
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        if (name == "selected-index") {
            SelectedIndex = value is int selectedIndex ? selectedIndex : SelectedIndex;
            ApplySelection();
            UpdateAccessibility();
        } else if (name == "items") {
            ItemList = value as string ?? string.Empty;
            SetItems(ItemList.Split(
                '|',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }

    protected override InvalidationKind ClassifyAttributeChange(string name) {
        return name switch {
            "selected-index" => InvalidationKind.Paint,
            "items" => InvalidationKind.LayoutAndPaint,
            _ => base.ClassifyAttributeChange(name),
        };
    }

    internal void SelectFromPopup(int index) {
        SetExpanded(false);
        Select(index);
    }

    private bool OptionsMatch(IReadOnlyList<Option> incoming) {
        if (_options.Count != incoming.Count) {
            return false;
        }

        for (var index = 0; index < _options.Count; index++) {
            if (!string.Equals(
                    _options[index].Text,
                    incoming[index].Text,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    _options[index].Value,
                    incoming[index].Value,
                    StringComparison.Ordinal)) {
                return false;
            }
        }

        return true;
    }

}
