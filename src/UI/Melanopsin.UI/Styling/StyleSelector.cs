// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Styling;

/// <summary>
/// A typed, immutable selector. A selector describes one compound selector and
/// may optionally link to a parent compound through a descendant or child
/// combinator.
/// </summary>
public sealed class StyleSelector : IEquatable<StyleSelector> {
    private static readonly string[] Empty = [];

    private StyleSelector(
        string? typeName,
        string? id,
        IReadOnlyList<string> classes,
        IReadOnlyList<string> states,
        string? pseudoElement,
        IReadOnlyList<string> pseudoElementStates,
        StyleSelector? parent,
        StyleCombinator? combinator) {
        TypeName = typeName;
        Id = id;
        Classes = classes;
        States = states;
        PseudoElement = pseudoElement;
        PseudoElementStates = pseudoElementStates;
        Parent = parent;
        Combinator = combinator;
        var parentSpecificity = parent?.Specificity ?? StyleSpecificity.Zero;
        Specificity = new StyleSpecificity(
            parentSpecificity.IdCount + (id is null ? 0 : 1),
            parentSpecificity.ClassOrStateCount +
                classes.Count +
                states.Count +
                pseudoElementStates.Count,
            parentSpecificity.TypeCount +
                (typeName is null ? 0 : 1) +
                (pseudoElement is null ? 0 : 1));
    }

    /// <summary>Matches every node.</summary>
    public static StyleSelector Universal { get; } =
        new(null, null, Empty, Empty, null, Empty, null, null);

    public string? TypeName { get; }

    public string? Id { get; }

    public IReadOnlyList<string> Classes { get; }

    public IReadOnlyList<string> States { get; }

    /// <summary>The painted part targeted by the selector, such as <c>thumb</c>.</summary>
    public string? PseudoElement { get; }

    /// <summary>States interpreted on the pseudo-element rather than its owner.</summary>
    public IReadOnlyList<string> PseudoElementStates { get; }

    public StyleSelector? Parent { get; }

    public StyleCombinator? Combinator { get; }

    /// <summary>The specificity of the complete selector chain.</summary>
    public StyleSpecificity Specificity { get; }

    public bool HasState => States.Count != 0 || Parent?.HasState == true;

    public bool HasPseudoElement => PseudoElement is not null;

    public static StyleSelector Type(string typeName) {
        ValidateIdentifier(typeName, nameof(typeName));
        return new StyleSelector(typeName, null, Empty, Empty, null, Empty, null, null);
    }

    public static StyleSelector Class(string className) {
        ValidateIdentifier(className, nameof(className));
        return new StyleSelector(
            null,
            null,
            [className],
            Empty,
            null,
            Empty,
            null,
            null);
    }

    public static StyleSelector State(string state) {
        ValidateIdentifier(state, nameof(state));
        return new StyleSelector(
            null,
            null,
            Empty,
            [state],
            null,
            Empty,
            null,
            null);
    }

    public StyleSelector WithType(string typeName) {
        ValidateIdentifier(typeName, nameof(typeName));
        return Clone(typeName: typeName);
    }

    public StyleSelector WithId(string id) {
        ValidateIdentifier(id, nameof(id));
        return Clone(id: id);
    }

    public StyleSelector WithClass(string className) {
        ValidateIdentifier(className, nameof(className));
        if (Classes.Contains(className, StringComparer.Ordinal)) {
            return this;
        }

        return Clone(classes: [.. Classes, className]);
    }

    public StyleSelector WithState(string state) {
        ValidateIdentifier(state, nameof(state));
        if (States.Contains(state, StringComparer.Ordinal)) {
            return this;
        }

        return Clone(states: [.. States, state]);
    }

    public StyleSelector WithPseudoElement(string pseudoElement) {
        ValidateIdentifier(pseudoElement, nameof(pseudoElement));
        return Clone(pseudoElement: pseudoElement);
    }

