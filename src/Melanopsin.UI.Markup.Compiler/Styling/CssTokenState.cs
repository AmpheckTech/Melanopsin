namespace Melanopsin.UI.Markup.Compiler.Styling;

internal sealed class CssTokenState {
    public CssTokenState(CssSlot slot, StyleValueKind kind, string cSharpName) {
        Slot = slot;
        Kind = kind;
        CSharpName = cSharpName;
    }

    public CssSlot Slot { get; }

    public StyleValueKind Kind { get; }

    public string CSharpName { get; }

    public string? Expression { get; set; }

    public bool IsResolving { get; set; }
}
