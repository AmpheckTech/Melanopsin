# Melanopsin.Reactive

`Melanopsin.Reactive` is a small, UI-independent reactive state graph. It is
optimized for values that are read repeatedly and invalidated selectively,
rather than for time-based event streams.

## Core model

| Type | Responsibility |
| --- | --- |
| `Signal<T>` | Writable source value with equality-based change suppression. |
| `Computed<T>` | Lazy, cached derived value with dynamic dependency tracking. |
| `Effect` | Immediate side effect that reruns when its dependencies change. |
| `ReactiveScope` | Ownership boundary that disposes effects and other resources. |
| `ReactiveBatch` | Coalesces synchronous writes and invalidates each target once. |
| `ReactiveRuntime` | Low-level tracking, untracked execution, and notification scopes. |
| `IReadOnlySignal<T>` | Untracked/writable signal abstraction used by derived values. |
| `IWritableSignal<T>` | Writable variant of `IReadOnlySignal<T>`. |

Both `Signal<T>` and `Computed<T>` implement `IReadOnlySignal<T>`. Consumers
should accept that interface unless they need to write the value.

```csharp
var first = new Signal<string>("Ada");
var last = new Signal<string>("Lovelace");
var fullName = new Computed<string>(() => $"{first.Value} {last.Value}");

using var scope = ReactiveScope.Create();
using var effect = new Effect(() => Console.WriteLine(fullName.Value));

ReactiveBatch.Run(() => {
    first.Value = "Grace";
    last.Value = "Hopper";
});
```

The effect prints `Ada Lovelace` and then `Grace Hopper`. The two writes in the
batch produce one effect run.

## Semantics

- Reads through `Value` track a dependency; `Peek()` never tracks.
- `Computed<T>` computes lazily and keeps its value until a dependency changes.
- Effects replace their dependency set after every successful run, including
  branches selected at runtime.
- `Signal<T>` notifies only when its equality comparer reports a change.
  `Invalidate()` forces a notification without replacing the value.
- Disposing an `Effect` releases all source subscriptions. Effects created in
  the current `ReactiveScope` are owned by that scope automatically.
- Batches are synchronous and nest. The outer batch flushes pending targets.
- Reactive graph cycles are rejected. Effects that do not stabilize within 100
  consecutive runs throw instead of spinning forever.
- Cross-thread writes are posted to the `IReactiveScheduler` captured by the
  source. Without a scheduler, notifications are synchronous.
- Exception handling is fail-fast: subscriber and effect exceptions propagate
  to the writer, with multiple failures combined as an `AggregateException`.

## Observable interop

`AsObservable()` bridges a signal or computed value to the BCL
`IObservable<T>` contract. It emits the initial value by default and can suppress
duplicates.

```csharp
using var subscription = signal
    .AsObservable(distinctUntilChanged: true)
    .Subscribe(observer);
```

This bridge intentionally does not reimplement the ReactiveX operator catalog.
Add System.Reactive in the consuming application when time-based operators such
as `Throttle`, `Sample`, `Window`, or `ObserveOn` are required. State should stay
in `Signal<T>` and `Computed<T>`; event pipelines should cross the
`IObservable<T>` boundary.

## Extension boundaries

The next layers should remain separate from the state core:

- asynchronous resources should model loading/error/value state rather than
  making every signal asynchronous;
- reactive collections should expose transactional mutations and stable item
  identity;
- framework ownership should integrate through `ReactiveScope`;
- time-based Rx operators belong to System.Reactive or a dedicated adapter
  package, not to `Signal<T>`.
