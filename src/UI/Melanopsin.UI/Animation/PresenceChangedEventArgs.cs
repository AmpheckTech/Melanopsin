namespace Melanopsin.UI;

public sealed class PresenceChangedEventArgs : EventArgs {
    public PresenceChangedEventArgs(
        PresenceChangeKind kind,
        Node node,
        int oldIndex,
        int newIndex) {
        Kind = kind;
        Node = node;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    public PresenceChangeKind Kind { get; }

    public Node Node { get; }

    public int OldIndex { get; }

    public int NewIndex { get; }
}
