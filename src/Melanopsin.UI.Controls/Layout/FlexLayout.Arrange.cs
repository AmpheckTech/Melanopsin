namespace Melanopsin.UI.Controls;

public sealed partial class FlexLayout {
    void ILayoutParticipant.Arrange(
        LayoutArrangeContext context,
        Rect bounds) {
        var content = context.ContentBounds;
        var ordered = OrderChildren(context);
        if (ordered.Count == 0) {
            return;
        }

        var rowAxis = Direction is FlexDirection.Row or FlexDirection.RowReverse;
        var reverse = Direction is FlexDirection.RowReverse or FlexDirection.ColumnReverse;
        if (reverse) {
            ordered.Reverse();
        }

        var mainSize = rowAxis ? content.Width : content.Height;
        var crossSize = rowAxis ? content.Height : content.Width;
        var columnGap = LayoutMath.ResolveGap(ColumnGap, context.Metrics);
        var rowGap = LayoutMath.ResolveGap(RowGap, context.Metrics);
        var mainGap = rowAxis ? columnGap : rowGap;
        var crossGap = rowAxis ? rowGap : columnGap;
        EnsureArrangeCapacity(ordered.Count);
        var childConstraint = LayoutConstraints.Loose(content.Width, content.Height);
        for (var index = 0; index < ordered.Count; index++) {
            _measured[index] = context.Measure(ordered[index], childConstraint);
            _styles[index] = context.GetStyle(ordered[index]);
            _items[index] = ResolveItem(ordered[index], _styles[index]);
        }

        if (Wrap == FlexWrap.NoWrap) {
            ArrangeLine(
                context,
                ordered,
                0,
                ordered.Count,
                rowAxis,
                reverse,
                content,
                mainSize,
                crossSize,
                mainGap);
            return;
        }

        _lines.Clear();
        var lineStart = 0;
        var usedMain = 0f;
        for (var index = 0; index < ordered.Count; index++) {
            var itemMain = MainSize(_measured[index], rowAxis);
            if (index > lineStart &&
                usedMain + mainGap + itemMain > mainSize) {
                _lines.Add(new LineRange(lineStart, index - lineStart));
                lineStart = index;
                usedMain = itemMain;
                continue;
            }

            usedMain += (index == lineStart ? 0 : mainGap) + itemMain;
        }

        if (lineStart < ordered.Count) {
            _lines.Add(new LineRange(lineStart, ordered.Count - lineStart));
        }

        EnsureLineCrossCapacity(_lines.Count);
        var totalCross = 0f;
        for (var lineIndex = 0; lineIndex < _lines.Count; lineIndex++) {
            var line = _lines[lineIndex];
            var lineCross = 0f;
            for (var index = line.Start; index < line.End; index++) {
                lineCross = Math.Max(lineCross, CrossSize(_measured[index], rowAxis));
            }

            _lineCross[lineIndex] = lineCross;
            totalCross += lineCross;
        }

        totalCross += crossGap * Math.Max(0, _lines.Count - 1);
        var freeCross = Math.Max(0, crossSize - totalCross);
        if (AlignContent == LayoutAlignment.Stretch && _lines.Count > 0) {
            var growth = freeCross / _lines.Count;
            for (var index = 0; index < _lines.Count; index++) {
                _lineCross[index] += growth;
            }

            freeCross = 0;
        }

        var crossLeading = AlignContent switch {
            LayoutAlignment.Center => freeCross / 2,
            LayoutAlignment.End => freeCross,
            _ => 0,
        };
        var crossCursor = (rowAxis ? content.Y : content.X) + crossLeading;
        for (var offset = 0; offset < _lines.Count; offset++) {
            var lineIndex = Wrap == FlexWrap.WrapReverse
                ? _lines.Count - 1 - offset
                : offset;
            var line = _lines[lineIndex];
            var lineBounds = rowAxis
                ? new Rect(content.X, crossCursor, content.Width, _lineCross[lineIndex])
                : new Rect(crossCursor, content.Y, _lineCross[lineIndex], content.Height);
            ArrangeLine(
                context,
                ordered,
                line.Start,
                line.Count,
                rowAxis,
                reverse,
                lineBounds,
                mainSize,
                _lineCross[lineIndex],
                mainGap);
            crossCursor += _lineCross[lineIndex] + crossGap;
        }
    }

