namespace Melanopsin.UI.Rendering;

public readonly record struct RenderCompositionContext {
    public RenderCompositionContext(
        RenderOptions options,
        DamageRegion damage,
        bool invalidatePaintCache = false) {
        ArgumentNullException.ThrowIfNull(damage);
        Options = options;
        Damage = damage;
        InvalidatePaintCache = invalidatePaintCache;
    }

    public RenderOptions Options { get; }

    public DamageRegion Damage { get; }

    public bool InvalidatePaintCache { get; }

}
