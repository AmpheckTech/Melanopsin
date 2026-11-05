using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal sealed class CssStylesheet {
    public CssStylesheet(
        string path,
        string source,
        IReadOnlyList<CssImport> imports,
        IReadOnlyList<CssRule> rules,
        IReadOnlyList<CssSlot> slots,
        IReadOnlyList<CssDiagnostic> diagnostics) {
        Path = path;
        Source = source;
        Imports = imports;
        Rules = rules;
        Slots = slots;
        Diagnostics = diagnostics;
    }

    public string Path { get; }

    public string Source { get; }

    public IReadOnlyList<CssImport> Imports { get; }

    public IReadOnlyList<CssRule> Rules { get; }

    public IReadOnlyList<CssSlot> Slots { get; }

    public IReadOnlyList<CssDiagnostic> Diagnostics { get; }
}

internal sealed class CssImport {
    public CssImport(string path, int offset, int length) {
        Path = path;
        Offset = offset;
        Length = length;
    }

    public string Path { get; }

    public int Offset { get; }

    public int Length { get; }
}

internal sealed class CssRule {
    public CssRule(
        CssComplexSelector selector,
        int offset,
        IReadOnlyList<CssDeclaration> declarations) {
        Selector = selector;
        Offset = offset;
        Declarations = declarations;
    }

    public CssComplexSelector Selector { get; }

    public int Offset { get; }

    public IReadOnlyList<CssDeclaration> Declarations { get; }
}

internal sealed class CssSlot {
    public CssSlot(
        string name,
        string typeName,
        string value,
        int offset,
        int length) {
        Name = name;
        TypeName = typeName;
        Value = value;
        Offset = offset;
        Length = length;
    }

    public string Name { get; }

    public string TypeName { get; }

    public string Value { get; }

    public int Offset { get; }

    public int Length { get; }
}

internal sealed class CssDeclaration {
    public CssDeclaration(
        string name,
        string value,
        int offset,
        int valueOffset,
        int length) {
        Name = name;
        Value = value;
        Offset = offset;
        ValueOffset = valueOffset;
        Length = length;
    }

    public string Name { get; }

    public string Value { get; }

    public int Offset { get; }

    public int ValueOffset { get; }

    public int Length { get; }
}

internal sealed class CssStyleBlockItem {
    private CssStyleBlockItem(CssDeclaration? declaration, CssNestedRule? rule) {
        Declaration = declaration;
        Rule = rule;
    }

    public CssDeclaration? Declaration { get; }

    public CssNestedRule? Rule { get; }

    public static CssStyleBlockItem FromDeclaration(CssDeclaration declaration) {
        return new CssStyleBlockItem(declaration, null);
    }

    public static CssStyleBlockItem FromRule(CssNestedRule rule) {
        return new CssStyleBlockItem(null, rule);
    }
}

internal sealed class CssNestedRule {
    public CssNestedRule(
        string selector,
        int offset,
        IReadOnlyList<CssStyleBlockItem> items) {
        Selector = selector;
        Offset = offset;
        Items = items;
    }

    public string Selector { get; }

    public int Offset { get; }

    public IReadOnlyList<CssStyleBlockItem> Items { get; }
}

internal sealed class CssDiagnostic {
    public CssDiagnostic(string code, string message, int offset, int length) {
        Code = code;
        Message = message;
        Offset = offset;
        Length = length;
    }

    public string Code { get; }

    public string Message { get; }

    public int Offset { get; }

    public int Length { get; }
}
