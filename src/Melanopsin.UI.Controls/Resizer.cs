using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

/// <summary>
/// A focusable divider that reports horizontal drag deltas in layout pixels.
/// The owning layout is responsible for applying those deltas to its panes.
/// </summary>
[MarkupElement]
public sealed class Resizer :
    Control,
    IInputHandler,
    IKeyboardInputHandler,
    IRenderCommandProvider {
    private bool _dragging;
    private float _lastPointerX;

    public Resizer()
        : base("Resizer") {
        IsRepaintBoundary = true;
        IsFocusable = true;
        InputHandler = this;
        Accessibility.Role = AccessibilityRole.Region;
        Accessibility.Name = "Resize panels";
    }

    /// <summary>
    /// Raised on each horizontal pointer movement and keyboard adjustment.
    /// Positive values move the divider right; negative values move it left.
    /// </summary>
    public event Action<double>? DragDelta;

    public void Handle(RoutedInputEvent inputEvent) {
        if (!IsEnabled) {
            EndDrag();
            return;
        }

        var input = inputEvent.Input;
        var phase = input.Phase;
        if (phase == PointerPhase.Down) {
            if (!input.IsPrimaryButton) {
                return;
            }

            _dragging = true;
            _lastPointerX = input.X;
            SetState(UiStateNames.Pressed, true);
            inputEvent.Handled = true;
        } else if (!input.IsPrimaryButton) {
            return;
        } else if (phase == PointerPhase.Move && _dragging) {
            var delta = input.X - _lastPointerX;
            _lastPointerX = input.X;
            if (MathF.Abs(delta) >= float.Epsilon) {
                DragDelta?.Invoke(delta);
            }

            inputEvent.Handled = true;
        } else if (phase is PointerPhase.Up or PointerPhase.Cancel && _dragging) {
            EndDrag();
            inputEvent.Handled = true;
        }
    }

    public bool HandleKey(KeyInputEvent input) {
        if (!IsEnabled || input.Phase != KeyPhase.Down) {
            return false;
        }

        return input.Key switch {
            Key.Left => ResizeFromKeyboard(-8),
            Key.Right => ResizeFromKeyboard(8),
            _ => false,
        };
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        var emphasized = IsHovered || IsFocused || HasState(UiStateNames.Pressed);
        var thickness = emphasized ? 3f : 1f;
        var line = new Rect(
            context.Bounds.X + ((context.Bounds.Width - thickness) / 2),
            context.Bounds.Y,
            thickness,
            context.Bounds.Height);
        commands.Add(new FillRectangleCommand(
            line,
            context.Foreground,
            thickness / 2));
    }

    private bool ResizeFromKeyboard(double delta) {
        DragDelta?.Invoke(delta);
        return true;
    }

    private void EndDrag() {
        _dragging = false;
        SetState(UiStateNames.Pressed, false);
    }
}
