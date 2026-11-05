// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Parsing;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.Markup.Compiler.Validation;

internal static partial class MarkupValidator {
    public static IReadOnlyList<MarkupDiagnostic> Validate(
        MarkupDocument document,
        ControlCatalog controls) {
        var diagnostics = new List<MarkupDiagnostic>();
        if (document.Namespace is { Length: > 0 } &&
            SyntaxFactory.ParseName(document.Namespace).ContainsDiagnostics) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidMetadata,
                $"'{document.Namespace}' is not a valid C# namespace.",
                0,
                1));
        }

        if (document.ResourcePackage is { Length: > 0 } &&
            SyntaxFactory.ParseName(document.ResourcePackage).ContainsDiagnostics) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidMetadata,
                $"'{document.ResourcePackage}' is not a valid resource package name.",
                0,
                1));
        }

        var resolver = new MarkupControlResolver(document, controls);
        var referenceNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in document.Nodes) {
            ValidateNode(
                resolver,
                node,
                isDynamic: false,
                referenceNames,
                diagnostics);
        }

        return diagnostics;
    }

    private static void ValidateNode(
        MarkupControlResolver resolver,
        MarkupNode node,
        bool isDynamic,
        HashSet<string> referenceNames,
        List<MarkupDiagnostic> diagnostics) {
        switch (node) {
            case MarkupElement element:
                ValidateElement(
                    resolver,
                    element,
                    isDynamic,
                    referenceNames,
                    diagnostics);
                foreach (var child in element.Children) {
                    ValidateNode(
                        resolver,
                        child,
                        isDynamic,
                        referenceNames,
                        diagnostics);
                }

                break;

            case MarkupExpression expression:
                ValidateExpression(expression.Expression, expression.Offset, diagnostics);
                break;

            case MarkupIf conditional:
                ValidateExpression(conditional.Condition, conditional.Offset, diagnostics);
                foreach (var child in conditional.WhenTrue) {
                    ValidateNode(
                        resolver,
                        child,
                        isDynamic: true,
                        referenceNames,
                        diagnostics);
                }

                foreach (var child in conditional.WhenFalse) {
                    ValidateNode(
                        resolver,
                        child,
                        isDynamic: true,
                        referenceNames,
                        diagnostics);
                }

                break;

            case MarkupFor loop:
                ValidateExpression(loop.Iterable, loop.Offset, diagnostics);
                if (loop.Track is { Length: > 0 }) {
                    ValidateExpression(loop.Track, loop.Offset, diagnostics);
                }

                foreach (var child in loop.Body) {
                    ValidateNode(
                        resolver,
                        child,
                        isDynamic: true,
                        referenceNames,
                        diagnostics);
                }

                foreach (var child in loop.EmptyBody) {
                    ValidateNode(
                        resolver,
                        child,
                        isDynamic: true,
                        referenceNames,
                        diagnostics);
                }

                break;

            case MarkupSwitch switchBlock:
                ValidateExpression(
                    switchBlock.Expression,
                    switchBlock.Offset,
                    diagnostics);
                foreach (var switchCase in switchBlock.Cases) {
                    ValidateExpression(
                        switchCase.Pattern,
                        switchCase.Offset,
                        diagnostics);
                    foreach (var child in switchCase.Body) {
                        ValidateNode(
                            resolver,
                            child,
                            isDynamic: true,
                            referenceNames,
                            diagnostics);
                    }
                }

                foreach (var child in switchBlock.DefaultBody) {
                    ValidateNode(
                        resolver,
                        child,
                        isDynamic: true,
                        referenceNames,
                        diagnostics);
                }

                break;
        }
    }

    private static void ValidateExpression(
        string expression,
        int offset,
        List<MarkupDiagnostic> diagnostics) {
        if (expression.Length == 0) {
            return;
        }

        var parsed = SyntaxFactory.ParseExpression(expression);
        if (parsed.IsMissing || parsed.ContainsDiagnostics) {
            diagnostics.Add(new MarkupDiagnostic(
                MarkupDiagnosticCodes.InvalidExpression,
                $"Invalid C# expression '{expression}'.",
                offset,
                1));
        }
    }
}
