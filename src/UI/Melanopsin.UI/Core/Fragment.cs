using Melanopsin.UI.Markup;

namespace Melanopsin.UI;

/// <summary>
/// A transparent reconciliation group. It owns multiple sibling roots but does
/// not participate in layout or rendering as a visual box.
/// </summary>
[MarkupElement]
public class Fragment : Element {
    public Fragment()
        : base("Fragment") {
    }
}
