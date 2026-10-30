using Melanopsin.Graphics.Rendering;

namespace Melanopsin.UI.Hosting;

public sealed class CompositeFramePresenter : IFramePresenter {
    private readonly IFramePresenter[] _presenters;

    public CompositeFramePresenter(params IFramePresenter[] presenters) {
        ArgumentNullException.ThrowIfNull(presenters);
        if (presenters.Length == 0) {
            throw new ArgumentException("At least one presenter is required.", nameof(presenters));
        }

        foreach (var presenter in presenters) {
            ArgumentNullException.ThrowIfNull(presenter, nameof(presenters));
        }

        _presenters = [.. presenters];
    }

    public void Present(WindowDescriptor window, RenderFrame frame) {
        ArgumentNullException.ThrowIfNull(frame);
        foreach (var presenter in _presenters) {
            presenter.Present(window, frame);
        }
    }
}
