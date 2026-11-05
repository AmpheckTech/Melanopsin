using Melanopsin.UI;

namespace Melanopsin.UI.Layout;

public interface ILayoutEngine {
    LayoutSnapshot Layout(
        Node root,
        PixelSize size,
        LayoutPassContext context = default);
}
