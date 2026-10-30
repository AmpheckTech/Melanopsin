namespace Melanopsin.UI.Styling;

public sealed class StyleRule {
    public StyleRule(string selector, IReadOnlyDictionary<string, string> declarations) {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentNullException.ThrowIfNull(declarations);
        Selector = selector.Trim();
        Declarations = new Dictionary<string, string>(
            declarations,
            StringComparer.OrdinalIgnoreCase);
        Specificity = CalculateSpecificity(Selector);
    }

    public string Selector { get; }

    public IReadOnlyDictionary<string, string> Declarations { get; }

    public int Specificity { get; }

    private static int CalculateSpecificity(string selector) {
        var specificity = selector.Contains('#') ? 100 : 0;
        specificity += selector.Contains('.') ? 10 : 0;
        specificity += selector.Contains(':') ? 10 : 0;
        specificity += selector.Length != 0 && char.IsLetter(selector[0]) ? 1 : 0;
        return specificity;
    }
}
