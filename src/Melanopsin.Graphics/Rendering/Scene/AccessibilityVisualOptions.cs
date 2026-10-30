namespace Melanopsin.Graphics.Rendering;

public readonly record struct AccessibilityVisualOptions(
    bool HighContrast = false,
    ColorVisionMode ColorVision = ColorVisionMode.None);
