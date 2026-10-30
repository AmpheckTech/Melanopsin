using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class StackPanel : LinearLayout {
    public StackPanel()
        : base("StackPanel") {
    }
}
