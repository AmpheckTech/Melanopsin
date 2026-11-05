using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class Separator : Control {
    public Separator()
        : base("Separator") {
        SetAttributeValue("height", "1hx");
    }
}
