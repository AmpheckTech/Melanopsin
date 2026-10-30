using Melanopsin.UI.Styling;

namespace Melanopsin.UI.DevTools;

public static class DevToolsStyles {
    public const string Css = """
        Page.devtools-root { background: #081116; color: #cadcd6; padding: 3su; }
        .devtools-header { padding: 1su; }
        .devtools-title { color: #5de2b5; font-size: 18fu; padding: 1su; }
        .devtools-caption { color: #78998e; font-size: 11fu; text-align: end; padding: 1su; }
        .devtools-tabs { padding: 1su; background: #0e1c22; border-radius: 3ru; }
        .devtools-tab { background: #17332d; color: #b8d3ca; border-radius: 2ru; padding: 2su; font-size: 12fu; text-align: center; }
        .devtools-tab:hover { background: #245847; }
        .devtools-tab:pressed { background: #183b31; }
        .devtools-tab-selected { background: #1e6956; color: #f1fff9; }
        .devtools-tab-selected:hover { background: #25836b; }
        .devtools-tab-selected:pressed { background: #175342; }
        .devtools-scroll { background: transparent; border-width: 0px; padding: 0px; }
        .devtools-content { background: #0c171c; border-color: #214039; border-width: 1hx; border-radius: 4ru; padding: 4su; }
        .devtools-section { color: #5de2b5; font-size: 11fu; padding: 1su; }
        .devtools-selected { color: #f1fff9; font-size: 18fu; padding: 1su; }
        .devtools-value { color: #b8d3ca; font-size: 13fu; padding: 1su; }
        .devtools-hint { color: #78998e; font-size: 11fu; padding: 2su; }
        """;

    public static void Install(StyleEngine styleEngine) {
        ArgumentNullException.ThrowIfNull(styleEngine);
        styleEngine.Sheet.AddRange(StyleSheetParser.Parse(Css).Rules);
    }
}
