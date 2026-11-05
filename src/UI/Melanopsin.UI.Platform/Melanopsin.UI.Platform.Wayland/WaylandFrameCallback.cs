using NWayland;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed class WaylandFrameCallback : IDisposable {
    private readonly CallbackListener _listener;
    private WlCallback? _callback;
    private Action? _continuation;
    private bool _disposed;

    public WaylandFrameCallback() {
        _listener = new CallbackListener(this);
    }

    public bool IsPending => _callback is not null;

    public bool TryArm(
        WlSurface surface,
        IWlTargetQueue queue,
        Action continuation) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_callback is not null) {
            return false;
        }

        _continuation = continuation;
        try {
            var callback = surface.Frame(_listener, queue);
            if (callback is null) {
                _continuation = null;
                return false;
            }

            _callback = callback;
        } catch {
            _continuation = null;
            throw;
        }

        return true;
    }

    public void Cancel() {
        if (_disposed) {
            return;
        }

        CancelCore();
    }

    private void CancelCore() {
        var callback = _callback;
        _callback = null;
        _continuation = null;
        callback?.Dispose();
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        CancelCore();
    }

    private void Done(WlCallback callback) {
        if (!ReferenceEquals(_callback, callback)) {
            callback.Dispose();
            return;
        }

        _callback = null;
        var continuation = _continuation;
        _continuation = null;
        try {
            callback.Dispose();
        } finally {
            continuation?.Invoke();
        }
    }

    private sealed class CallbackListener : WlCallback.Listener {
        private readonly WaylandFrameCallback _owner;

        public CallbackListener(WaylandFrameCallback owner) {
            _owner = owner;
        }

        protected override void Done(
            WlCallback eventSender,
            uint callbackData) {
            _owner.Done(eventSender);
        }
    }
}
