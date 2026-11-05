// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler.Resources;

internal sealed record ResolvedResource(
    ResourceManifest Manifest,
    ResourceDefinition Definition) {
    public string KeyExpression =>
        $"global::{Manifest.Package}.{Manifest.AccessorClassName}." +
        $"{Definition.Category}.{Definition.Name}";
}
