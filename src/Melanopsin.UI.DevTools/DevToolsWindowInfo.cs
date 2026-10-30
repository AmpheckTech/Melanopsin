using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.DevTools;

public sealed record DevToolsWindowInfo(
    WindowId Id,
    string Title,
    string CssClass);
