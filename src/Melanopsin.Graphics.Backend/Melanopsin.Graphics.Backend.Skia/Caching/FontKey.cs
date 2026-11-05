// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal readonly record struct FontKey(SKTypeface Typeface, float Size);
