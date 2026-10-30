using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class ComboBox :
    Control,
    IInputHandler,
    IKeyboardInputHandler,
    ILayoutParticipant,
    IRenderCommandProvider,
    IAccessibilityActionProvider {
    private readonly List<Option> _options = [];
    private readonly List<Node> _scrollObservers = [];
    private ComboBoxPopup? _popup;
    private OverlayHost? _overlayHost;
    private Rect _triggerLayoutBounds;
    private Rect? _triggerClipBounds;
    private bool _isOpen;

    public ComboBox()
        : base("ComboBox") {
        IsRepaintBoundary = true;
        AllowsRasterCache = false;
        InputHandler = this;
        IsFocusable = true;
        ClipToBounds = false;
        Accessibility.Role = AccessibilityRole.ComboBox;
        InlineStyle.Height = Length.Space(8);
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

    public void Handle(RoutedInputEvent inputEvent) {
        if (!IsEnabled) {
            SetState(UiStateNames.Pressed, false);
            return;
        }

        var phase = inputEvent.Input.Phase;
        if (phase == PointerPhase.Down) {
            SetState(UiStateNames.Pressed, true);
        } else if (phase == PointerPhase.Up) {
            var activate = HasState(UiStateNames.Pressed) && inputEvent.IsInsideTarget;
            SetState(UiStateNames.Pressed, false);
            if (activate) {
                Activate(inputEvent);
            }
        } else if (phase is PointerPhase.Cancel or PointerPhase.Leave) {
            SetState(UiStateNames.Pressed, false);
        }
    }

    public bool HandleKey(KeyInputEvent input) {
        if (input.Phase != KeyPhase.Down || !IsEnabled) {
            return false;
        }

        var current = SelectedIndex < 0 ? 0 : SelectedIndex;
        switch (input.Key) {
            case Key.Escape when IsOpen:
                SetExpanded(false);
                return true;
            case Key.Enter:
            case Key.Space:
                SetExpanded(!IsOpen);
                return true;
            case Key.Down when IsOpen:
                Select(Math.Min(_options.Count - 1, current + 1));
                return _options.Count > 0;
            case Key.Up when IsOpen:
                Select(Math.Max(0, current - 1));
                return _options.Count > 0;
            case Key.Down when !IsOpen:
                SetExpanded(true);
                return true;
            case Key.Up when !IsOpen:
                SetExpanded(true);
                return true;
            case Key.Home when IsOpen:
                Select(0);
                return _options.Count > 0;
            case Key.End when IsOpen:
                Select(_options.Count - 1);
                return _options.Count > 0;
            default:
                return false;
        }
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        AppendTriggerText(commands, context);
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
        _triggerLayoutBounds = bounds;
        _triggerClipBounds = context.ChildClipBounds;
        TriggerHeight = Math.Max(1, bounds.Height);
        RefreshScrollObservers();
        SyncPopup();
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        if (name == "selected-index") {
            SelectedIndex = ControlValueParser.ReadInteger(value) ?? SelectedIndex;
            ApplySelection();
            UpdateAccessibility();
        } else if (name == "items") {
            ItemList = value?.ToString() ?? string.Empty;
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

    protected internal override void OnChildAdded(Node child) {
        base.OnChildAdded(child);
        if (child is not Option option) {
            return;
        }

        RefreshOptions();
        var optionIndex = _options.IndexOf(option);
        if (option.IsSelected) {
            SelectedIndex = optionIndex;
        } else if (SelectedIndex < 0 && _options.Count == 1) {
            SelectedIndex = 0;
        }

        ApplySelection();
        UpdateAccessibility();
        _popup?.InvalidateLayout();
    }

    protected internal override void OnChildRemoved(Node child) {
        base.OnChildRemoved(child);
        if (child is not Option) {
            return;
        }

        RefreshOptions();
        SelectedIndex = _options.Count == 0
            ? -1
            : Math.Min(SelectedIndex, _options.Count - 1);
        ApplySelection();
        UpdateAccessibility();
        _popup?.InvalidateLayout();
    }

    protected internal override void ReplaceChildren(
        IReadOnlyList<Node> children) {
        foreach (var child in children) {
            EnsureOption(child);
        }

        var selectedIndex = SelectedIndex;
        base.ReplaceChildren(children);
        SelectedIndex = _options.Count == 0
            ? -1
            : Math.Clamp(selectedIndex, 0, _options.Count - 1);
        ApplySelection();
        UpdateAccessibility();
        _popup?.InvalidateLayout();
    }

    protected internal override void OnMounted() {
        base.OnMounted();
        SyncPopup();
    }

    protected internal override void OnUnmounted() {
        ClosePopup();
        ClearScrollObservers();
        _overlayHost = null;
        _triggerClipBounds = null;
        base.OnUnmounted();
    }

    private void Activate(RoutedInputEvent inputEvent) {
        if (!IsOpen) {
            SetExpanded(true);
            return;
        }

        if (inputEvent.Target is Option option) {
            var optionIndex = _options.IndexOf(option);
            SetExpanded(false);
            if (optionIndex >= 0) {
                Select(optionIndex);
            }

            return;
        }

        SetExpanded(false);
    }

    internal void SelectFromPopup(int index) {
        SetExpanded(false);
        Select(index);
    }

    private void SyncPopup() {
        var host = FindOverlayHost();
        if (!ReferenceEquals(host, _overlayHost)) {
            ClosePopup();
            _overlayHost = host;
        }

        if (host is null ||
            !IsMounted ||
            !IsOpen ||
            _options.Count == 0 ||
            TriggerHeight <= 0 ||
            _triggerLayoutBounds.Width <= 0) {
            ClosePopup();
            return;
        }

        if (_popup is null) {
            _popup = new ComboBoxPopup(this);
            _popup.Closed += OnPopupClosed;
            _ = host.Show(_popup);
        } else if (!host.Overlays.Contains(
                       _popup,
                       ReferenceEqualityComparer.Instance)) {
            ClosePopup();
            _popup = new ComboBoxPopup(this);
            _popup.Closed += OnPopupClosed;
            _ = host.Show(_popup);
        }

        var anchorBounds = GetVisibleTriggerBounds();
        if (anchorBounds is null) {
            ClosePopup();
            IsOpen = false;
            return;
        }

        _popup.UpdateAnchor(anchorBounds.Value);
        _popup.EnsureRowVisible(SelectedIndex);
    }

    private void ClosePopup() {
        var popup = _popup;
        if (popup is null) {
            return;
        }

        _popup = null;
        popup.Closed -= OnPopupClosed;
        if (_overlayHost is { } host &&
            host.Overlays.Contains(popup, ReferenceEqualityComparer.Instance)) {
            _ = host.Close(popup);
        } else if (popup.IsOpen) {
            popup.Close();
        }
    }

    private void OnPopupClosed(Overlay popup) {
        if (!ReferenceEquals(popup, _popup)) {
            return;
        }

        popup.Closed -= OnPopupClosed;
        _popup = null;
        if (IsOpen) {
            SetExpanded(false);
        }
    }

    private OverlayHost? FindOverlayHost() {
        for (var current = Parent; current is not null; current = current.Parent) {
            if (current is OverlayHost host) {
                return host;
            }
        }

        return null;
    }

    private Rect? GetVisibleTriggerBounds() {
        var x = _triggerLayoutBounds.X;
        var y = _triggerLayoutBounds.Y;
        for (var current = Parent; current is not null; current = current.Parent) {
            if (current is IScrollTransform scrollTransform) {
                x -= scrollTransform.OffsetX;
                y -= scrollTransform.OffsetY;
            }
        }

        var bounds = new Rect(
            x,
            y,
            _triggerLayoutBounds.Width,
            _triggerLayoutBounds.Height);
        return _triggerClipBounds is { } clipBounds &&
            !bounds.IntersectsWith(clipBounds)
                ? null
                : bounds;
    }

    private void RefreshScrollObservers() {
        ClearScrollObservers();
        for (var current = Parent; current is not null; current = current.Parent) {
            if (current is IScrollTransform) {
                current.Invalidated += OnScrollInvalidated;
                _scrollObservers.Add(current);
            }
        }
    }

    private void ClearScrollObservers() {
        foreach (var observer in _scrollObservers) {
            observer.Invalidated -= OnScrollInvalidated;
        }

        _scrollObservers.Clear();
    }

    private void OnScrollInvalidated(NodeInvalidation invalidation) {
        if (_popup is null ||
            !_scrollObservers.Contains(invalidation.Source)) {
            return;
        }

        if (invalidation.Kind != InvalidationKind.None) {
            SetExpanded(false);
        }
    }

    private void OnComboBoxInvalidated(NodeInvalidation invalidation) {
        if (_popup is null) {
            return;
        }

        if ((invalidation.Kind & InvalidationKind.Layout) != 0) {
            _popup.InvalidateLayout();
        } else if ((invalidation.Kind & InvalidationKind.Paint) != 0) {
            _popup.InvalidatePaint();
        }
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

    private static void EnsureOption(Node child) {
        ArgumentNullException.ThrowIfNull(child);
        if (child is not Option) {
            throw new ArgumentException(
                "ComboBox accepts only Option children.",
                nameof(child));
        }
    }

    private void RefreshOptions() {
        _options.Clear();
        _options.AddRange(Children.OfType<Option>());
    }

    private void ApplySelection() {
        for (var index = 0; index < _options.Count; index++) {
            _options[index].SetSelected(index == SelectedIndex);
        }
    }

    private void UpdateAccessibility() {
        Accessibility.Name = SelectedText;
        Accessibility.Value = SelectedIndex.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private void AppendTriggerText(
        List<RenderCommand> commands,
        RenderCommandContext context) {
        var arrowWidth = context.FontSize;
        var textBounds = new Rect(
            context.Bounds.X + (context.Padding * 2),
            context.Bounds.Y,
            Math.Max(
                0,
                context.Bounds.Width - (context.Padding * 4) - arrowWidth),
            context.Bounds.Height);
        var displayText = context.TextLayout is { } textLayout
            ? TextFitting.FitSingleLine(
                textLayout,
                SelectedText,
                context.FontSize,
                context.Font,
                textBounds.Width)
            : SelectedText;
        context.AppendText(
            commands,
            displayText,
            textBounds,
            context.Foreground,
            TextHorizontalAlignment.Start,
            TextVerticalAlignment.Center,
            wrap: false);

        var arrowBounds = new Rect(
            context.Bounds.Right - context.Padding - arrowWidth,
            context.Bounds.Y,
            arrowWidth,
            context.Bounds.Height);
        context.AppendText(
            commands,
            IsOpen ? "▴" : "▾",
            arrowBounds,
            context.Foreground,
            TextHorizontalAlignment.Center,
            TextVerticalAlignment.Center,
            wrap: false);
    }
}
