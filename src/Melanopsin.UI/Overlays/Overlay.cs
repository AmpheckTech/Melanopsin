using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI;

[MarkupElement]
public class Overlay : Control {
    private bool _isOpen = true;

    public Overlay()
        : this("Overlay") {
    }

    protected Overlay(string type)
        : base(type) {
        IsFocusable = true;
        Accessibility.Role = AccessibilityRole.Region;
    }

    public event Action<Overlay>? Closed;

    [MarkupAttribute("modal", MarkupValueKind.Boolean)]
    public bool IsModal { get; set; } = true;

    [MarkupAttribute("dismiss-on-backdrop", MarkupValueKind.Boolean)]
    public bool DismissOnBackdrop { get; set; }

    public bool IsOpen => _isOpen;

    public string? Result { get; private set; }

    public void Close(string? result = null) {
        if (!_isOpen) {
            return;
        }

        _isOpen = false;
        Result = result;
        Closed?.Invoke(this);
    }

    internal void Reopen() {
        _isOpen = true;
    }

    protected internal virtual Rect GetArrangeBounds(
        LayoutArrangeContext context,
        LayoutSize measured) {
        var content = context.ContentBounds;
        return new Rect(
            content.X + ((content.Width - measured.Width) / 2),
            content.Y + ((content.Height - measured.Height) / 2),
            Math.Min(content.Width, measured.Width),
            Math.Min(content.Height, measured.Height));
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "modal":
                IsModal = value is bool modal ? modal : IsModal;
                break;
            case "dismiss-on-backdrop":
                DismissOnBackdrop = value is bool dismissOnBackdrop
                    ? dismissOnBackdrop
                    : DismissOnBackdrop;
                break;
        }
    }
}
