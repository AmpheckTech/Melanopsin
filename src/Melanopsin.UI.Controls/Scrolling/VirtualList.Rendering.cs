using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

public sealed partial class VirtualList {
    /// <summary>
    /// Emits paint commands from the latest layout snapshot. It does not
    /// correct scroll bounds or materialize rows.
    /// </summary>
    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        if (ItemCount == 0 || context.Bounds.Height <= 0) {
            return;
        }

        if (UsesItemTemplate) {
            AppendSelectionCommands(context, commands);
            return;
        }

        var first = _rowLayout.FindRowAt(_scrollOffset, ItemCount);
        first = Math.Max(0, first - Math.Max(0, Overscan));
        var lastVisible = _rowLayout.FindRowAt(
            _scrollOffset + context.Bounds.Height,
            ItemCount);
        var last = Math.Min(
            ItemCount - 1,
            lastVisible + Math.Max(0, Overscan));
        for (var index = first; index <= last; index++) {
            var rowHeight = _rowLayout.GetRowHeight(index);
            var rowBounds = new Rect(
                context.Bounds.X,
                context.Bounds.Y + _rowLayout.GetRowTop(index) - _scrollOffset,
                context.Bounds.Width,
                rowHeight);
            if (index == _selectedIndex) {
                commands.Add(new FillRectangleCommand(
                    rowBounds,
                    new Color(224, 238, 232),
                    context.CornerRadius * 0.5f));
            }

            context.AppendText(
                commands,
                ItemTextProvider?.Invoke(index) ?? $"Item {index + 1}",
                new Rect(
                    rowBounds.X + context.Padding,
                    rowBounds.Y,
                    Math.Max(0, rowBounds.Width - (context.Padding * 2)),
                    rowBounds.Height),
                context.Foreground,
                TextHorizontalAlignment.Start,
                TextVerticalAlignment.Center,
                wrap: false);
        }
    }

    private void AppendSelectionCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        if (_selectedIndex < 0) {
            return;
        }

        foreach (var child in Children) {
            if (GetRealizedIndex(child) != _selectedIndex) {
                continue;
            }

            var rowBounds = new Rect(
                context.Bounds.X + context.Padding,
                context.Bounds.Y + context.Padding +
                    _rowLayout.GetRowTop(_selectedIndex) - _scrollOffset,
                Math.Max(0, context.Bounds.Width - (context.Padding * 2)),
                _rowLayout.GetRowHeight(_selectedIndex));
            commands.Add(new FillRectangleCommand(
                rowBounds,
                new Color(224, 238, 232),
                context.CornerRadius * 0.5f));
        }
    }
}
