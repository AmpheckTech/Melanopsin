// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;
using Melanopsin.UI;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Demo.Views;

public sealed record StressItem(int Index, string Title, string Detail, string Tag) {
    public Length ChipWidth => Length.Su(4f + (Tag.Length * 2f) + ((Index % 3) * 1.5f));
}

[MarkupComponent]
public sealed partial class LayoutSample : Component {
    private const int MinimumNodes = 20;
    private const int MaximumNodes = 400;
    private const int NodeStep = 80;
    private const string FlowMode = "flow";
    private const string GridMode = "grid";
    private const string NestedMode = "nested";
    private static readonly string[] Tags = [
        "measure",
        "arrange",
        "wrap",
        "flex",
        "grid",
        "reflow",
        "paint",
        "damage",
    ];

    private string _mode = FlowMode;
    private IReadOnlyList<StressItem> _items;

    public LayoutSample() {
        _items = CreateItems(MaximumNodes / 2);
    }

    public bool ShowFlow => _mode == FlowMode;

    public bool ShowGrid => _mode == GridMode;

    public bool ShowNested => _mode == NestedMode;

    public string FlowButtonClass => ModeButtonClass(FlowMode);

    public string GridButtonClass => ModeButtonClass(GridMode);

    public string NestedButtonClass => ModeButtonClass(NestedMode);

    public IReadOnlyList<StressItem> StressItems => _items;

    public string StressSummary =>
        $"{_items.Count} items · {_mode} layout · resize to exercise reflow";

    public void SelectFlow() => Select(FlowMode);

    public void SelectGrid() => Select(GridMode);

    public void SelectNested() => Select(NestedMode);

    public void AddStressNodes() {
        var count = Math.Min(MaximumNodes, _items.Count + NodeStep);
        if (count != _items.Count) {
            _items = CreateItems(count);
        }
    }

    public void RemoveStressNodes() {
        var count = Math.Max(MinimumNodes, _items.Count - NodeStep);
        if (count != _items.Count) {
            _items = CreateItems(count);
        }
    }

    private void Select(string mode) {
        _mode = mode;
    }

    private string ModeButtonClass(string mode) =>
        _mode == mode ? "quiet-action selected" : "quiet-action";

    private static IReadOnlyList<StressItem> CreateItems(int count) {
        var items = new StressItem[count];
        for (var index = 0; index < count; index++) {
            var tag = Tags[index % Tags.Length];
            items[index] = new StressItem(
                index,
                $"Node {index + 1:000}",
                $"{tag} · {20 + ((index * 17) % 280)} px",
                tag);
        }

        return items;
    }
}
