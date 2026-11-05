// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics.Rendering;

namespace Melanopsin.UI.Hosting;

// TODO(frame-lifecycle): Exercise reject, backpressure, throw, double release,
// and resize or close while a presenter retains a lease. Every path must release
// the frame lease exactly once.
/// <summary>
/// Consumes completed frames for a platform window.
/// </summary>
public interface IFramePresenter {
    /// <summary>
    /// Presents a completed frame. Only <see cref="PresentResult.Accepted"/>
    /// transfers the lease to the presenter; every other result leaves the
    /// lease with the caller. Throwing also leaves ownership with the caller.
    /// </summary>
    /// <remarks>
    /// Calls currently originate on the application/UI event thread. A
    /// presenter may retain an accepted lease after this method returns and is
    /// then responsible for releasing it exactly once.
    /// </remarks>
    PresentResult Present(WindowDescriptor window, RenderFrameLease frame);
}

/// <summary>
/// Describes whether ownership of a presented frame was accepted.
/// </summary>
public enum PresentResult {
    /// <summary>The presenter owns the lease and will release it later.</summary>
    Accepted,

    /// <summary>The caller retains the lease and may retry after a later event.</summary>
    Backpressured,

    /// <summary>The caller retains the lease; the platform cannot accept this frame.</summary>
    Rejected,
}
