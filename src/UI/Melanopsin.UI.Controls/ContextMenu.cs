using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

public sealed class ContextMenu :
    Overlay,
    IInputHandler,
    IKeyboardInputHandler,
    IRenderCommandProvider {
    private const float RowHeightSu = 9;
    private readonly List<ContextMenuItem?> _items = [];
    private ContextMenuRequest _request;
    private int _activeIndex = -1;
    private int _pressedIndex = -1;
    private float _renderRowHeight = RowHeightSu * 4;
    private float _renderPadding = 8;

    public ContextMenu(ContextMenuRequest request)
        : base("ContextMenu") {
        ArgumentNullException.ThrowIfNull(request);
        _request = request;
        IsModal = false;
        DismissOnBackdrop = true;
        IsFocusable = true;
        IsRepaintBoundary = true;
        InputHandler = this;
        Accessibility.Role = AccessibilityRole.Menu;
        InlineStyle.Width = Length.Su(58);
        UpdateHeight();
    }

    public ContextMenuRequest Request => _request;

    public ContextMenu AddItem(
        string text,
        Action action,
        bool isEnabled = true) {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(action);
        _items.Add(new ContextMenuItem(text, action, isEnabled));
        UpdateHeight();
        if (_activeIndex < 0 && isEnabled) {
            _activeIndex = _items.Count - 1;
        }

        return this;
    }

    public ContextMenu AddSeparator() {
        _items.Add(default);
        UpdateHeight();
        return this;
    }

    public void Show() {
        if (Parent is not null) {
            throw new InvalidOperationException("The context menu is already open.");
        }

        if (FindOverlayHost(_request.Source) is not { } host) {
            throw new InvalidOperationException(
                "The context menu source is not inside an OverlayHost.");
        }

        _ = host.Show(this);
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        commands.Add(new FillRectangleCommand(
            context.Bounds,
            new Color(250, 252, 251),
            context.CornerRadius));
        commands.Add(new StrokeRectangleCommand(
            context.Bounds,
            new Color(194, 207, 201),
            1,
            context.CornerRadius));

        var rowHeight = context.FontSize * 2.25f;
        _renderRowHeight = rowHeight;
        _renderPadding = context.Padding;
        for (var index = 0; index < _items.Count; index++) {
            var row = new Rect(
                context.Bounds.MinX + context.Padding,
                context.Bounds.MinY + context.Padding + (index * rowHeight),
                Math.Max(0, context.Bounds.Width - (context.Padding * 2)),
                rowHeight);
            if (_items[index] is not { } item) {
                var separatorY = row.MinY + (row.Height / 2);
                commands.Add(new FillRectangleCommand(
                    new Rect(
                        row.MinX + context.Padding,
                        separatorY,
                        Math.Max(0, row.Width - (context.Padding * 2)),
                        1),
                    new Color(220, 228, 224)));
                continue;
            }

            if (index == _activeIndex && item.IsEnabled) {
                commands.Add(new FillRectangleCommand(
                    row,
                    new Color(230, 240, 236),
                    4));
            }

            context.AppendText(
                commands,
                item.Text,
                new Rect(
                    row.MinX + context.Padding,
                    row.MinY,
                    Math.Max(0, row.Width - (context.Padding * 2)),
                    row.Height),
                item.IsEnabled
                    ? context.Foreground
                    : new Color(139, 151, 146),
                TextHorizontalAlignment.Start,
                TextVerticalAlignment.Center,
                wrap: false);
        }
    }

    public void Handle(RoutedInputEvent inputEvent) {
        if (!inputEvent.Input.IsPrimaryButton) {
            return;
        }

        var index = HitItem(inputEvent.LayoutY);
        switch (inputEvent.Input.Phase) {
            case PointerPhase.Down:
                _pressedIndex = CanInvoke(index) ? index : -1;
                inputEvent.Handled = true;
                break;
            case PointerPhase.Move:
                SetActive(CanInvoke(index) ? index : -1);
                break;
            case PointerPhase.Up:
                var selected = _pressedIndex;
                _pressedIndex = -1;
                inputEvent.Handled = true;
                if (inputEvent.IsInsideTarget && selected == index) {
                    Invoke(index);
                }

                break;
            case PointerPhase.Cancel or PointerPhase.Leave:
                _pressedIndex = -1;
                SetActive(-1);
                break;
        }
    }

    public bool HandleKey(KeyInputEvent input) {
        if (input.Phase != KeyPhase.Down) {
            return false;
        }

        switch (input.Key) {
            case Key.Down:
                SetActive(FindAdjacent(_activeIndex, 1));
                return _activeIndex >= 0;
            case Key.Up:
                SetActive(FindAdjacent(_activeIndex < 0 ? 0 : _activeIndex, -1));
                return _activeIndex >= 0;
            case Key.Home:
                SetActive(FindAdjacent(-1, 1));
                return _activeIndex >= 0;
            case Key.End:
                SetActive(FindAdjacent(0, -1));
                return _activeIndex >= 0;
            case Key.Enter:
            case Key.Space:
                if (!CanInvoke(_activeIndex)) {
                    return false;
                }

                Invoke(_activeIndex);
                return true;
            default:
                return false;
        }
    }

    protected internal override Rect GetArrangeBounds(
        LayoutArrangeContext context,
        LayoutSize measured) {
        var bounds = context.ContentBounds;
        var x = Math.Clamp(
            _request.X,
            bounds.MinX,
            Math.Max(bounds.MinX, bounds.MaxX - measured.Width));
        var y = Math.Clamp(
            _request.Y,
            bounds.MinY,
            Math.Max(bounds.MinY, bounds.MaxY - measured.Height));
        return new Rect(x, y, measured.Width, measured.Height);
    }

    private void Invoke(int index) {
        if (index < 0 || _items[index] is not { IsEnabled: true } item) {
            return;
        }

        Close(item.Text);
        item.Action();
    }

    private int HitItem(float localY) {
        var index = (int)MathF.Floor(
            (localY - _renderPadding) / _renderRowHeight);
        return index >= 0 && index < _items.Count ? index : -1;
    }

    private bool CanInvoke(int index) {
        return index >= 0 && _items[index] is { IsEnabled: true };
    }

    private void SetActive(int index) {
        if (_activeIndex == index) {
            return;
        }

        _activeIndex = index;
        InvalidatePaint();
    }

    private int FindAdjacent(int from, int direction) {
        if (_items.Count == 0) {
            return -1;
        }

        var index = from;
        for (var count = 0; count < _items.Count; count++) {
            index = (index + direction + _items.Count) % _items.Count;
            if (CanInvoke(index)) {
                return index;
            }
        }

        return -1;
    }

    private void UpdateHeight() {
        var rows = Math.Max(1, _items.Count);
        InlineStyle.Height = Length.Su((rows * RowHeightSu) + 2);
    }

    private static OverlayHost? FindOverlayHost(Node source) {
        var root = source;
        while (root.Parent is not null) {
            root = root.Parent;
        }

        return Find(root);

        static OverlayHost? Find(Node node) {
            if (node is OverlayHost host) {
                return host;
            }

            if (node is not Element element) {
                return null;
            }

            foreach (var child in element.Children) {
                if (Find(child) is { } nestedHost) {
                    return nestedHost;
                }
            }

            return null;
        }
    }

    private sealed record ContextMenuItem(
        string Text,
        Action Action,
        bool IsEnabled);
}
