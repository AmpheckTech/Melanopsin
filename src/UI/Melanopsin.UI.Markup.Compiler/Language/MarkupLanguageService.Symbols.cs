// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.Markup.Compiler.Language;

/// <summary>
/// Ends of the compiler the language features share: the component a document belongs to, the
/// members it exposes, and how a markup event is named.
/// </summary>
public static partial class MarkupLanguageService {
    private static INamedTypeSymbol? FindComponent(
        MarkupAnalysis analysis,
        Compilation compilation,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return string.IsNullOrWhiteSpace(analysis.Namespace) ||
               string.IsNullOrWhiteSpace(analysis.ComponentName)
            ? null
            : compilation.GetTypeByMetadataName(
                analysis.Namespace + "." + analysis.ComponentName);
    }

    /// <summary>
    /// Enumerates the members a view can reference. Private members stay hidden unless
    /// <paramref name="includePrivate"/> is set, which is the case inside a <c>@code</c> block
    /// where the component is addressing itself.
    /// </summary>
    private static IEnumerable<ISymbol> EnumerateMembers(
        INamedTypeSymbol type,
        bool includePrivate = false) {
        for (var current = type; current is not null; current = current.BaseType) {
            foreach (var member in current.GetMembers()) {
                if (member.IsImplicitlyDeclared) {
                    continue;
                }

                if (includePrivate ||
                    member.DeclaredAccessibility is Accessibility.Public or
                        Accessibility.Internal or Accessibility.Protected or
                        Accessibility.ProtectedOrInternal) {
                    yield return member;
                }
            }
        }
    }

    /// <summary>The member names a handler or interpolation may reference.</summary>
    private static IReadOnlyCollection<string> MemberNames(
        INamedTypeSymbol? component,
        bool includePrivate = false) {
        if (component is null) {
            return Array.Empty<string>();
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in EnumerateMembers(component, includePrivate)) {
            names.Add(member.Name);
        }

        return names;
    }

    /// <summary>
    /// The event an attribute name refers to - <c>onClick</c> names <c>Click</c> - or <c>null</c>
    /// when the name is not shaped like an event handler.
    /// </summary>
    private static string? EventNameOf(string attributeName) {
        return attributeName.Length > 2 &&
               attributeName.StartsWith("on", StringComparison.Ordinal) &&
               char.IsUpper(attributeName[2])
            ? attributeName.Substring(2)
            : null;
    }

    private static string EventDetail(MarkupEventInfo markupEvent) {
        var arguments = markupEvent.ParameterCount switch {
            0 => string.Empty,
            1 => markupEvent.ArgumentTypeName ?? "event argument",
            _ => markupEvent.ParameterCount + " arguments",
        };
        return $"Event · {arguments}";
    }
}
