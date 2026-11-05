using System;
using System.Collections.Generic;
using System.Text;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal enum CssCombinator {
    Descendant,
    Child,
}

internal sealed class CssCompoundSelector {
    private static readonly string[] Empty = [];

    public CssCompoundSelector(
        string? typeName = null,
        string? id = null,
        IReadOnlyList<string>? classes = null,
        IReadOnlyList<string>? states = null,
        string? pseudoElement = null,
        IReadOnlyList<string>? pseudoElementStates = null) {
        TypeName = typeName;
        Id = id;
        Classes = classes ?? Empty;
        States = states ?? Empty;
        PseudoElement = pseudoElement;
        PseudoElementStates = pseudoElementStates ?? Empty;
    }

    public string? TypeName { get; }

    public string? Id { get; }

    public IReadOnlyList<string> Classes { get; }

    public IReadOnlyList<string> States { get; }

    public string? PseudoElement { get; }

    public IReadOnlyList<string> PseudoElementStates { get; }

    public CssCompoundSelector Append(CssCompoundSelector suffix) {
        if (suffix.TypeName is not null) {
            throw new InvalidOperationException("A nested selector suffix cannot introduce a type.");
        }

        if (suffix.Id is not null && Id is not null) {
            throw new InvalidOperationException("A compound selector cannot contain multiple ids.");
        }

        if (suffix.PseudoElement is not null && PseudoElement is not null) {
            throw new InvalidOperationException(
                "A compound selector cannot contain multiple pseudo-elements.");
        }

        var classes = new List<string>(Classes);
        classes.AddRange(suffix.Classes);
        var states = new List<string>(States);
        states.AddRange(suffix.States);
        var pseudoElementStates = new List<string>(PseudoElementStates);
        pseudoElementStates.AddRange(suffix.PseudoElementStates);
        return new CssCompoundSelector(
            TypeName,
            Id ?? suffix.Id,
            classes,
            states,
            PseudoElement ?? suffix.PseudoElement,
            pseudoElementStates);
    }

    public override string ToString() {
        var text = new StringBuilder();
        if (TypeName is not null) {
            text.Append(TypeName);
        }

        if (Id is not null) {
            text.Append('#').Append(Id);
        }

        foreach (var className in Classes) {
            text.Append('.').Append(className);
        }

        foreach (var state in States) {
            text.Append(':').Append(state);
        }

        if (PseudoElement is not null) {
            text.Append("::").Append(PseudoElement);
        }

        foreach (var state in PseudoElementStates) {
            text.Append(':').Append(state);
        }

        return text.Length == 0 ? "*" : text.ToString();
    }
}

internal sealed class CssComplexSelector {
    public CssComplexSelector(
        CssCompoundSelector rightmost,
        CssComplexSelector? parent = null,
        CssCombinator? combinator = null) {
        Rightmost = rightmost;
        Parent = parent;
        Combinator = combinator;
    }

    public CssCompoundSelector Rightmost { get; }

    public CssComplexSelector? Parent { get; }

    public CssCombinator? Combinator { get; }

    public override string ToString() {
        if (Parent is null) {
            return Rightmost.ToString();
        }

        var separator = Combinator == CssCombinator.Child ? " > " : " ";
        return Parent + separator + Rightmost;
    }
}
