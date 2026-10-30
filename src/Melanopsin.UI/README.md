# Melanopsin.UI

UI runtime, controls, styling, layout, and platform integration for Melanopsin.

`Melanopsin.UI` consumes the independent `Melanopsin.Graphics`,
`Melanopsin.Reactive`, and `Melanopsin.Mathematics` packages. Animation
timelines, easing, tweens, keyframes, and springs are part of this assembly and
use the `Melanopsin.UI.Animation` namespace. UI-specific units such as `Length`,
`LengthUnit`, and `UiMetrics` also belong to this assembly and use the
`Melanopsin.UI` namespace. General-purpose geometry and drawing types do not
belong here.

## Style-sheet units

The UI text parsers accept the following unit tokens. Parsing is
case-insensitive, but the lowercase forms below are canonical.

| Token | Domain | Meaning |
| --- | --- | --- |
| `auto` | length | Lets layout choose the value |
| `px` | length | Raw layout pixels; `Length.Resolve` does not apply `ScaleFactor` |
| `%` | length | Percentage of the available layout extent |
| `su` | length | Spacing unit: `value * 4 * ScaleFactor` |
| `ru` | length | Radius unit: `value * 4 * ScaleFactor`; semantically distinct from spacing even though the current scale is the same |
| `fu` | length | Font unit: `value * ScaleFactor` |
| `iu` | length | Icon unit: `value * ScaleFactor` |
| `hx` | length | Hairline count: `value * 1` layout unit |
| `vw` | length | Percentage of viewport width |
| `vh` | length | Percentage of viewport height |
| `fr` | length | Fraction of the available layout extent |
| `rem` | length | Root-relative font length: `value * RootFontSize * ScaleFactor` |
| `em` | length | Current font-relative length; falls back to the root font size |
| `dp` | length | Density-independent unit: `value * ScaleFactor` |
| `sp` | length | Currently an alias of `dp`; it is not Android's font-scale-aware `sp` |
| `pt` | length | Typographic point: `value * ScaleFactor * 96 / 72` |
| `ms` | duration | Milliseconds |
| `s` | duration | Seconds |
| `du` | duration | Duration shorthand: `value * 100 ms`; for example, `3du` is `300 ms` |
| `deg` | angle | Degrees |
| `rad` | angle | Radians |
| `turn` | angle | Turns, where `1turn` is `360deg` |
Easing is written with self-describing tokens such as `ease-out`, `ease-in`,
`ease-in-out`, `linear`, and `spring`, or with `cubic-bezier(...)` and
`steps(...)`.

Length, duration, and angle examples:

```text
padding: 3su;             /* 12 layout units before ScaleFactor */
border-radius: 5ru;       /* 20 layout units before ScaleFactor */
font-size: 15fu;          /* 15 font units before ScaleFactor */
transition-duration: 3du; /* 300 ms */
transition-easing: ease-in-out;
```

Angles are parsed through `UiAngle`, for example `UiAngle.Parse("0.5turn")`.
