using Melanopsin.Graphics.Rendering;

namespace Melanopsin.UI.Hosting;

public interface IFramePresenter {
    /// <summary>
    /// Presents a completed frame. Only <see cref="PresentResult.Accepted"/>
    /// transfers the lease to the presenter; every other result leaves the
    /// lease with the caller. Throwing also leaves ownership with the caller.
    /// </summary>
    PresentResult Present(WindowDescriptor window, RenderFrameLease frame);
}

public enum PresentResult {
    Accepted,
    Backpressured,
    Rejected,
}
