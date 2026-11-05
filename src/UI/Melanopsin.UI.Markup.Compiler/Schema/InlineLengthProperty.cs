// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler.Schema;

internal static class InlineLengthProperty {
    public static bool TryGet(string attributeName, out string propertyName) {
        propertyName = attributeName switch {
            "width" => "Width",
            "height" => "Height",
            "min-width" => "MinWidth",
            "max-width" => "MaxWidth",
            "min-height" => "MinHeight",
            "max-height" => "MaxHeight",
            _ => string.Empty,
        };
        return propertyName.Length != 0;
    }
}
