// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Reactive;

/// <summary>
/// Starts dependency tracking and notification scopes for integrations such as
/// UI runtimes and resource systems.
/// </summary>
public static class ReactiveRuntime {
    [ThreadStatic]
    internal static HashSet<IReactiveSource>? Dependencies;

    [ThreadStatic]
    internal static Action<IReactiveSource>? SourceTracked;

    [ThreadStatic]
    internal static int NotificationDepth;

    [ThreadStatic]
    internal static HashSet<IReactiveInvalidationTarget>? PendingInvalidations;

    [ThreadStatic]
    private static HashSet<IReactiveSource>? ComputationStack;

    /// <summary>
    /// Gets whether the current execution context is recording dependencies.
    /// </summary>
    public static bool IsTracking => Dependencies is not null;

    public static IDisposable Track(HashSet<IReactiveSource> dependencies) {
        return Track(dependencies, null);
    }

    /// <summary>
    /// Starts dependency tracking and invokes <paramref name="sourceTracked"/>
    /// the first time each source is read, so consumers can subscribe before the
    /// first value is observed.
    /// </summary>
    public static IDisposable Track(
        HashSet<IReactiveSource> dependencies,
        Action<IReactiveSource>? sourceTracked) {
        ArgumentNullException.ThrowIfNull(dependencies);
        var previous = Dependencies;
        var previousSourceTracked = SourceTracked;
        Dependencies = dependencies;
        SourceTracked = sourceTracked;
        return new ReactiveTrackingScope(previous, previousSourceTracked);
    }

    /// <summary>
    /// Records a dependency if a tracking scope is currently active.
    /// </summary>
    public static void Track(IReactiveSource source) {
        ArgumentNullException.ThrowIfNull(source);
        if (Dependencies?.Add(source) == true) {
            SourceTracked?.Invoke(source);
        }
    }

    /// <summary>
    /// Runs a function without recording any reactive dependency reads.
    /// </summary>
    public static T Untrack<T>(Func<T> action) {
        ArgumentNullException.ThrowIfNull(action);
        var previousDependencies = Dependencies;
        var previousSourceTracked = SourceTracked;
        Dependencies = null;
        SourceTracked = null;
        try {
            return action();
        } finally {
            Dependencies = previousDependencies;
            SourceTracked = previousSourceTracked;
        }
    }

    /// <summary>
    /// Runs an action without recording any reactive dependency reads.
    /// </summary>
    public static void Untrack(Action action) {
        ArgumentNullException.ThrowIfNull(action);
        _ = Untrack(() => {
            action();
            return true;
        });
    }

    public static IDisposable BeginNotification() {
        if (NotificationDepth == 0) {
            PendingInvalidations = [];
        }

        NotificationDepth++;
        return new ReactiveNotificationScope();
    }

    public static void RequestInvalidation(IReactiveInvalidationTarget target) {
        ArgumentNullException.ThrowIfNull(target);
        if (NotificationDepth == 0) {
            target.InvalidateFromReactiveSource();
            return;
        }

        PendingInvalidations!.Add(target);
    }

    internal static void EnterComputation(IReactiveSource source) {
        var stack = ComputationStack ??= [];
        if (!stack.Add(source)) {
            throw new InvalidOperationException(
                "A cycle was detected in the reactive dependency graph.");
        }
    }

    internal static void ExitComputation(IReactiveSource source) {
        var stack = ComputationStack;
        stack?.Remove(source);
        if (stack is { Count: 0 }) {
            ComputationStack = null;
        }
    }
}
