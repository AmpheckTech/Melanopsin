// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI;

namespace Melanopsin.UI.Styling;

/// <summary>
/// Minimal defaults that keep an unstyled element tree legible and operable.
/// These behave like a browser user-agent stylesheet: author declarations and
/// inline styles are applied afterwards and therefore override every value.
/// </summary>
internal static class UserAgentStyles {
    private static readonly Color Canvas = new(12, 18, 24);
    private static readonly Color Surface = new(27, 34, 42);
    private static readonly Color SurfaceHover = new(35, 44, 54);
    private static readonly Color SurfacePressed = new(21, 27, 34);
    private static readonly Color Border = new(72, 84, 96);
    private static readonly Color Ink = new(232, 238, 242);
    private static readonly Color MutedInk = new(174, 184, 193);

    public static void Apply(Node node, ResolvedStyle style) {
        if (node is not Element element) {
            return;
        }

        switch (element.Type) {
            case "Page":
                style.Background = Canvas;
                style.Foreground = Ink;
                break;

            case "Button":
                ApplyControlChrome(style);
                style.Foreground = Ink;
                style.Cursor = CursorKind.Pointer;
                if (element is Control { IsEnabled: false }) {
                    style.Foreground = MutedInk;
                    style.Background = SurfacePressed;
                } else if (element.HasState(UiStateNames.Pressed)) {
                    style.Background = SurfacePressed;
                } else if (element.IsHovered) {
                    style.Background = SurfaceHover;
                }

                break;

            case "Input":
            case "ComboBox":
                ApplyControlChrome(style);
                style.Foreground = Ink;
                style.Cursor = element.Type == "Input"
                    ? CursorKind.Text
                    : CursorKind.Pointer;
                break;

            case "Separator":
                style.Background = Border;
                break;
        }
    }

    private static void ApplyControlChrome(ResolvedStyle style) {
        style.Background = Surface;
        style.BorderColor = Border;
        style.BorderWidth = 1;
        style.BorderRadius = 4;
    }
}
