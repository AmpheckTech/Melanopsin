using Melanopsin.UI.App;

namespace Melanopsin.UI.Input;

internal sealed class PointerCaptureState {
    public PointerCaptureState(WindowHitTarget target) {
        Target = target;
    }

    public WindowHitTarget Target { get; }

    public HashSet<MouseButtonKind> Buttons { get; } = [];
}
