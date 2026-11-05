// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Rendering;

/// <summary>
/// Adds backend-neutral decoration after an element's transformed children.
/// Overlay commands are clipped with the element and are not moved by its
/// scroll transform.
/// </summary>
public interface IOverlayRenderCommandProvider {
    void AppendOverlayRenderCommands(
        RenderCommandContext context,
        ResolvedStyle style,
        List<RenderCommand> commands);
}
