// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Input;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI;

internal sealed class OverlayBackdrop : Control, IInputHandler, IRenderCommandProvider {
    private bool _pressed;

    public OverlayBackdrop()
        : base("OverlayBackdrop") {
        SetVisible(false);
        IsFocusable = true;
        InputHandler = this;
        Accessibility.IsHidden = true;
    }

    public Action? Clicked { get; set; }

    public bool IsDimmed { get; set; } = true;

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        if (!IsDimmed) {
            return;
        }

        commands.Add(new FillRectangleCommand(
            context.Bounds,
            new Color(12, 20, 18, 92)));
    }

    public void Handle(RoutedInputEvent inputEvent) {
        var input = inputEvent.Input;
        if (input.Phase != PointerPhase.Scroll && !input.IsPrimaryButton) {
            return;
        }

        var phase = input.Phase;
        if (phase == PointerPhase.Down) {
            _pressed = true;
            inputEvent.Handled = true;
        } else if (phase == PointerPhase.Up) {
            var clicked = _pressed && inputEvent.IsInsideTarget;
            _pressed = false;
            inputEvent.Handled = true;
            if (clicked) {
                Clicked?.Invoke();
            }
        } else if (phase == PointerPhase.Scroll) {
            inputEvent.Handled = true;
        } else if (phase is PointerPhase.Cancel or PointerPhase.Leave) {
            _pressed = false;
            inputEvent.Handled = true;
        }
    }
}
