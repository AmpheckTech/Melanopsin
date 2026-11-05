// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Controls;

public sealed partial class ComboBox {
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
}
