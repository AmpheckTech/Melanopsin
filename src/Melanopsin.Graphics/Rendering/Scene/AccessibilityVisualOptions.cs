// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

public readonly record struct AccessibilityVisualOptions(
    bool HighContrast = false,
    ColorVisionMode ColorVision = ColorVisionMode.None);
