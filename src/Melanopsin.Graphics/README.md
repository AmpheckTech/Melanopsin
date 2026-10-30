# Melanopsin.Graphics

Backend-neutral two-dimensional graphics primitives, scene contracts, and
text layout for Melanopsin.

This is a general graphics library, not a UI-only package. It does not depend
on `Melanopsin.UI`, a windowing platform, Reactive, or Animation. Rendering can
run headlessly and write to an in-memory target, as demonstrated by the
[`Melanopsin.UI.Demo`](../../samples/Melanopsin.UI.Demo/).

The package contains:

- shared colors and pixel primitives in
  `Melanopsin.Graphics`;
- rendering commands, layers, scenes, and backend contracts in
  `Melanopsin.Graphics.Rendering`;
- font parsing, shaping, and paragraph layout in
  `Melanopsin.Graphics.Text` and `Melanopsin.Graphics.Text.Fonts`.

Rendering backends are separate packages. For example,
`Melanopsin.Graphics.Backend.Skia` depends on `Melanopsin.Graphics` and provides
both a CPU bitmap backend and an optional GPU backend backed by WGL on Windows
or EGL on Linux. The UI demo uses the CPU backend so it can run without a GPU.

## Demo

The backend-neutral rendering path is:

```text
RenderScene -> IRenderBackend.CreateTarget -> IRenderTarget.Render -> RenderFrame
```

The UI demo executes that path with the Skia CPU backend:

```sh
dotnet run --project samples/Melanopsin.UI.Demo
```

Shape-only scenes do not require a primary font. A `FontFace` is needed only
when the scene contains text commands.

## Source layout

Directories group the implementation by responsibility and do not
automatically define a public namespace.

| Path | Contents | Public namespace |
|---|---|---|
| `Color.cs` | Shared 8-bit RGBA color value | `Melanopsin.Graphics` |
| `Geometry/` | Pixel-space `PixelRect` and `PixelSize` | `Melanopsin.Graphics` |
| `Images/` | Backend-neutral image resources | `Melanopsin.Graphics` |
| `Rendering/` | Commands, layers, scenes, and backend contracts | `Melanopsin.Graphics.Rendering` |
| `Text/` | Shaping, font parsing, and text layout | `Melanopsin.Graphics.Text` |

Text is part of the Graphics assembly instead of a separate package. Its
namespace remains `Melanopsin.Graphics.Text` so the public API keeps a clear
domain boundary without requiring a second project reference.
