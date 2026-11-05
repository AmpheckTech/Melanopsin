// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;

namespace Melanopsin.UI.Styling;

public sealed class InlineStyle {
    private Length? _width;
    private Length? _height;
    private Length? _minWidth;
    private Length? _maxWidth;
    private Length? _minHeight;
    private Length? _maxHeight;
    private float? _flexGrow;
    private float? _flexShrink;
    private float? _weight;
    private int? _order;
    private int? _gridRow;
    private int? _gridColumn;
    private int? _gridRowSpan;
    private int? _gridColumnSpan;
    private LayoutAlignment? _alignSelf;

    public event Action? Changed;

    public Length? Width {
        get => _width;
        set => Set(ref _width, value);
    }

    public Length? Height {
        get => _height;
        set => Set(ref _height, value);
    }

    public Length? MinWidth {
        get => _minWidth;
        set => Set(ref _minWidth, value);
    }

    public Length? MaxWidth {
        get => _maxWidth;
        set => Set(ref _maxWidth, value);
    }

    public Length? MinHeight {
        get => _minHeight;
        set => Set(ref _minHeight, value);
    }

    public Length? MaxHeight {
        get => _maxHeight;
        set => Set(ref _maxHeight, value);
    }

    public float? FlexGrow {
        get => _flexGrow;
        set => Set(ref _flexGrow, value);
    }

    public float? FlexShrink {
        get => _flexShrink;
        set => Set(ref _flexShrink, value);
    }

    public float? Weight {
        get => _weight;
        set => Set(ref _weight, value);
    }

    public int? Order {
        get => _order;
        set => Set(ref _order, value);
    }

    public int? GridRow {
        get => _gridRow;
        set => Set(ref _gridRow, value);
    }

    public int? GridColumn {
        get => _gridColumn;
        set => Set(ref _gridColumn, value);
    }

    public int? GridRowSpan {
        get => _gridRowSpan;
        set => Set(ref _gridRowSpan, value);
    }

    public int? GridColumnSpan {
        get => _gridColumnSpan;
        set => Set(ref _gridColumnSpan, value);
    }

    public LayoutAlignment? AlignSelf {
        get => _alignSelf;
        set => Set(ref _alignSelf, value);
    }

    /// <summary>
    /// Overwrites every value from <paramref name="source"/> and reports whether
    /// anything changed. A null source resets the style to its defaults, which
    /// is what a rebuild without inline values must do.
    /// </summary>
    internal bool CopyFrom(InlineStyle? source) {
        var changed = false;
        changed |= CopyValue(ref _width, source?._width);
        changed |= CopyValue(ref _height, source?._height);
        changed |= CopyValue(ref _minWidth, source?._minWidth);
        changed |= CopyValue(ref _maxWidth, source?._maxWidth);
        changed |= CopyValue(ref _minHeight, source?._minHeight);
        changed |= CopyValue(ref _maxHeight, source?._maxHeight);
        changed |= CopyValue(ref _flexGrow, source?._flexGrow);
        changed |= CopyValue(ref _flexShrink, source?._flexShrink);
        changed |= CopyValue(ref _weight, source?._weight);
        changed |= CopyValue(ref _order, source?._order);
        changed |= CopyValue(ref _gridRow, source?._gridRow);
        changed |= CopyValue(ref _gridColumn, source?._gridColumn);
        changed |= CopyValue(ref _gridRowSpan, source?._gridRowSpan);
        changed |= CopyValue(ref _gridColumnSpan, source?._gridColumnSpan);
        changed |= CopyValue(ref _alignSelf, source?._alignSelf);
        return changed;
    }

    internal bool TrySet(string name, object? value) {
        switch (name) {
            case "flex-grow":
                FlexGrow = ReadFloat(value);
                return FlexGrow is not null;
            case "flex-shrink":
                FlexShrink = ReadFloat(value);
                return FlexShrink is not null;
            case "weight":
                Weight = ReadFloat(value);
                return Weight is not null;
            case "order":
                Order = ReadInteger(value);
                return Order is not null;
            case "grid-row":
                GridRow = ReadInteger(value);
                return GridRow is not null;
            case "grid-column":
                GridColumn = ReadInteger(value);
                return GridColumn is not null;
            case "grid-row-span":
                GridRowSpan = ReadInteger(value);
                return GridRowSpan is not null;
            case "grid-column-span":
                GridColumnSpan = ReadInteger(value);
                return GridColumnSpan is not null;
            case "align-self":
                AlignSelf = ReadAlignment(value);
                return AlignSelf is not null;
        }

        return value is Length length && TrySetLength(name, length);
    }

    /// <summary>
    /// Reports whether a name can be represented by this style. Callers use this
    /// to avoid allocating a style for attributes owned by a control.
    /// </summary>
    internal static bool IsProperty(string name) {
        return name switch {
            "flex-grow" or "flex-shrink" or "weight" or "order" or
            "grid-row" or "grid-column" or "grid-row-span" or
            "grid-column-span" or "align-self" or "width" or "height" or
            "min-width" or "max-width" or "min-height" or "max-height" =>
                true,
            _ => false,
        };
    }

    private bool TrySetLength(string name, Length length) {

        switch (name) {
            case "width":
                Width = length;
                return true;
            case "height":
                Height = length;
                return true;
            case "min-width":
                MinWidth = length;
                return true;
            case "max-width":
                MaxWidth = length;
                return true;
            case "min-height":
                MinHeight = length;
                return true;
            case "max-height":
                MaxHeight = length;
                return true;
            default:
                return false;
        }
    }

    private void Set<T>(ref T field, T value) {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return;
        }

        field = value;
        Changed?.Invoke();
    }

    private static bool CopyValue<T>(ref T field, T value) {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return false;
        }

        field = value;
        return true;
    }

    private static float? ReadFloat(object? value) {
        if (value is null) {
            return null;
        }

        return value switch {
            float number => number,
            double number => (float)number,
            decimal number => (float)number,
            int number => number,
            _ => null,
        };
    }

    private static int? ReadInteger(object? value) {
        if (value is null) {
            return null;
        }

        return value is int number ? number : null;
    }

    private static LayoutAlignment? ReadAlignment(object? value) {
        return value as LayoutAlignment?;
    }
}
