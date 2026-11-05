using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class Canvas : Control, IRenderCommandProvider {
    private Action<RenderCommandContext, List<RenderCommand>>? _draw;

    public Canvas()
        : base("Canvas") {
        ClipToBounds = true;
        IsRepaintBoundary = true;
    }

    public Action<RenderCommandContext, List<RenderCommand>>? Draw {
        get => _draw;
        set {
            if (ReferenceEquals(_draw, value)) {
                return;
            }

            _draw = value;
            InvalidatePaint();
        }
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        Draw?.Invoke(context, commands);
    }
}
