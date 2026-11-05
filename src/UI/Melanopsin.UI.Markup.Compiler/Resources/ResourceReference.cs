// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.Markup.Compiler.Resources;

internal sealed record ResourceReference(string Category, string Name) {
    public string Id => $"{Category}.{Name}";

    public static bool TryParse(string value, out ResourceReference reference) {
        var separator = value.IndexOf('.');
        if (separator <= 0 ||
            separator != value.LastIndexOf('.') ||
            separator == value.Length - 1) {
            reference = null!;
            return false;
        }

        var category = value.Substring(0, separator);
        var name = value.Substring(separator + 1);
        if (!SyntaxFacts.IsValidIdentifier(category) ||
            !SyntaxFacts.IsValidIdentifier(name)) {
            reference = null!;
            return false;
        }

        reference = new ResourceReference(category, name);
        return true;
    }

    public override string ToString() {
        return Id;
    }
}
