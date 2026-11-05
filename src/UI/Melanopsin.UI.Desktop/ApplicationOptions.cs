// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Desktop;

/// <summary>
/// Configures <see cref="Application.Default(ApplicationOptions)"/>. The render
/// backend is supplied by the host so applications can swap backends without
/// forking the desktop bootstrap.
/// </summary>
public sealed class ApplicationOptions
{
    /// <summary>
    /// Creates the render backend that backs every window. The primary font is
    /// passed in so raster and GPU backends share the same text pipeline. When
    /// this is <see langword="null"/> the Skia raster backend is used.
    /// </summary>
    public Func<FontFace, IRenderBackend>? BackendFactory { get; init; }

    /// <summary>Optional callback that adds application styles to the sheet.</summary>
    public Action<StyleSheet>? ConfigureStyles { get; init; }

    /// <summary>
    /// Viewport width reported to <see cref="UiMetrics"/> for stylesheet units.
    /// </summary>
    public int ViewportWidth { get; init; } = 1280;

    /// <summary>
    /// Viewport height reported to <see cref="UiMetrics"/> for stylesheet units.
    /// </summary>
    public int ViewportHeight { get; init; } = 720;
}
