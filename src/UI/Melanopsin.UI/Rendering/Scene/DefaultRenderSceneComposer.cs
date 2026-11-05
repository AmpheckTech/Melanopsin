// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Rendering;

/// <summary>
/// Converts a window layout snapshot into an explicit backend-neutral layer
/// tree. Backends never inspect controls or layout rules.
/// </summary>
public sealed partial class DefaultRenderSceneComposer : IRenderSceneComposer {
    private static readonly RenderLayer EmptyLayer = new ContainerLayer([]);
    private readonly StyleEngine _styleEngine;
    private readonly ITextLayout? _textLayout;

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
        // Backdrop filters depend on content outside the filtered node, and a
        // blur samples pixels around its input bounds. Until those dependencies
        // are represented in damage propagation, effects conservatively repaint
        // the complete scene so partial damage cannot leave stale edges.
        var damage = ContainsBlurLayer(rootLayer)
            ? DamageRegion.Full(layout.Size)
            : context.Damage;
        return new RenderScene(
            layout.Size,
            context.Options.ClearColor,
            rootLayer,
            damage,
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

        var style = _styleEngine.ResolveReadOnly(element);
        List<RenderCommand>? overlayCommands = null;
        if (element is IOverlayRenderCommandProvider overlayProvider) {
            overlayCommands = [];
            overlayProvider.AppendOverlayRenderCommands(
                new RenderCommandContext(
                    bounds,
                    style.BorderRadius,
                    ApplyOpacity(style.Foreground, style.Opacity),
                    style.FontSize,
                    style.Padding,
                    style.Font,
                    _textLayout,
                    _styleEngine),
                style,
                overlayCommands);
        }

        var layer = WrapLayer(
            commands,
            children,
            clip,
            scrollTransform,
            overlayCommands);
        if (style.BackdropBlurRadius > 0) {
            layer = new BackdropBlurLayer(
                style.BackdropBlurRadius,
                bounds,
                style.BorderRadius,
                layer);
        }

        if (style.FilterBlurRadius > 0) {
            layer = new BlurLayer(
                style.FilterBlurRadius,
                bounds,
                layer);
        }

        return layer;
    }

    private void AppendElementContent(
        List<RenderCommand> commands,
        Element element,
        Rect bounds,
        RenderOptions options) {
        var style = _styleEngine.ResolveReadOnly(element);
        var background = style.Background;
        var genericBackground = element.Attributes.GetValueOrDefault("background") as Color?;
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
                    _textLayout,
                    _styleEngine),
                commands);
        }
    }

    private static RenderLayer WrapLayer(
        List<RenderCommand> commands,
        List<RenderLayer> children,
        Rect? clip,
        IScrollTransform? scrollTransform = null,
        List<RenderCommand>? overlayCommands = null) {
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

        if (overlayCommands is { Count: > 0 }) {
            layers.Add(new ContentLayer(overlayCommands));
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

}
