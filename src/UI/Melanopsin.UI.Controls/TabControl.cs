using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class TabControl : LinearLayout {
    private readonly FlexLayout _tabBar;
    private readonly List<TabItem> _tabs = [];
    private readonly Dictionary<TabItem, Button> _tabButtons = [];
    private int _selectedIndex = -1;

    public TabControl()
        : base("TabControl") {
        SetAttributeValue("orientation", "vertical");
        SetAttributeValue("spacing", "1su");
        Accessibility.Role = AccessibilityRole.TabList;
        _tabBar = new FlexLayout();
        _tabBar.SetAttributeValue("orientation", "horizontal");
        _tabBar.SetAttributeValue("column-gap", "1su");
        _tabBar.SetAttributeValue("height", "10su");
        base.AppendChild(_tabBar);
    }

    public event Action<int>? SelectionChanged;

    public int SelectedIndex => _selectedIndex;

    public IReadOnlyList<TabItem> Tabs => _tabs;

    public override void AppendChild(Node child) {
        if (child is not TabItem tab) {
            throw new ArgumentException("TabControl accepts only TabItem children.", nameof(child));
        }

        base.AppendChild(tab);
        _tabs.Add(tab);
        if (_selectedIndex < 0) {
            Select(0);
        } else {
            SyncTabBar();
        }
    }

    public override bool RemoveChild(Node child) {
        if (child is not TabItem tab || !base.RemoveChild(tab)) {
            return false;
        }

        _tabs.Remove(tab);
        NormalizeSelection();
        SyncTabBar();
        return true;
    }

    public override void ClearChildren() {
        if (_tabs.Count == 0) {
            return;
        }

        foreach (var tab in _tabs.ToArray()) {
            _ = base.RemoveChild(tab);
        }

        _tabs.Clear();
        _selectedIndex = -1;
        SyncTabBar();
    }

    public void Select(int index) {
        if (index < 0 || index >= _tabs.Count) {
            return;
        }

        _selectedIndex = index;
        for (var tabIndex = 0; tabIndex < _tabs.Count; tabIndex++) {
            _tabs[tabIndex].SetVisible(tabIndex == index);
            _tabs[tabIndex].SetState(
                UiStateNames.Selected,
                tabIndex == index);
        }

        SyncTabBar();
        SelectionChanged?.Invoke(index);
    }

    protected internal override void ReplaceChildren(
        IReadOnlyList<Node> children) {
        base.ReplaceChildren(children);
        var tabsChanged = !TabsMatch(children);
        if (tabsChanged) {
            _tabs.Clear();
            foreach (var child in children) {
                if (child is TabItem tab) {
                    _tabs.Add(tab);
                }
            }
        }

        NormalizeSelection();
        SyncTabBar();
    }

    private void SyncTabBar() {
        foreach (var pair in _tabButtons.ToArray()) {
            if (!_tabs.Contains(pair.Key)) {
                _tabBar.RemoveChild(pair.Value);
                _tabButtons.Remove(pair.Key);
            }
        }

        for (var index = 0; index < _tabs.Count; index++) {
            var tab = _tabs[index];
            if (!_tabButtons.TryGetValue(tab, out var button)) {
                button = new Button();
                button.SetAttributeValue("text", tab.Title);
                button.SetAttributeValue("class", "tab-button");
                button.SetAttributeValue("height", "32");
                button.Clicked += () => {
                    var selected = _tabs.IndexOf(tab);
                    if (selected >= 0) {
                        Select(selected);
                    }
                };
                _tabButtons[tab] = button;
                _tabBar.AppendChild(button);
            }

            SetAttributeIfChanged(button, "text", tab.Title);
            SetAttributeIfChanged(button, "class", "tab-button");
            SetAttributeIfChanged(button, "height", "32");
            button.SetState(UiStateNames.Selected, index == _selectedIndex);
            var currentIndex = _tabBar.IndexOfChild(button);
            if (currentIndex != index) {
                _tabBar.RemoveChild(button);
                _tabBar.InsertChild(
                    Math.Min(index, _tabBar.Children.Count),
                    button);
            }
        }
    }

    private static void SetAttributeIfChanged(
        Element element,
        string name,
        object value) {
        if (element.Attributes.TryGetValue(name, out var current) &&
            Equals(current, value)) {
            return;
        }

        element.SetAttributeValue(name, value);
    }

    private bool TabsMatch(IReadOnlyList<Node> children) {
        var index = 0;
        foreach (var child in children) {
            if (child is not TabItem tab) {
                continue;
            }

            if (index >= _tabs.Count ||
                !ReferenceEquals(_tabs[index], tab)) {
                return false;
            }

            index++;
        }

        return index == _tabs.Count;
    }

    private void NormalizeSelection() {
        if (_tabs.Count == 0) {
            _selectedIndex = -1;
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _tabs.Count - 1);
        for (var index = 0; index < _tabs.Count; index++) {
            _tabs[index].SetVisible(index == _selectedIndex);
        }
    }
}
