using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Platform.Win32.Windowing;

internal static class Win32WindowPlacement {
    private const int Margin = 40;
    private const int Gap = 24;
    private const int CascadeStep = 40;

    public static Rect CreateFallbackWorkArea(int width, int height) {
        return new Rect {
            Right = Math.Max(width, 1280),
            Bottom = Math.Max(height, 720),
        };
    }

    public static (int X, int Y) Find(
        Rect workArea,
        IReadOnlyList<Rect> occupied,
        int width,
        int height,
        int windowCount) {
        foreach (var rectangle in occupied) {
            var x = rectangle.Right + Gap;
            var y = rectangle.Top;
            if (Fits(x, y, width, height, workArea, occupied)) {
                return (x, y);
            }
        }

        foreach (var rectangle in occupied) {
            var x = rectangle.Left;
            var y = rectangle.Bottom + Gap;
            if (Fits(x, y, width, height, workArea, occupied)) {
                return (x, y);
            }
        }

        var cascadeX = Margin + (windowCount * CascadeStep);
        var cascadeY = Margin + (windowCount * CascadeStep);
        return (
            Math.Clamp(
                cascadeX,
                workArea.Left,
                Math.Max(workArea.Left, workArea.Right - width)),
            Math.Clamp(
                cascadeY,
                workArea.Top,
                Math.Max(workArea.Top, workArea.Bottom - height)));
    }

    private static bool Fits(
        int x,
        int y,
        int width,
        int height,
        Rect workArea,
        IReadOnlyList<Rect> occupied) {
        if (x < workArea.Left ||
            y < workArea.Top ||
            x + width > workArea.Right ||
            y + height > workArea.Bottom) {
            return false;
        }

        var candidate = new Rect {
            Left = x,
            Top = y,
            Right = x + width,
            Bottom = y + height,
        };
        foreach (var rectangle in occupied) {
            if (candidate.Left < rectangle.Right &&
                candidate.Right > rectangle.Left &&
                candidate.Top < rectangle.Bottom &&
                candidate.Bottom > rectangle.Top) {
                return false;
            }
        }

        return true;
    }
}