    private void ArrangeLine(
        LayoutArrangeContext context,
        IReadOnlyList<Node> children,
        int start,
        int count,
        bool rowAxis,
        bool reverse,
        Rect content,
        float mainSize,
        float crossSize,
        float gap) {
        var end = start + count;
        var totalMain = 0f;
        var totalGrow = 0f;
        var totalShrink = 0f;
        for (var index = start; index < end; index++) {
            _baseMain[index] = MainSize(_measured[index], rowAxis);
            totalMain += _baseMain[index];
            totalGrow += _items[index].Grow;
            totalShrink += _items[index].Shrink * _baseMain[index];
        }

        totalMain += gap * Math.Max(0, count - 1);
        var free = mainSize - totalMain;
        if (free < 0 && totalShrink > 0) {
            var overflow = -free;
            for (var index = start; index < end; index++) {
                var weight = _items[index].Shrink * _baseMain[index];
                _baseMain[index] = Math.Max(
                    0,
                    _baseMain[index] - (overflow * (weight / totalShrink)));
            }

            free = 0;
        } else if (free > 0 && totalGrow > 0) {
            for (var index = start; index < end; index++) {
                _baseMain[index] += free * (_items[index].Grow / totalGrow);
            }

            free = 0;
        }

        free = Math.Max(0, free);
        var (leading, between) = ResolveMainDistribution(
            JustifyContent,
            free,
            count,
            gap);
        var cursor = rowAxis
            ? (reverse ? content.Right - leading : content.X + leading)
            : (reverse ? content.Bottom - leading : content.Y + leading);

        for (var index = start; index < end; index++) {
            var style = _styles[index];
            var itemMain = Math.Max(0, _baseMain[index] - (style.Margin * 2));
            var crossAvailable = Math.Max(0, crossSize - (style.Margin * 2));
            var naturalCross = Math.Max(
                0,
                CrossSize(_measured[index], rowAxis) - (style.Margin * 2));
            var alignment = _items[index].AlignSelf == LayoutAlignment.Auto
                ? AlignItems
                : _items[index].AlignSelf;
            var crossLength = rowAxis ? style.Height : style.Width;
            var itemCross = crossLength.Unit != LengthUnit.Auto
                ? naturalCross
                : alignment == LayoutAlignment.Stretch
                    ? crossAvailable
                    : Math.Min(naturalCross, crossAvailable);
            var crossOffset = alignment switch {
                LayoutAlignment.Center => (crossAvailable - itemCross) / 2,
                LayoutAlignment.End => crossAvailable - itemCross,
                _ => 0,
            };
            var main = reverse
                ? cursor - style.Margin - itemMain
                : cursor + style.Margin;
            var cross = (rowAxis ? content.Y : content.X) + style.Margin + crossOffset;
            var bounds = rowAxis
                ? new Rect(main, cross, itemMain, itemCross)
                : new Rect(cross, main, itemCross, itemMain);
            context.ArrangeChild(children[index], bounds);
            cursor += (reverse ? -1 : 1) * (_baseMain[index] + between);
        }
    }

    private static (float Leading, float Between) ResolveMainDistribution(
        JustifyContent justify,
        float free,
        int count,
        float gap) {
        if (count == 0) {
            return (0, gap);
        }

        return justify switch {
            JustifyContent.End => (free, gap),
            JustifyContent.Center => (free / 2, gap),
            JustifyContent.SpaceBetween when count > 1 =>
                (0, gap + (free / (count - 1))),
            JustifyContent.SpaceAround =>
                (free / (count * 2), gap + (free / count)),
            JustifyContent.SpaceEvenly =>
                (free / (count + 1), gap + (free / (count + 1))),
            _ => (0, gap),
        };
    }
}
