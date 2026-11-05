// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI;
using Melanopsin.UI.App;

namespace Melanopsin.UI.Input;

public sealed class RoutedInputEvent {
    private readonly WindowHitTarget _target;

    internal RoutedInputEvent(PointerInputEvent input, WindowHitTarget target) {
        Input = input;
        _target = target;
        Target = target.Node;
        TargetBounds = target.Bounds;
        CurrentTarget = target.Node;
    }

    public PointerInputEvent Input { get; }

    public Node Target { get; }

    public Rect TargetBounds { get; }

    public Node CurrentTarget { get; internal set; }

    public bool Handled { get; set; }

    /// <summary>
    /// Requests that the window keep its current focus after this pointer-down
    /// event. The runtime applies its default focus behavior after routing.
    /// </summary>
    public bool PreventDefaultFocus { get; set; }

    /// <summary>
    /// Gets the pointer X in the target's untransformed layout coordinates.
    /// </summary>
    public float LayoutX => _target.ToLayoutX(Input.X);

    /// <summary>
    /// Gets the pointer Y in the target's untransformed layout coordinates.
    /// </summary>
    public float LayoutY => _target.ToLayoutY(Input.Y);

    /// <summary>
    /// Gets the pointer X in the current routing target's untransformed layout
    /// coordinates. Unlike <see cref="LayoutX"/>, this remains stable while the
    /// event bubbles through scroll containers and nested controls.
    /// </summary>
    public float CurrentTargetLayoutX => _target.ToLayoutX(Input.X, CurrentTarget);

    /// <summary>
    /// Gets the pointer Y in the current routing target's untransformed layout
    /// coordinates. Unlike <see cref="LayoutY"/>, this remains stable while the
    /// event bubbles through scroll containers and nested controls.
    /// </summary>
    public float CurrentTargetLayoutY => _target.ToLayoutY(Input.Y, CurrentTarget);

    public bool IsInsideTarget => _target.Contains(Input.X, Input.Y);

    internal void CancelPendingInteraction() {
        var canceledInput = Input.WithPhase(PointerPhase.Cancel);
        var canceledEvent = new RoutedInputEvent(canceledInput, _target);
        for (var index = _target.Route.Count - 1; index >= 0; index--) {
            var node = _target.Route[index];
            if (ReferenceEquals(node, CurrentTarget)) {
                break;
            }

            canceledEvent.CurrentTarget = node;
            node.RaisePointerEvent(canceledInput);
            node.InputHandler?.Handle(canceledEvent);
        }
    }
}
