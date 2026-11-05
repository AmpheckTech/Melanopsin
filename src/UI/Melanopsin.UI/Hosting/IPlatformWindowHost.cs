using Melanopsin.UI.Input;

namespace Melanopsin.UI.Hosting;

public interface IPlatformWindowHost : IFramePresenter, IDisposable {
    int WindowCount { get; }

    float DisplayScale { get; }

    IPlatformClipboard? Clipboard => null;

    IPlatformNotifications? Notifications => null;

    /// <summary>
    /// Returns whether a frame can currently be submitted. Callers should leave
    /// the window dirty and retry after the next platform event when this is
    /// <see langword="false"/>.
    /// </summary>
    bool IsPresentationReady(WindowDescriptor windowDescriptor) => true;

    /// <summary>
    /// Applies the cursor shape requested by the UI for a window. Hosts that
    /// cannot control a cursor may keep this default no-op implementation.
    /// </summary>
    void SetCursor(WindowId windowId, CursorKind cursor) {
    }

    /// <summary>
    /// Updates the platform text-input client for the currently focused editor.
    /// A null state deactivates platform input-method integration.
    /// </summary>
    void SetTextInput(TextInputState? state) {
    }

    /// <summary>
    /// Releases all platform resources associated with a window. If the host
    /// retains an accepted frame lease, it must release that lease before this
    /// method returns so the render target can be safely reclaimed.
    /// </summary>
    void CloseWindow(WindowId windowId);

    /// <summary>
    /// Dequeues one pending UI event after draining the platform queue.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when no UI event is currently queued.
    /// </returns>
    bool TryDequeueEvent(out WindowEvent windowEvent);

    /// <summary>
    /// Waits for platform activity and dispatches it before returning.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// Zero polls without blocking; a negative value waits indefinitely.
    /// </param>
    /// <remarks>
    /// These members are called on the platform/application event thread. A
    /// wake-up may occur without producing a queued UI event, so callers must
    /// still check <see cref="TryDequeueEvent"/> afterward.
    /// </remarks>
    void WaitForEvent(int timeoutMilliseconds);
}