    public StyleSelector WithPseudoElementState(string state) {
        ValidateIdentifier(state, nameof(state));
        if (PseudoElement is null) {
            throw new InvalidOperationException(
                "A pseudo-element must be specified before one of its states.");
        }

        if (PseudoElementStates.Contains(state, StringComparer.Ordinal)) {
            return this;
        }

        return Clone(pseudoElementStates: [.. PseudoElementStates, state]);
    }

    public StyleSelector DescendantOf(StyleSelector parent) {
        ArgumentNullException.ThrowIfNull(parent);
        return Clone(parent: parent, combinator: StyleCombinator.Descendant);
    }

    public StyleSelector ChildOf(StyleSelector parent) {
        ArgumentNullException.ThrowIfNull(parent);
        return Clone(parent: parent, combinator: StyleCombinator.Child);
    }

    public bool Equals(StyleSelector? other) {
        if (other is null ||
            !string.Equals(TypeName, other.TypeName, StringComparison.Ordinal) ||
            !string.Equals(Id, other.Id, StringComparison.Ordinal) ||
            !string.Equals(PseudoElement, other.PseudoElement, StringComparison.Ordinal) ||
            Combinator != other.Combinator ||
            !Classes.SequenceEqual(other.Classes, StringComparer.Ordinal) ||
            !States.SequenceEqual(other.States, StringComparer.Ordinal) ||
            !PseudoElementStates.SequenceEqual(
                other.PseudoElementStates,
                StringComparer.Ordinal)) {
            return false;
        }

        return Parent is null ? other.Parent is null : Parent.Equals(other.Parent);
    }

    public override bool Equals(object? obj) => Equals(obj as StyleSelector);

    public override int GetHashCode() {
        var hash = new HashCode();
        hash.Add(TypeName, StringComparer.Ordinal);
        hash.Add(Id, StringComparer.Ordinal);
        hash.Add(PseudoElement, StringComparer.Ordinal);
        hash.Add(Combinator);
        foreach (var className in Classes) {
            hash.Add(className, StringComparer.Ordinal);
        }

        foreach (var state in States) {
            hash.Add(state, StringComparer.Ordinal);
        }

        foreach (var state in PseudoElementStates) {
            hash.Add(state, StringComparer.Ordinal);
        }

        hash.Add(Parent);
        return hash.ToHashCode();
    }

    public override string ToString() {
        var current = CompoundText();
        return Parent is null
            ? current
            : Parent + (Combinator == StyleCombinator.Child ? " > " : " ") + current;
    }

    private string CompoundText() {
        var text = TypeName ?? string.Empty;
        if (Id is not null) {
            text += "#" + Id;
        }

        foreach (var className in Classes) {
            text += "." + className;
        }

        foreach (var state in States) {
            text += ":" + state;
        }

        if (PseudoElement is not null) {
            text += "::" + PseudoElement;
        }

        foreach (var state in PseudoElementStates) {
            text += ":" + state;
        }

        return text.Length == 0 ? "*" : text;
    }

    private StyleSelector Clone(
        string? typeName = null,
        string? id = null,
        IReadOnlyList<string>? classes = null,
        IReadOnlyList<string>? states = null,
        string? pseudoElement = null,
        IReadOnlyList<string>? pseudoElementStates = null,
        StyleSelector? parent = null,
        StyleCombinator? combinator = null) {
        return new StyleSelector(
            typeName ?? TypeName,
            id ?? Id,
            classes ?? Classes,
            states ?? States,
            pseudoElement ?? PseudoElement,
            pseudoElementStates ?? PseudoElementStates,
            parent ?? Parent,
            combinator ?? Combinator);
    }

    private static void ValidateIdentifier(string value, string parameterName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if ((!char.IsLetter(value[0]) && value[0] != '_') ||
            value.Any(static character =>
                !char.IsLetterOrDigit(character) && character is not ('_' or '-'))) {
            throw new ArgumentException(
                $"'{value}' is not a valid selector identifier.",
                parameterName);
        }
    }
}

public enum StyleCombinator {
    Descendant,
    Child,
}
