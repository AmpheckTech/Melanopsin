namespace Melanopsin.Graphics.Text;

public sealed partial class HarfBuzzTextShaper {
    public void Dispose() {
        _lifetimeLock.EnterWriteLock();
        try {
            lock (_layoutLock) {
                if (_disposed) {
                    return;
                }

                _disposed = true;
                _layouts.Clear();
            }

            _glyphShaper.Dispose();
        } finally {
            _lifetimeLock.ExitWriteLock();
        }
    }
}
