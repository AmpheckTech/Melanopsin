namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// A reusable handle to the single frame held by a render target.
/// Frame members must be consumed before the lease is released. Release is
/// idempotent and stale-safe, so duplicate or late releases cannot release a
/// newer frame. Access to a frame's contents must not race with its release.
/// </summary>
public readonly struct RenderFrameLease {
    private readonly IRenderFrameLeaseOwner? _owner;
    private readonly long _generation;

    public RenderFrameLease(IRenderFrameLeaseOwner owner, long generation) {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(generation, 0);
        _owner = owner;
        _generation = generation;
    }

    public bool IsValid =>
        _owner is not null && _owner.IsLeaseValid(_generation);

    public long Sequence => Owner.GetSequence(_generation);

    public IRenderSurface Surface => Owner.GetSurface(_generation);

    public DamageRegion Damage => Owner.GetDamage(_generation);

    public RenderStatistics Statistics => Owner.GetStatistics(_generation);

    public AccessibilityVisualOptions VisualAccessibility =>
        Owner.GetVisualAccessibility(_generation);

    /// <summary>
    /// Ends this lease if it is still the active lease. Releasing an already
    /// released or stale lease has no effect and cannot release a newer frame.
    /// </summary>
    public void Release() {
        _owner?.ReleaseLease(_generation);
    }

    private IRenderFrameLeaseOwner Owner =>
        IsValid
            ? _owner!
            : throw new InvalidOperationException(
                "The render frame lease is stale or has already been released.");
}

/// <summary>
/// Supplies frame metadata and owns the frame slot represented by a lease.
/// Implementations must reject stale generations.
/// </summary>
public interface IRenderFrameLeaseOwner {
    bool IsLeaseValid(long generation);

    long GetSequence(long generation);

    IRenderSurface GetSurface(long generation);

    DamageRegion GetDamage(long generation);

    RenderStatistics GetStatistics(long generation);

    AccessibilityVisualOptions GetVisualAccessibility(long generation);

    void ReleaseLease(long generation);
}
