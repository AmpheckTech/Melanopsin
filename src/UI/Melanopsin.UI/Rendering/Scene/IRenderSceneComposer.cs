// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Rendering;

public interface IRenderSceneComposer {
    RenderScene Compose(
        LayoutSnapshot layout,
        RenderCompositionContext context);
}
