// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Produces reusable frame leases backed by a render surface.
/// </summary>
/// <remarks>
/// A successful <see cref="TryRender"/> transfers ownership of the returned
/// lease to the caller, which must call <see cref="RenderFrameLease.Release"/>
/// after consuming it. A failed call returns a default lease and transfers no
/// ownership. <see cref="IsFrameAvailable"/> is false when no frame slot can be
/// acquired. Rendering, resizing, surface access, and disposal of one target
/// must be serialized; the UI host performs these operations on its
/// application event thread.
/// </remarks>
public interface IRenderTarget : IDisposable {
    RenderTargetDescriptor Descriptor { get; }

    bool IsFrameAvailable { get; }

    bool TryRender(RenderScene scene, out RenderFrameLease frame);
}
