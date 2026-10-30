using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Resources;

internal sealed record ResourceManifest(
    string Path,
    string Source,
    string Package,
    string AccessorClassName,
    IReadOnlyList<ResourceDefinition> Definitions,
    IReadOnlyList<MarkupDiagnostic> Diagnostics) {
    public bool IsValid => Diagnostics.Count == 0;

    public bool TryFind(
        ResourceReference reference,
        out ResourceDefinition definition) {
        foreach (var candidate in Definitions) {
            if (string.Equals(candidate.Id, reference.Id, StringComparison.Ordinal)) {
                definition = candidate;
                return true;
            }
        }

        definition = null!;
        return false;
    }
}
