// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public sealed class RenderScene {
    public RenderScene(
        PixelSize size,
        Color clearColor,
        RenderLayer rootLayer,
        DamageRegion? damage = null,
        RenderStatistics? statistics = null,
        AccessibilityVisualOptions visualAccessibility = default) {
        ArgumentNullException.ThrowIfNull(rootLayer);
        Size = size;
        ClearColor = clearColor;
        RootLayer = rootLayer;
        Damage = damage ?? DamageRegion.Full(size);
        if (damage is not null && damage.Size != size) {
            throw new ArgumentException(
                "The damage region size does not match the render scene.",
                nameof(damage));
        }

        Statistics = statistics ??
            new RenderStatistics(rootLayer.CountCommands(), 0, 0);
        VisualAccessibility = visualAccessibility;
    }

    public PixelSize Size { get; }

    public Color ClearColor { get; }

    public RenderLayer RootLayer { get; }

    public DamageRegion Damage { get; }

    public RenderStatistics Statistics { get; }

    public AccessibilityVisualOptions VisualAccessibility { get; }
}
