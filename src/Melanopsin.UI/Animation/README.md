# Melanopsin.UI.Animation

`Melanopsin.UI.Animation` contains the animation primitives shipped by the
`Melanopsin.UI` assembly. It does not own a clock or frame loop; callers advance
animations with elapsed time from the UI loop or another scheduler.

## Source layout

- `Easings/` contains one file per concrete `Easing` implementation.
- Concrete animation and spring types, the abstract `Easing` type,
  `AnimationTimeline`, `EasingParser`, `StepPosition`, and the shared
  direction/state enums are at the `Animation/` root.

All animation primitives use the `Melanopsin.UI.Animation` namespace and are
included in the `Melanopsin.UI` project as part of this directory.

## Capabilities

- Direction-aware timelines with delay, easing, finite or infinite iterations,
  pause/resume, restart, seek, and completion notification.
- Linear, ease-in, ease-out, ease-in-out, spring, cubic-bezier, and stepped easing.
- A closed `Easing` record hierarchy: each curve owns only the data it needs,
  and cubic-bezier/step values cannot exist with the wrong parameters.
- A span-based `EasingParser` for text formats such as `ease-out`,
  `cubic-bezier(0.42, 0, 0.58, 1)`, and `steps(4, jump-end)`.
- Generic tweens with caller-provided interpolation.
- Generic keyframe animations with ordered samples.
- Damped spring state and a stateless spring integration step.

The value animation types deliberately do not reference a math package; provide
the interpolation function for the value type you use. Springs are advanced
directly through physical simulation rather than through a finite playback
timeline.

## Units and tolerances

The library keeps units explicit at its boundaries and does not perform unit
conversion internally.

| Concept | Unit |
| --- | --- |
| Timeline `Duration`, `Delay`, `Elapsed`, and `Advance(TimeSpan)` | Wall-clock duration; the caller supplies elapsed time |
| `Advance(float)` on timelines, tweens, keyframe animations, and springs, plus `SpringSolver.Step` | Seconds |
| `AnimationTimeline.Progress` and `Easing.Evaluate` input/output | Dimensionless normalized progress |
| Cubic-bezier `x1` and `x2` | Dimensionless normalized time in `[0, 1]` |
| Cubic-bezier `y1` and `y2` | Dimensionless progress; finite values outside `[0, 1]` may intentionally overshoot |
| `steps` | Integer jump count, not a rate |
| Iterations | Integer repetition count |
| `SpringValue.Position`, `Target`, and `RestDistance` | Caller-defined linear position unit `U` (for example, pixels) |
| `SpringValue.Velocity` and `RestVelocity` | `U / second` |
| `SpringConfig.Mass` | Caller-defined mass unit `M` (kilograms when `U` is meters) |
| `SpringConfig.Stiffness` | `M / second²` |
| `SpringConfig.Damping` | `M / second` |

The spring follows the damped harmonic oscillator equation

```text
m x'' + c x' + k (x - target) = 0
```

where `x` is `Position`, `x'` is `Velocity`, `m` is `Mass`, `c` is `Damping`,
and `k` is `Stiffness`. `SpringSolver.Step` uses semi-implicit Euler:

```text
a = (k * (target - x) - c * v) / m
v = v + a * dt
x = x + v * dt
```

The values are not converted. If `U` is meters and `M` is kilograms, then
stiffness is `kg/s²` (equivalent to `N/m`) and damping is `kg/s`. If `U` is
pixels, `M` is still an abstract mass unit and the same equations apply to pixel
positions; stiffness is not `N/m` in that case.

`new SpringConfig()` defaults to `mass: 1`, `stiffness: 160`, and
`damping: 24` (with `restDistance: 0.01` and `restVelocity: 0.01`).
For `mass = 1` and `stiffness = 160`, critical damping is
`2 * sqrt(k * m) ≈ 25.3`; the default damping is therefore slightly
underdamped (`ζ ≈ 0.949`) and may overshoot a little. Useful tuning identities
are the natural angular frequency `ω = sqrt(k / m)` and damping ratio
`ζ = c / (2 * sqrt(k * m))`: `ζ < 1` bounces, `ζ = 1` is critical damping, and
`ζ > 1` approaches the target without overshoot. As a starting range for UI
springs, use `mass` near `0.5–2`, `stiffness` near `80–300`, and `damping` near
`15–40`, then tune `ζ` and `ω` rather than treating the numbers as universal.
All time values must be supplied in seconds.

`EasingParser` accepts the named curves `linear`, `ease-in`, `ease-out`,
`ease-in-out`, and `spring`, plus `cubic-bezier(...)` and `steps(...)`.

