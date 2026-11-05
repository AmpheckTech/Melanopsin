using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Resources;

internal sealed class ResourceManifestCatalog {
    public static readonly ResourceManifestCatalog Empty = new(
        Array.Empty<ResourceManifest>(),
        new Dictionary<string, ResourceManifest>(StringComparer.Ordinal));

    private readonly Dictionary<string, ResourceManifest> _byPackage;

    private ResourceManifestCatalog(
        IReadOnlyList<ResourceManifest> manifests,
        Dictionary<string, ResourceManifest> byPackage) {
        Manifests = manifests;
        _byPackage = byPackage;
    }

    public IReadOnlyList<ResourceManifest> Manifests { get; }

    public static ResourceManifestCatalog Create(
        IReadOnlyList<ResourceManifest> manifests) {
        var byPackage = new Dictionary<string, ResourceManifest>(StringComparer.Ordinal);
        var normalized = new List<ResourceManifest>(manifests.Count);
        foreach (var candidate in manifests) {
            var manifest = candidate;
            var package = manifest.Package;
            if (manifest.IsValid &&
                package.Length != 0 &&
                byPackage.TryGetValue(package, out var existing)) {
                manifest = manifest with {
                    Diagnostics = [
                        .. manifest.Diagnostics,
                        new MarkupDiagnostic(
                            MarkupDiagnosticCodes.InvalidResourceManifest,
                            $"Resource package '{package}' is already declared by '{existing.Path}'.",
                            0,
                            1),
                    ],
                };
            }

            normalized.Add(manifest);
            if (manifest.IsValid && package.Length != 0) {
                byPackage[package] = manifest;
            }
        }

        return new ResourceManifestCatalog(normalized, byPackage);
    }

    public ResourceManifest? Find(string? package) {
        if (string.IsNullOrWhiteSpace(package)) {
            return null;
        }

        return _byPackage.TryGetValue(package!, out var manifest)
            ? manifest
            : null;
    }
}
