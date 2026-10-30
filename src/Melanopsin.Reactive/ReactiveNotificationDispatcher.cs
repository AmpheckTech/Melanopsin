namespace Melanopsin.Reactive;

internal static class ReactiveNotificationDispatcher {
    public static IReactiveScheduler? Capture() {
        return ReactiveScheduler.Default;
    }

    public static IReactiveScheduler? CaptureOrReuse(
        IReactiveScheduler? current) {
        var active = Capture();
        if (active is { IsStopped: false }) {
            return active;
        }

        return current is { IsStopped: false } ? current : null;
    }

    public static bool TryInvoke(
        IReactiveScheduler? scheduler,
        Action action) {
        ArgumentNullException.ThrowIfNull(action);
        if (scheduler is null || scheduler.CheckAccess()) {
            action();
            return true;
        }

        // Once the scheduler is stopping, there is no consumer left to observe
        // the notification. Dropping it is intentional and avoids failing the
        // writer during shutdown.
        return scheduler.TryPost(action);
    }
}