The cubic-bezier solver targets a normalized parameter-space width of `1e-7` for
the time parameter `t`, not a fixed tolerance on the eased output. Its local
output uncertainty is approximately `|dy/dt| * Δt`, which is equivalently
`|dy/dx|` times the residual in normalized time. Because finite Y control points
have no magnitude limit, a small parameter residual, or even a small residual in
`x(t)`, does not imply a small absolute error in the returned Y value. The API
therefore promises parameter-space accuracy, not an absolute output tolerance.

## Quick start

```csharp
using Melanopsin.UI.Animation;

var timeline = new AnimationTimeline(
    TimeSpan.FromMilliseconds(300),
    Easing.EaseOut,
    delay: TimeSpan.FromMilliseconds(50));

timeline.Completed += (_, _) => Console.WriteLine("complete");
timeline.Play();
var advanced = timeline.Advance(TimeSpan.FromMilliseconds(16));
```

`advanced` is `true` when the call moved timeline time forward or completed the
timeline. It does not mean that the timeline is still running.

A tween uses the same timeline state:

```csharp
var tween = new Tween<float>(
    0,
    1,
    TimeSpan.FromMilliseconds(200),
    static (from, to, progress) => from + ((to - from) * progress),
    Easing.EaseOut);

tween.Play();
_ = tween.Advance(1f / 120f);
```

Text-based callers can parse easing without depending on a UI framework:

```csharp
if (EasingParser.TryParse("cubic-bezier(0.42, 0, 0.58, 1)", out var easing)) {
    var timeline = new AnimationTimeline(TimeSpan.FromMilliseconds(250), easing);
}
```

Animations sharing a caller-owned frame loop are advanced independently:

```csharp
var opacity = new Tween<float>(
    0,
    1,
    TimeSpan.FromMilliseconds(200),
    static (from, to, progress) => from + ((to - from) * progress));
var scale = new Tween<float>(
    0.8f,
    1,
    TimeSpan.FromMilliseconds(300),
    static (from, to, progress) => from + ((to - from) * progress));

opacity.Play();
scale.Play();
_ = opacity.Advance(1f / 120f);
_ = scale.Advance(1f / 120f);
```

A keyframe animation samples the ordered keyframes:

```csharp
var animation = new KeyframeAnimation<float>(
    [
        new Keyframe<float>(offset: 0, value: 0),
        new Keyframe<float>(offset: 0.5f, value: 0.8f),
        new Keyframe<float>(offset: 1, value: 1),
    ],
    TimeSpan.FromSeconds(1),
    static (from, to, progress) => from + ((to - from) * progress));
```

Keyframe `Offset` values must be finite and within `[0, 1]`. They do not need to
be provided in ascending order: the constructor performs a stable sort by
offset. Duplicate offsets are allowed; among keyframes with the same offset, the
one that appears later in the input wins. Progress before the first keyframe
samples the first value, and progress after the last keyframe samples the last
value.

Springs can be driven independently:

```csharp
var spring = new SpringValue(initialValue: 0, targetValue: 100);
// Bound the integration loop so a misconfigured spring cannot hang the caller.
for (var frame = 0; frame < 600 && !spring.IsAtRest; frame++) {
    spring.Advance(1f / 120f);
}
```

## Playback semantics

- `Advance` is the only operation that consumes time and only advances while the
  timeline is `Running`. It returns `true` when the call changes elapsed time or
  transitions the timeline to `Completed`, not when the timeline is merely
  "still active". The terminal call that completes a finite timeline returns
  `true`; the next call returns `false`. Idle, paused, and zero-delta calls also
  return `false`.
- `Play` starts an `Idle` timeline, resumes a `Paused` timeline, and is a no-op
  while `Running`. Playing a `Completed` timeline resets it to zero and starts a
  new run. Use `Restart` to restart unconditionally from zero.
- `Completed` is raised synchronously on the thread calling `Advance`, exactly
  once per run, before `Advance` returns. Handlers should return quickly;
  exceptions propagate to the caller. Calling `Seek(1)` marks the timeline
  complete but does not raise `Completed`.
- `Advance(float)` is a convenience overload that converts seconds to a
  `TimeSpan` at 100-nanosecond tick resolution. Prefer `Advance(TimeSpan)` when
  the caller owns exact accumulated time, especially for long-running
  animations.
- Keyframe offsets must be in `[0, 1]`; input order may be unsorted. Duplicate
  offsets are allowed and the later input keyframe wins at that offset.
- Easing endpoints are exact: normalized progress `0` maps to `0`, and `1` maps
  to `1`. Spring-like curves may overshoot between those endpoints.
- `steps(n)` defaults to `jump-end`. The parser and `Easing.Step` also support
  `jump-start`, `jump-none`, and `jump-both`; endpoint values remain exact even
  when a step position would otherwise place a jump at an endpoint.
