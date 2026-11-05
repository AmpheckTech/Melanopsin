using Melanopsin.UI.Markup;
using Melanopsin.UI.Motion;

namespace Melanopsin.UI;

/// <summary>
/// A transparent boundary that reports child entry, exit and movement. Exiting
/// children can be retained for application-defined styling before removal.
/// </summary>
[MarkupElement]
public sealed class PresenceScope : Fragment {
    private readonly Dictionary<Node, AnimationTimeline> _entering =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Node, AnimationTimeline> _exiting =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Node, AnimationTimeline> _moving =
        new(ReferenceEqualityComparer.Instance);

    public PresenceScope()
        : base() {
    }

    public event Action<PresenceChangedEventArgs>? Changed;

    /// <summary>
    /// Keeps the entering state active for this duration. Applications can use
    /// the state in selectors or render code before it clears.
    /// </summary>
    public UiDuration EnterDuration { get; set; }

    /// <summary>
    /// Retains exiting children for this duration. A zero duration removes them
    /// during reconciliation, preserving the default retained-tree behavior.
    /// </summary>
    public UiDuration ExitDuration { get; set; }

    /// <summary>
    /// Keeps the moving state active for this duration. Layout position itself is
    /// still owned by the layout engine; the state is available to visual effects.
    /// </summary>
    public UiDuration MoveDuration { get; set; }

    public IReadOnlyCollection<Node> ExitingNodes => _exiting.Keys;

    public bool HasActivePresence =>
        _entering.Count != 0 || _exiting.Count != 0 || _moving.Count != 0;

    protected internal override void OnMounted() {
        base.OnMounted();
        foreach (var child in Children) {
            BeginEnter(child);
        }
    }

    protected internal override void OnUnmounted() {
        _entering.Clear();
        _exiting.Clear();
        _moving.Clear();
        base.OnUnmounted();
    }

    public override void InsertChild(int index, Node child) {
        base.InsertChild(index, child);
        if (IsMounted) {
            BeginEnter(child);
        }
    }

    public override bool RemoveChild(Node child) {
        ArgumentNullException.ThrowIfNull(child);
        if (ExitDuration.Milliseconds <= 0) {
            return base.RemoveChild(child);
        }

        if (!ReferenceEquals(child.Parent, this)) {
            return false;
        }

        if (!_exiting.ContainsKey(child)) {
            BeginExit(child, IndexOfChild(child));
        }

        return true;
    }

    public override void ClearChildren() {
        if (ExitDuration.Milliseconds <= 0 || !IsMounted) {
            base.ClearChildren();
            return;
        }

        for (var index = Children.Count - 1; index >= 0; index--) {
            var child = Children[index];
            if (!_exiting.ContainsKey(child)) {
                BeginExit(child, index);
            }
        }
    }

    protected internal override void ReplaceChildren(
        IReadOnlyList<Node> children) {
        ArgumentNullException.ThrowIfNull(children);
        var previous = Children.ToList();
        var previousIndices = new Dictionary<Node, int>(
            previous.Count,
            ReferenceEqualityComparer.Instance);
        for (var index = 0; index < previous.Count; index++) {
            previousIndices[previous[index]] = index;
        }

        var incoming = new HashSet<Node>(
            children,
            ReferenceEqualityComparer.Instance);
        var retained = new List<Node>(children.Count + _exiting.Count);
        retained.AddRange(children);
        foreach (var child in previous) {
            if (!incoming.Contains(child)) {
                var isExiting = _exiting.ContainsKey(child);
                if (isExiting || BeginExit(child, previousIndices[child])) {
                    retained.Add(child);
                }

                continue;
            }

            if (_exiting.ContainsKey(child)) {
                CancelExit(child);
            }
        }

        base.ReplaceChildren(retained);
        for (var index = 0; index < children.Count; index++) {
            var child = children[index];
            if (!previousIndices.TryGetValue(child, out var oldIndex)) {
                BeginEnter(child);
            } else if (oldIndex != index) {
                BeginMove(child, oldIndex, index);
            }
        }
    }

    internal bool AdvanceAnimation(float elapsedSeconds) {
        var changed = false;
        foreach (var entry in _entering.ToArray()) {
            changed = true;
            _ = entry.Value.Advance(elapsedSeconds);
            if (entry.Value.IsComplete) {
                _entering.Remove(entry.Key);
                entry.Key.SetState(UiStateNames.Entering, false);
            }
        }

        foreach (var entry in _moving.ToArray()) {
            changed = true;
            _ = entry.Value.Advance(elapsedSeconds);
            if (entry.Value.IsComplete) {
                _moving.Remove(entry.Key);
                entry.Key.SetState(UiStateNames.Moving, false);
            }
        }

        foreach (var entry in _exiting.ToArray()) {
            changed = true;
            _ = entry.Value.Advance(elapsedSeconds);
            if (!entry.Value.IsComplete) {
                continue;
            }

            _exiting.Remove(entry.Key);
            entry.Key.SetState(UiStateNames.Exiting, false);
            _ = base.RemoveChild(entry.Key);
        }

        return changed;
    }

    private void BeginEnter(Node node) {
        Raise(PresenceChangeKind.Enter, node, -1, IndexOfChild(node));
        if (EnterDuration.Milliseconds <= 0) {
            return;
        }

        _entering[node] = StartTimeline(EnterDuration);
        node.SetState(UiStateNames.Entering, true);
    }

    private bool BeginExit(Node node, int oldIndex) {
        Raise(PresenceChangeKind.Exit, node, oldIndex, -1);
        if (ExitDuration.Milliseconds <= 0) {
            return false;
        }

        _exiting[node] = StartTimeline(ExitDuration);
        node.SetState(UiStateNames.Exiting, true);
        return true;
    }

    private void BeginMove(Node node, int oldIndex, int newIndex) {
        Raise(PresenceChangeKind.Move, node, oldIndex, newIndex);
        if (MoveDuration.Milliseconds <= 0) {
            return;
        }

        _moving[node] = StartTimeline(MoveDuration);
        node.SetState(UiStateNames.Moving, true);
    }

    private void CancelExit(Node node) {
        _exiting.Remove(node);
        node.SetState(UiStateNames.Exiting, false);
    }

    private void Raise(
        PresenceChangeKind kind,
        Node node,
        int oldIndex,
        int newIndex) {
        Changed?.Invoke(new PresenceChangedEventArgs(
            kind,
            node,
            oldIndex,
            newIndex));
    }

    private static AnimationTimeline StartTimeline(UiDuration duration) {
        var timeline = new AnimationTimeline(
            TimeSpan.FromMilliseconds(Math.Max(0, duration.Milliseconds)));
        timeline.Play();
        return timeline;
    }
}
