using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class Dialog : Overlay {
    private readonly TextBlock _title;
    private readonly TextBlock _message;
    private readonly FlexLayout _actions;

    public Dialog()
        : base("Dialog") {
        Accessibility.Role = AccessibilityRole.Region;
        InlineStyle.Width = Length.Su(110);
        _title = new TextBlock();
        _title.SetAttributeValue("class", "dialog-title");
        _message = new TextBlock();
        _message.SetAttributeValue("class", "dialog-message");
        _actions = new FlexLayout();
        _actions.SetAttributeValue("orientation", "horizontal");
        _actions.SetAttributeValue("justify-content", "end");
        _actions.SetAttributeValue("column-gap", "2su");
        _actions.SetAttributeValue("height", "12su");
        base.AppendChild(_title);
        base.AppendChild(_message);
        base.AppendChild(_actions);
    }

    public string Title {
        get => _title.Text;
        set {
            _title.Text = value;
            Accessibility.Name = value;
            InvalidateLayout();
        }
    }

    public string Message {
        get => _message.Text;
        set {
            _message.Text = value;
            InvalidateLayout();
        }
    }

    public Button AddAction(
        string text,
        Action? action = null,
        string? result = null) {
        var button = new Button();
        button.SetAttributeValue("class", "dialog-button");
        button.SetAttributeValue("height", "10su");
        button.SetAttributeValue("width", "30su");
        button.Text = text;
        button.Clicked += () => {
            action?.Invoke();
            Close(result ?? text);
        };
        _actions.AppendChild(button);
        return button;
    }
}
