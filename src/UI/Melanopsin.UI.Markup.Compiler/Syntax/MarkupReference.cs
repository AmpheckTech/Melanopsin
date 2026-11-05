using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupReference(string Name, int Offset) {
    public static bool TryParse(
        MarkupAttribute attribute,
        out MarkupReference reference) {
        if (attribute.Parts.Count == 1) {
            var part = attribute.Parts[0];
            if (!part.IsExpression && !part.IsResource) {
                var name = part.Value.Trim();
                if (SyntaxFacts.IsValidIdentifier(name)) {
                    reference = new MarkupReference(name, attribute.Offset);
                    return true;
                }
            }
        }

        reference = null!;
        return false;
    }
}
