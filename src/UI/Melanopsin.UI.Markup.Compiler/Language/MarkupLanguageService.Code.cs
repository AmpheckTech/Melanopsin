using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Melanopsin.UI.Markup.Compiler.Generation;
using Melanopsin.UI.Markup.Compiler.Resources;
using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.Markup.Compiler.Language;

/// <summary>
/// The C# that markup declares - <c>@code</c> blocks, interpolations and event handlers - and the
/// generated tree that turns it into symbols.
/// </summary>
public static partial class MarkupLanguageService {
    /// <summary>
    /// Rebuilds the tree of every supplied document from its text, so member lookup and C# diagnostics
    /// follow unsaved editor buffers.
    /// </summary>
    public static Compilation ApplyMarkupBuffers(
        Compilation compilation,
        IReadOnlyList<MarkupSourceFile> buffers,
        IReadOnlyList<MarkupSourceFile> resources) {
        if (buffers.Count == 0) {
            return compilation;
        }

        return MarkupComponentTrees.Apply(
            compilation,
            GetControlCatalog(compilation, CancellationToken.None),
            ReadResourceManifests(resources),
            buffers,
            CancellationToken.None);
    }

    /// <summary>The tree whose symbols describe the component of a markup document.</summary>
    public static SyntaxTree? FindComponentTree(
        Compilation compilation,
        string path,
        bool buffered = false,
        CancellationToken cancellationToken = default) {
        return MarkupComponentTrees.Find(compilation, path, buffered, cancellationToken);
    }

    /// <summary>
    /// The spans of markup text that hold C#: the bodies of interpolations and the values of event
    /// handlers. <c>@code</c> blocks are left out, because their contents are described by symbols
    /// rather than by names.
    /// </summary>
    public static IReadOnlyList<MarkupCodeBlockInfo> GetEmbeddedCodeSpans(
        string path,
        string source,
        Compilation compilation,
        CancellationToken cancellationToken = default) {
        var analysis = Analyze(path, source, compilation, cancellationToken);
        var spans = new List<MarkupCodeBlockInfo>();
        foreach (var element in analysis.Elements.SelectMany(Flatten)) {
            if (element.Control is null || !TryFindOpenTag(source, element.Offset, out var tag)) {
                continue;
            }

            foreach (var attribute in tag.Attributes) {
                if (attribute.ValueStart >= 0 && IsEventHandler(element.Control, source, attribute)) {
                    spans.Add(new MarkupCodeBlockInfo(
                        attribute.ValueStart,
                        attribute.ValueEnd - attribute.ValueStart));
                }
            }
        }

        foreach (var interpolation in Interpolations(source, analysis.CodeBlocks)) {
            spans.Add(interpolation);
        }

        return spans;
    }

    private static bool IsEventHandler(
        MarkupControlInfo control,
        string source,
        AttributeSpan attribute) {
        if (attribute.NameEnd - attribute.NameStart <= 2) {
            return false;
        }

        var eventName = EventNameOf(source.Substring(
            attribute.NameStart,
            attribute.NameEnd - attribute.NameStart));
        return eventName is not null &&
               control.Events.Any(candidate => candidate.Name == eventName);
    }

    private static IEnumerable<MarkupCodeBlockInfo> Interpolations(
        string source,
        IReadOnlyList<MarkupCodeBlockInfo> codeBlocks) {
        var position = 0;
        while (position < source.Length) {
            var open = source.IndexOf("{{", position, StringComparison.Ordinal);
            if (open < 0) {
                yield break;
            }

            var close = source.IndexOf("}}", open + 2, StringComparison.Ordinal);
            var end = close < 0 ? source.Length : close;
            if (InsideSpans(codeBlocks, open)) {
                position = end;
                continue;
            }

            yield return new MarkupCodeBlockInfo(open + 2, end - open - 2);
            position = close < 0 ? source.Length : close + 2;
        }
    }

    private static bool InsideSpans(IReadOnlyList<MarkupCodeBlockInfo> blocks, int offset) {
        return blocks.Any(block => offset >= block.Offset && offset < block.Offset + block.Length);
    }

    private static ResourceManifestCatalog ReadResourceManifests(
        IReadOnlyList<MarkupSourceFile> resources) {
        if (resources.Count == 0) {
            return ResourceManifestCatalog.Empty;
        }

        var manifests = new List<ResourceManifest>(resources.Count);
        foreach (var resource in resources) {
            manifests.Add(ResourceManifestParser.Parse(resource.Path, resource.Text));
        }

        return ResourceManifestCatalog.Create(manifests);
    }
}
