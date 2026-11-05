// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class Button :
    Control,
    ISingleLineTextContent,
    IKeyboardActivationTarget,
    IInputHandler,
    IAccessibilityActionProvider,
    ILayoutFallbackSizeProvider {
    private string _text = string.Empty;

    public Button()
        : base("Button") {
        IsRepaintBoundary = true;
        InputHandler = this;
        IsFocusable = true;
        Accessibility.Role = AccessibilityRole.Button;
    }

    [MarkupAttribute("text")]
    public string Text {
        get => _text;
        set {
            if (string.Equals(_text, value, StringComparison.Ordinal)) {
                return;
            }

            _text = value;
            Accessibility.Name = value;
            InvalidateLayout();
        }
    }

    public bool IsPressed {
        get => HasState(UiStateNames.Pressed);
        private set => SetState(UiStateNames.Pressed, value);
    }

    public event Action? Clicked;

    public AccessibilityAction SupportedActions =>
        IsEnabled ? AccessibilityAction.Default : AccessibilityAction.None;

    public bool PerformAccessibilityAction(AccessibilityAction action) {
        if (action != AccessibilityAction.Default || !IsEnabled) {
            return false;
        }

        Activate();
        return true;
    }

    public void AppendText(string value) {
        Text += value;
    }

    public void Handle(RoutedInputEvent inputEvent) {
        if (!IsEnabled) {
            return;
        }

        var input = inputEvent.Input;
        if (!input.IsPrimaryButton) {
            return;
        }

        var phase = input.Phase;
        if (phase == PointerPhase.Down) {
            IsPressed = true;
        } else if (phase == PointerPhase.Up) {
            var shouldClick = IsPressed && inputEvent.IsInsideTarget;
            IsPressed = false;
            if (shouldClick) {
                Clicked?.Invoke();
            }
        } else if (phase is PointerPhase.Cancel or PointerPhase.Leave) {
            IsPressed = false;
        }
    }

    public void Activate() {
        Clicked?.Invoke();
    }

    float ILayoutFallbackSizeProvider.MeasureFallbackHeight(
        LayoutMeasureContext context,
        LayoutConstraints constraints) {
        return context.Metrics.Space(8);
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        if (name == "text") {
            Text = value?.ToString() ?? string.Empty;
        }
    }
}
