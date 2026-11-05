// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Melanopsin.UI.Markup.Compiler.Language;
using Melanopsin.UI.Markup.Compiler.Parsing;
using Melanopsin.UI.Markup.Compiler.Resources;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Melanopsin.UI.Markup.Compiler.Generation;

/// <summary>
/// The syntax tree that mirrors the component generated for a markup document, rebuilt from the text
/// the editor shows so member queries answer for the buffer instead of the last build.
/// </summary>
internal static class MarkupComponentTrees {
    /// <summary>File name suffix of a tree emitted from an editor buffer.</summary>
    public const string BufferSuffix = ".g.cs";

    private static readonly ConditionalWeakTable<SyntaxTree, string?> Paths = new();

    /// <summary>Replaces the tree of each document with one emitted from its text.</summary>
    public static Compilation Apply(
        Compilation compilation,
        ControlCatalog controls,
        ResourceManifestCatalog resources,
        IReadOnlyList<MarkupSourceFile> buffers,
        CancellationToken cancellationToken) {
        foreach (var buffer in buffers) {
            cancellationToken.ThrowIfCancellationRequested();
            var replaced = Replace(compilation, controls, resources, buffer, cancellationToken);
            if (replaced is not null) {
                compilation = replaced;
            }
        }

        return compilation;
    }

    /// <summary>
    /// The tree a document's symbols are read from. A tree emitted for the buffer wins; otherwise
    /// the tree the build produced is used. <paramref name="buffered"/> rejects the latter, because
    /// only positions of a tree emitted from the buffer map onto that buffer.
    /// </summary>
    public static SyntaxTree? Find(
        Compilation compilation,
        string path,
        bool buffered,
        CancellationToken cancellationToken) {
        SyntaxTree? built = null;
        foreach (var tree in compilation.SyntaxTrees) {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsBufferedTree(tree, path)) {
                return tree;
            }

            if (!buffered && built is null && MappedPath(tree, cancellationToken) == path) {
                built = tree;
            }
        }

