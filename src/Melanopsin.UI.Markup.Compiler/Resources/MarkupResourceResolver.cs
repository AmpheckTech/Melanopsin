namespace Melanopsin.UI.Markup.Compiler.Resources;

internal sealed class MarkupResourceResolver {
    private readonly ResourceManifestCatalog _manifests;

    public MarkupResourceResolver(ResourceManifestCatalog manifests) {
        _manifests = manifests;
    }

    public bool TryResolve(
        string? package,
        ResourceReference reference,
        out ResolvedResource resource) {
        var manifest = _manifests.Find(package);
        if (manifest is not null &&
            manifest.IsValid &&
            manifest.TryFind(reference, out var definition)) {
            resource = new ResolvedResource(manifest, definition);
            return true;
        }

        resource = null!;
        return false;
    }
}
