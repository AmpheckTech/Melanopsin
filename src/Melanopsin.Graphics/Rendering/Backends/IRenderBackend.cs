// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Creates independent render targets for scenes, surfaces, or other drawing
/// destinations.
/// Implementations should be AOT-compatible and avoid reflection.
/// </summary>
public interface IRenderBackend : IDisposable {
    string Name { get; }

    bool IsHardwareAccelerated => false;

    IRenderTarget CreateTarget(RenderTargetDescriptor descriptor);
}
