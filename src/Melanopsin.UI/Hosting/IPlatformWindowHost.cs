namespace Melanopsin.UI.Hosting;

public interface IPlatformWindowHost : IFramePresenter, IDisposable {
    int WindowCount { get; }

    float DisplayScale { get; }

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

    void CloseWindow(WindowId windowId);

    bool TryDequeueEvent(out WindowEvent windowEvent);

    void WaitForEvent(int timeoutMilliseconds);
}
