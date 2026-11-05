namespace Melanopsin.UI.Accessibility;

public sealed class AccessibilityNode {
    public AccessibilityNode(
        AccessibilityNodeId id,
        string? name,
        AccessibilityRole role,
        Rect bounds,
        bool isFocused,
        string? description = null,
        string? value = null,
        AccessibilityState state = AccessibilityState.None,
        AccessibilityAction actions = AccessibilityAction.None) {
        Id = id;
        Name = name;
        Role = role;
        Bounds = bounds;
        IsFocused = isFocused;
        Description = description;
        Value = value;
        State = state;
        Actions = actions;
    }

    public AccessibilityNodeId Id { get; }

    public string? Name { get; }

    public AccessibilityRole Role { get; }

    public Rect Bounds { get; }

    public bool IsFocused { get; }

    public string? Description { get; }

    public string? Value { get; }

    public AccessibilityState State { get; }

    public AccessibilityAction Actions { get; }

    public List<AccessibilityNode> Children { get; } = [];

    public bool HasState(AccessibilityState state) {
        return (State & state) == state;
    }
}
