using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

internal sealed class ComboBoxPopup :
    Overlay,
    IFocusProxy,
    IInputHandler,
    ILayoutParticipant,
    IRenderCommandProvider {
    private const int MaxVisibleRows = 8;
    private const float ScrollbarWidth = 2;
    private static readonly Color RowHoverColor = new(243, 248, 245);
    private static readonly Color RowSelectedColor = new(226, 238, 232);
    private static readonly Color ScrollbarColor = new(31, 109, 89, 96);
    private int _pressedRow = -1;
    private int _hoveredRow = -1;
    private float _scrollOffset;
    private Rect _arrangedBounds;
    private Rect _contentBounds;

    public ComboBoxPopup(ComboBox owner)
        : base("ComboBoxPopup") {
        Owner = owner;
        IsModal = false;
        IsFocusable = true;
        InputHandler = this;
        IsRepaintBoundary = true;
        AllowsRasterCache = false;
        ClipToBounds = true;
        Accessibility.Role = AccessibilityRole.List;
    }

    public ComboBox Owner { get; }

    public Rect AnchorBounds { get; private set; }

    public Node FocusTarget => Owner;

    public void UpdateAnchor(Rect bounds) {
        if (AnchorBounds == bounds) {
            return;
        }

        AnchorBounds = bounds;
        InvalidateLayout();
    }

    internal void EnsureRowVisible(int index, bool invalidate = true) {
        var rowHeight = Owner.TriggerHeight;
        if (index < 0 || index >= Owner.Options.Count ||
            rowHeight <= 0 ||
            _contentBounds.IsEmpty) {
            return;
        }

        var rowTop = index * rowHeight;
        var rowBottom = rowTop + rowHeight;
        var nextOffset = _scrollOffset;
        if (rowTop < _scrollOffset) {
            nextOffset = rowTop;
        } else if (rowBottom > _scrollOffset + _contentBounds.Height) {
            nextOffset = rowBottom - _contentBounds.Height;
        }

        SetScrollOffset(nextOffset, invalidate);
    }

    public void Handle(RoutedInputEvent inputEvent) {
        var phase = inputEvent.Input.Phase;
        if (phase == PointerPhase.Scroll) {
            ScrollBy(-inputEvent.Input.WheelDelta / 120f * Owner.TriggerHeight);
            inputEvent.Handled = true;
            return;
        }

        if (!inputEvent.Input.IsPrimaryButton) {
            return;
        }

        var row = HitRow(inputEvent.LayoutY);
        if (phase == PointerPhase.Down) {
            _pressedRow = row;
            inputEvent.Handled = true;
        } else if (phase == PointerPhase.Up) {
            var selectedRow = row;
            var wasPressed = _pressedRow;
            _pressedRow = -1;
            inputEvent.Handled = true;
            if (selectedRow >= 0 && selectedRow == wasPressed) {
                Owner.SelectFromPopup(selectedRow);
            }
        } else if (phase is PointerPhase.Cancel or PointerPhase.Leave) {
            _pressedRow = -1;
            SetHoveredRow(-1);
            inputEvent.Handled = true;
        } else {
            SetHoveredRow(row);
        }
    }

    LayoutSize ILayoutParticipant.Measure(
        LayoutMeasureContext context,
        LayoutConstraints constraints) {
        var style = context.GetStyle(this);
        var visibleRows = Math.Min(Owner.Options.Count, MaxVisibleRows);
        return new LayoutSize(
            constraints.ConstrainWidth(AnchorBounds.Width),
            constraints.ConstrainHeight(
                (style.Padding * 2) +
                (Owner.TriggerHeight * visibleRows)));
    }

    protected internal override Rect GetArrangeBounds(
        LayoutArrangeContext context,
        LayoutSize measured) {
        var content = context.ContentBounds;
        var style = context.GetStyle(this);
        var width = Math.Min(
            content.Width,
            Math.Max(1, Math.Min(AnchorBounds.Width, measured.Width)));
        var desiredHeight = Math.Min(content.Height, measured.Height);
        var spaceBelow = Math.Max(0, content.MaxY - AnchorBounds.MaxY);
        var spaceAbove = Math.Max(0, AnchorBounds.MinY - content.MinY);
        var height = Math.Min(
            desiredHeight,
            Math.Max(1, Math.Max(spaceBelow, spaceAbove)));
        var x = Math.Clamp(
            AnchorBounds.MinX,
            content.MinX,
            Math.Max(content.MinX, content.MaxX - width));
        var placeBelow = spaceBelow >= height || spaceBelow >= spaceAbove;
        var y = placeBelow
            ? AnchorBounds.MaxY
            : AnchorBounds.MinY - height;
        y = Math.Clamp(
            y,
            content.MinY,
            Math.Max(content.MinY, content.MaxY - height));
        _arrangedBounds = new Rect(x, y, width, height);
        _contentBounds = new Rect(
            x + style.Padding,
            y + style.Padding,
            Math.Max(0, width - (style.Padding * 2)),
            Math.Max(0, height - (style.Padding * 2)));
        _scrollOffset = Math.Clamp(
            _scrollOffset,
            0,
            GetMaximumScrollOffset());
        EnsureRowVisible(Owner.SelectedIndex, invalidate: false);
        return _arrangedBounds;
    }

    void ILayoutParticipant.Arrange(
        LayoutArrangeContext context,
        Rect bounds) {
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        var rowHeight = Owner.TriggerHeight;
        if (rowHeight <= 0 || _contentBounds.IsEmpty) {
            return;
        }

        var maximumScroll = GetMaximumScrollOffset();
        var scrollbarGutter = maximumScroll > 0 ? ScrollbarWidth + 2 : 0;
        var firstVisibleRow = Math.Max(
            0,
            (int)Math.Floor(_scrollOffset / rowHeight));
        for (var index = firstVisibleRow;
             index < Owner.Options.Count;
             index++) {
            var rowY = _contentBounds.MinY +
                (index * rowHeight) -
                _scrollOffset;
            if (rowY >= _contentBounds.MaxY) {
                break;
            }

            var renderedHeight = Math.Min(
                rowHeight,
                _contentBounds.MaxY - rowY);
            if (renderedHeight <= 0 || rowY + renderedHeight <= _contentBounds.MinY) {
                continue;
            }

            var rowColor = index == _hoveredRow
                ? RowHoverColor
                : index == Owner.SelectedIndex
                    ? RowSelectedColor
                    : (Color?)null;
            if (rowColor is { } background) {
                commands.Add(new FillRectangleCommand(
                    new Rect(
                        _contentBounds.MinX,
                        rowY,
                        _contentBounds.Width,
                        renderedHeight),
                    background));
            }

            var textBounds = new Rect(
                _contentBounds.MinX + context.Padding,
                rowY,
                Math.Max(
                    0,
                    _contentBounds.Width -
                    (context.Padding * 2) -
                    scrollbarGutter),
                renderedHeight);
            var text = Owner.Options[index].Text;
            var displayText = context.TextLayout is { } textLayout
                ? TextFitting.FitSingleLine(
                    textLayout,
                    text,
                    context.FontSize,
                    context.Font,
                    textBounds.Width)
                : text;
            context.AppendText(
                commands,
                displayText,
                textBounds,
                context.Foreground,
                TextHorizontalAlignment.Start,
                TextVerticalAlignment.Center,
                wrap: false);
        }

        if (maximumScroll > 0) {
            var trackHeight = _contentBounds.Height;
            var thumbHeight = Math.Max(
                rowHeight / 2,
                trackHeight * trackHeight /
                (Owner.Options.Count * rowHeight));
            var thumbTravel = Math.Max(0, trackHeight - thumbHeight);
            var thumbY = _contentBounds.MinY +
                (thumbTravel * (_scrollOffset / maximumScroll));
            commands.Add(new FillRectangleCommand(
                new Rect(
                    _contentBounds.MaxX - ScrollbarWidth,
                    thumbY,
                    ScrollbarWidth,
                    Math.Min(trackHeight, thumbHeight)),
                ScrollbarColor,
                ScrollbarWidth / 2));
        }
    }

    private int HitRow(float layoutY) {
        var rowHeight = Owner.TriggerHeight;
        if (rowHeight <= 0 ||
            _contentBounds.IsEmpty ||
            layoutY < _contentBounds.MinY ||
            layoutY >= _contentBounds.MaxY) {
            return -1;
        }

        var contentY = layoutY - _contentBounds.MinY + _scrollOffset;
        var row = (int)Math.Floor(contentY / rowHeight);
        return row >= 0 && row < Owner.Options.Count ? row : -1;
    }

    private void ScrollBy(float deltaY) {
        SetScrollOffset(_scrollOffset + deltaY);
    }

    private void SetScrollOffset(float value, bool invalidate = true) {
        var next = Math.Clamp(value, 0, GetMaximumScrollOffset());
        if (Math.Abs(next - _scrollOffset) < 0.001f) {
            return;
        }

        _scrollOffset = next;
        if (invalidate) {
            InvalidatePaint();
        }
    }

    private float GetMaximumScrollOffset() {
        if (_contentBounds.IsEmpty) {
            return 0;
        }

        return Math.Max(
            0,
            (Owner.Options.Count * Owner.TriggerHeight) - _contentBounds.Height);
    }

    private void SetHoveredRow(int row) {
        if (_hoveredRow == row) {
            return;
        }

        _hoveredRow = row;
        InvalidatePaint();
    }
}
