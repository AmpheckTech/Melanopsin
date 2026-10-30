using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal readonly record struct FontKey(SKTypeface Typeface, float Size);