        return built;
    }

    private static Compilation? Replace(
        Compilation compilation,
        ControlCatalog controls,
        ResourceManifestCatalog resources,
        MarkupSourceFile buffer,
        CancellationToken cancellationToken) {
        var stale = StaleTrees(compilation, buffer.Path, cancellationToken);
        var without = stale.Count == 0 ? compilation : compilation.RemoveSyntaxTrees(stale);
        var tree = Create(without, controls, resources, buffer);
        return tree is null ? null : without.AddSyntaxTrees(tree);
    }

    private static SyntaxTree? Create(
        Compilation compilation,
        ControlCatalog controls,
        ResourceManifestCatalog resources,
        MarkupSourceFile buffer) {
        var document = MarkupParser.Parse(buffer.Path, buffer.Text);
        if (document.Namespace is not { Length: > 0 } namespaceName ||
            document.Nodes.Count != 1 ||
            document.Nodes[0] is not MarkupElement) {
            // Metadata or the element tree is missing; the parser reports why on its own.
            return null;
        }

        var className = MarkupNaming.ComponentClassName(document);
        var declaresComponent = compilation.GetTypeByMetadataName(
            $"{namespaceName}.{className}") is null;
        string source;
        try {
            source = MarkupEmitter.Emit(document, controls, resources, declaresComponent);
        } catch (Exception) {
            // Unresolved elements or resources: keep the tree the build produced instead of
            // publishing symbols for code that cannot be generated.
            return null;
        }

        return CSharpSyntaxTree.ParseText(
            source,
            ParseOptions(compilation),
            buffer.Path + BufferSuffix);
    }

    private static IReadOnlyList<SyntaxTree> StaleTrees(
        Compilation compilation,
        string path,
        CancellationToken cancellationToken) {
        List<SyntaxTree>? stale = null;
        foreach (var tree in compilation.SyntaxTrees) {
            cancellationToken.ThrowIfCancellationRequested();
            if (MappedPath(tree, cancellationToken) == path) {
                (stale ??= []).Add(tree);
            }
        }

        return stale ?? (IReadOnlyList<SyntaxTree>)Array.Empty<SyntaxTree>();
    }

    private static bool IsBufferedTree(SyntaxTree tree, string path) {
        var file = tree.FilePath;
        return file.Length == path.Length + BufferSuffix.Length &&
               file.StartsWith(path, StringComparison.Ordinal) &&
               file.EndsWith(BufferSuffix, StringComparison.Ordinal);
    }

    /// <summary>
    /// The markup document a syntax tree mirrors: the buffer suffix of a tree emitted for an
    /// editor, the path of a tree declaring a markup-only component, or the <c>#line</c> target of
    /// generated code. The answer is memoized because every request asks the same question.
    /// </summary>
    private static string? MappedPath(SyntaxTree tree, CancellationToken cancellationToken) {
        return Paths.GetValue(tree, _ => ComputePath(tree, cancellationToken));
    }

    private static string? ComputePath(SyntaxTree tree, CancellationToken cancellationToken) {
        var file = tree.FilePath;
        if (file.Length > BufferSuffix.Length &&
            file.EndsWith(BufferSuffix, StringComparison.Ordinal)) {
            var document = file.Substring(0, file.Length - BufferSuffix.Length);
            if (document.EndsWith(".mxml", StringComparison.OrdinalIgnoreCase)) {
                return document;
            }
        }

        if (file.EndsWith(".mxml", StringComparison.OrdinalIgnoreCase)) {
            return file;
        }

        foreach (var trivia in tree.GetRoot(cancellationToken)
                     .DescendantTrivia(descendIntoTrivia: true)) {
            if (trivia.GetStructure() is LineDirectiveTriviaSyntax directive &&
                directive.File.ValueText is { Length: > 0 } target) {
                return target;
            }
        }

        return null;
    }

    /// <summary>
    /// Every token of a tree whose <c>#line</c> mapping lands inside a markup document, with the span
    /// it occupies there.
    /// </summary>
    public static IEnumerable<(SyntaxToken Token, int Offset, int Length)> DocumentTokens(
        SyntaxTree tree,
        string path,
        string source,
        CancellationToken cancellationToken) {
        var text = SourceText.From(source);
        foreach (var token in tree.GetRoot(cancellationToken).DescendantTokens()) {
            if (token.Span.Length == 0) {
                continue;
            }

            var mapped = token.GetLocation().GetMappedLineSpan();
            if (!mapped.HasMappedPath ||
                !string.Equals(mapped.Path, path, StringComparison.Ordinal)) {
                continue;
            }

            if (Offset(text, mapped.StartLinePosition) is not { } start ||
                Offset(text, mapped.EndLinePosition) is not { } end ||
                end - start != token.Text.Length ||
                !MarkupEventBinding.TextMatches(
                    token.Text,
                    text.ToString(TextSpan.FromBounds(start, end)))) {
                continue;
            }

            yield return (token, start, end - start);
        }
    }

    private static int? Offset(SourceText text, LinePosition position) {
        if (position.Line < 0 || position.Line >= text.Lines.Count) {
            return null;
        }

        var line = text.Lines[position.Line];
        return line.Start + Math.Min(position.Character, line.Span.Length);
    }

    /// <summary>
    /// A tree joining a compilation has to share its parse options; language version and
    /// preprocessor state come from the compilation itself.
    /// </summary>
    public static CSharpParseOptions ParseOptions(Compilation compilation) {
        var options = compilation.SyntaxTrees
            .Select(static tree => tree.Options)
            .OfType<CSharpParseOptions>()
            .FirstOrDefault();
        return options is null
            ? new CSharpParseOptions(LanguageVersion.Latest)
            : options.WithKind(SourceCodeKind.Regular);
    }
}
