namespace Melanopsin.UI.Styling;

public sealed class StyleSheet {
    private readonly List<StyleRule> _rules = [];
    private long _version;

    public IReadOnlyList<StyleRule> Rules => _rules;

    public long Version => _version;

    public void Add(StyleRule rule) {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
        _version++;
    }

    public void AddRange(IEnumerable<StyleRule> rules) {
        ArgumentNullException.ThrowIfNull(rules);
        _rules.AddRange(rules);
        _version++;
    }
}
