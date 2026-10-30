using Melanopsin.UI;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Rendering;

/// <summary>
/// Converts a window layout snapshot into an explicit backend-neutral layer
/// tree. Backends never inspect controls or layout rules.
/// </summary>
public sealed class DefaultRenderSceneComposer : IRenderSceneComposer {
    private static readonly RenderLayer EmptyLayer = new ContainerLayer([]);
    private readonly StyleEngine _styleEngine;
    private readonly ITextLayout? _textLayout;
    private readonly Dictionary<Node, BoundaryLayerCache> _boundaryCaches = [];

    public DefaultRenderSceneComposer(
        StyleEngine? styleEngine = null,
        ITextLayout? textLayout = null) {
        _styleEngine = styleEngine ?? new StyleEngine();
        _textLayout = textLayout;
    }

    public RenderScene Compose(
        LayoutSnapshot layout,
        RenderCompositionContext context) {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            context.Options.ScaleFactor);

        var reusedBoundaries = 0;
        var repaintedBoundaries = 0;
        var rootLayer = AppendNode(
            layout.Root,
            context.Options,
            context.InvalidatePaintCache,
            isRoot: true,
            ref reusedBoundaries,
            ref repaintedBoundaries);
        RemoveStaleCaches(layout);
        return new RenderScene(
            layout.Size,
            context.Options.ClearColor,
            rootLayer,
            context.Damage,
            new RenderStatistics(
                rootLayer.CountCommands(),
                reusedBoundaries,
                repaintedBoundaries),
            context.Options.VisualAccessibility);
    }

    private RenderLayer AppendNode(
        LayoutNode layoutNode,
        RenderOptions options,
        bool invalidatePaintCache,
        bool isRoot,
        ref int reusedBoundaries,
        ref int repaintedBoundaries) {
        var node = layoutNode.Node;
        if (isRoot || node.IsRepaintBoundary || node.IsRelayoutBoundary) {
            return AppendBoundary(
                layoutNode,
                options,
                invalidatePaintCache,
                cacheRaster: !isRoot &&
                    node.IsRepaintBoundary &&
                    node.AllowsRasterCache,
                ref reusedBoundaries,
                ref repaintedBoundaries);
        }

        return AppendNodeContent(
            layoutNode,
            options,
            invalidatePaintCache: false,
            ref reusedBoundaries,
            ref repaintedBoundaries);
    }

    private RenderLayer AppendBoundary(
        LayoutNode layoutNode,
        RenderOptions options,
        bool invalidatePaintCache,
        bool cacheRaster,
        ref int reusedBoundaries,
        ref int repaintedBoundaries) {
        var node = layoutNode.Node;
        if (!invalidatePaintCache &&
            _boundaryCaches.TryGetValue(node, out var cache) &&
            cache.IsValid(layoutNode, options)) {
            reusedBoundaries++;
            return cache.Layer;
        }

        repaintedBoundaries++;
        var contentLayer = AppendNodeContent(
            layoutNode,
            options,
            invalidatePaintCache,
            ref reusedBoundaries,
            ref repaintedBoundaries);
        var layer = cacheRaster
            ? new RetainedLayer(layoutNode.Bounds, contentLayer)
            : contentLayer;
        _boundaryCaches[node] = new BoundaryLayerCache(
            node.SubtreePaintRevision,
            layoutNode.Bounds,
            layoutNode.ClipBounds,
            options.DrawDebugBounds,
            layer);
        return layer;
    }

    private RenderLayer AppendNodeContent(
        LayoutNode layoutNode,
        RenderOptions options,
        bool invalidatePaintCache,
        ref int reusedBoundaries,
        ref int repaintedBoundaries) {
        var node = layoutNode.Node;
        if (node is Control { IsVisible: false }) {
            return EmptyLayer;
        }

        var bounds = layoutNode.Bounds;
        var clipBounds = layoutNode.ClipBounds;
        var scrollTransform = node as IScrollTransform;
        var parentIsScrollTransform = layoutNode.Parent?.Node is IScrollTransform;
        var addedClip = scrollTransform is not null ||
            !parentIsScrollTransform &&
            clipBounds is not null &&
            layoutNode.Parent?.ClipBounds != clipBounds;
        var clip = addedClip
            ? scrollTransform is not null
                ? bounds
                : clipBounds!.Value
            : (Rect?)null;

        var commands = new List<RenderCommand>();
        if (node is TextNode text) {
            AppendTextNode(commands, text, bounds: bounds);
            return WrapLayer(commands, [], clip);
        }

        if (node is not Element element) {
            return EmptyLayer;
        }

        AppendElementContent(
            commands,
            element,
            bounds,
            options);

        var children = new List<RenderLayer>(layoutNode.Children.Count);
        foreach (var child in layoutNode.Children) {
            var childLayer = AppendNode(
                child,
                options,
                invalidatePaintCache,
                isRoot: false,
                ref reusedBoundaries,
                ref repaintedBoundaries);
            if (!ReferenceEquals(childLayer, EmptyLayer)) {
                children.Add(childLayer);
            }
        }

        return WrapLayer(
            commands,
            children,
            clip,
            scrollTransform);
    }

    private void AppendElementContent(
        List<RenderCommand> commands,
        Element element,
        Rect bounds,
        RenderOptions options) {
        var style = _styleEngine.ResolveReadOnly(element);
        var background = style.Background;
        var genericBackground = ControlValueParser.ReadColor(
            element.Attributes.GetValueOrDefault("background"));
        if (element is Control { Background: { } explicitBackground }) {
            background = explicitBackground;
        } else if (genericBackground is { } parsedBackground) {
            background = parsedBackground;
        }

        if (background.Alpha != 0) {
            commands.Add(new FillRectangleCommand(
                bounds,
                ApplyOpacity(background, style.Opacity),
                style.BorderRadius));
        }

        if (style.BorderWidth > 0 && style.BorderColor.Alpha != 0) {
            commands.Add(new StrokeRectangleCommand(
                bounds,
                ApplyOpacity(style.BorderColor, style.Opacity),
                style.BorderWidth,
                style.BorderRadius));
        }

        if (options.DrawDebugBounds) {
            commands.Add(new StrokeRectangleCommand(
                bounds,
                new Color(80, 180, 255, 180),
                strokeWidth: 1,
                cornerRadius: style.BorderRadius));
        }

        if (element is ITextContentControl { Text.Length: > 0 } contentControl &&
            element is not ICustomTextRendering) {
            AppendControlText(
                commands,
                contentControl,
                bounds,
                style,
                wrap: element switch {
                    ISingleLineTextContent => false,
                    ITextContentWrapPolicy policy => policy.WrapText,
                    _ => true,
                });
        }

        if (element is IRenderCommandProvider commandProvider) {
            commandProvider.AppendRenderCommands(
                new RenderCommandContext(
                    bounds,
                    style.BorderRadius,
                    ApplyOpacity(style.Foreground, style.Opacity),
                    style.FontSize,
                    style.Padding,
                    style.Font,
                    _textLayout),
                commands);
        }
    }

    private void AppendTextNode(
        List<RenderCommand> commands,
        TextNode text,
        Rect bounds) {
        var fontSize = Math.Max(
            _styleEngine.Metrics.Font(10),
            Math.Min(
                _styleEngine.Metrics.Font(UiMetrics.DefaultFontSize),
                bounds.Height - _styleEngine.Metrics.Space(1)));
        if (_textLayout is not null) {
            var shaped = _textLayout.Layout(
                text.Text,
                new TextLayoutOptions(fontSize, Math.Max(1, bounds.Width)));
            AppendShapedText(
                commands,
                shaped,
                bounds,
                new Color(232, 238, 242),
                TextHorizontalAlignment.Start,
                TextVerticalAlignment.Center);
        } else {
            commands.Add(new DrawTextCommand(
                text.Text,
                bounds,
                new Color(232, 238, 242),
                fontSize,
                TextHorizontalAlignment.Start,
                TextVerticalAlignment.Center));
        }
    }

    private void AppendControlText(
        List<RenderCommand> commands,
        ITextContentControl control,
        Rect bounds,
        ResolvedStyle style,
        bool wrap) {
        var textBounds = new Rect(
            bounds.X + style.Padding,
            bounds.Y,
            Math.Max(1, bounds.Width - (style.Padding * 2)),
            bounds.Height);
        var fontSize = Math.Max(
            _styleEngine.Metrics.Font(10),
            Math.Min(
                style.FontSize,
                bounds.Height - _styleEngine.Metrics.Space(1)));
        if (_textLayout is not null) {
            var displayText = wrap
                ? control.Text
                : TextFitting.FitSingleLine(
                    _textLayout,
                    control.Text,
                    fontSize,
                    style.Font,
                    textBounds.Width);
            var shaped = _textLayout.Layout(
                displayText,
                new TextLayoutOptions(
                    fontSize,
                    wrap ? textBounds.Width : float.PositiveInfinity,
                    font: style.Font));
            AppendShapedText(
                commands,
                shaped,
                textBounds,
                ApplyOpacity(style.Foreground, style.Opacity),
                style.TextHorizontalAlignment,
                style.TextVerticalAlignment);
        } else {
            commands.Add(new DrawTextCommand(
                control.Text,
                textBounds,
                ApplyOpacity(style.Foreground, style.Opacity),
                fontSize,
                style.TextHorizontalAlignment,
                style.TextVerticalAlignment,
                style.Font));
        }
    }

    private static RenderLayer WrapLayer(
        List<RenderCommand> commands,
        List<RenderLayer> children,
        Rect? clip,
        IScrollTransform? scrollTransform = null) {
        var layers = new List<RenderLayer>(2);
        if (commands.Count != 0) {
            layers.Add(new ContentLayer(commands));
        }

        if (children.Count != 0) {
            RenderLayer childLayer = children.Count == 1
                ? children[0]
                : new ContainerLayer(children);
            if (scrollTransform is not null &&
                (scrollTransform.OffsetX != 0 || scrollTransform.OffsetY != 0)) {
                childLayer = new TransformLayer(
                    -scrollTransform.OffsetX,
                    -scrollTransform.OffsetY,
                    childLayer);
            }

            layers.Add(childLayer);
        }

        RenderLayer result = layers.Count switch {
            0 => EmptyLayer,
            1 => layers[0],
            _ => new ContainerLayer(layers),
        };
        return clip is { } bounds
            ? new ClipLayer(bounds, result)
            : result;
    }

    private static Color ApplyOpacity(Color color, float opacity) {
        return new Color(
            color.Red,
            color.Green,
            color.Blue,
            (byte)Math.Clamp(color.Alpha * opacity, 0, 255));
    }

    private static void AppendShapedText(
        List<RenderCommand> commands,
        ShapedText text,
        Rect bounds,
        Color color,
        TextHorizontalAlignment horizontalAlignment,
        TextVerticalAlignment verticalAlignment) {
        var top = verticalAlignment switch {
            TextVerticalAlignment.Top => bounds.Y,
            TextVerticalAlignment.Bottom => bounds.Bottom - text.Height,
            _ => bounds.Y + ((bounds.Height - text.Height) / 2),
        };
        foreach (var line in text.Lines) {
            var lineWidth = Math.Max(0, line.Width);
            var lineX = horizontalAlignment switch {
                TextHorizontalAlignment.Center => (bounds.Width - lineWidth) / 2,
                TextHorizontalAlignment.End => bounds.Width - lineWidth,
                _ => 0,
            };
            foreach (var run in line.Runs) {
                commands.Add(new DrawGlyphRunCommand(
                    new Rect(
                        bounds.X + lineX,
                        top,
                        lineWidth,
                        line.Height),
                    run,
                    top + line.Ascent,
                    color,
                    bounds.X + lineX));
            }

            top += line.Height;
        }
    }

    private void RemoveStaleCaches(LayoutSnapshot layout) {
        if (_boundaryCaches.Count == 0) {
            return;
        }

        List<Node>? stale = null;
        foreach (var node in _boundaryCaches.Keys) {
            if (layout.Contains(node)) {
                continue;
            }

            stale ??= [];
            stale.Add(node);
        }

        if (stale is null) {
            return;
        }

        foreach (var node in stale) {
            _boundaryCaches.Remove(node);
        }
    }

}
