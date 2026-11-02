using System.Xml.Linq;

namespace Melanopsin.UI.Markup.Compiler.Parsing.Templates;

internal readonly record struct MarkupTemplateToken(
    MarkupTemplateTokenKind Kind,
    int Offset,
    string Value = "",
    XElement? Element = null,
    int ValueOffset = -1) {
    public static MarkupTemplateToken Text(string value, int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.Text, offset, value);
    }

    public static MarkupTemplateToken FromElement(XElement element, int offset) {
        return new MarkupTemplateToken(
            MarkupTemplateTokenKind.Element,
            offset,
            Element: element);
    }

    public static MarkupTemplateToken If(string condition, int offset, int conditionOffset) {
        return new MarkupTemplateToken(
            MarkupTemplateTokenKind.If,
            offset,
            condition,
            ValueOffset: conditionOffset);
    }

    public static MarkupTemplateToken ElseIf(string condition, int offset, int conditionOffset) {
        return new MarkupTemplateToken(
            MarkupTemplateTokenKind.ElseIf,
            offset,
            condition,
            ValueOffset: conditionOffset);
    }

    public static MarkupTemplateToken Else(int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.Else, offset);
    }

    public static MarkupTemplateToken For(string header, int offset, int headerOffset) {
        return new MarkupTemplateToken(
            MarkupTemplateTokenKind.For,
            offset,
            header,
            ValueOffset: headerOffset);
    }

    public static MarkupTemplateToken Empty(int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.Empty, offset);
    }

    public static MarkupTemplateToken Switch(string expression, int offset, int expressionOffset) {
        return new MarkupTemplateToken(
            MarkupTemplateTokenKind.Switch,
            offset,
            expression,
            ValueOffset: expressionOffset);
    }

    public static MarkupTemplateToken Case(string pattern, int offset, int patternOffset) {
        return new MarkupTemplateToken(
            MarkupTemplateTokenKind.Case,
            offset,
            pattern,
            ValueOffset: patternOffset);
    }

    public static MarkupTemplateToken Default(int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.Default, offset);
    }

    public static MarkupTemplateToken Code(string text, int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.Code, offset, text);
    }

    public static MarkupTemplateToken EndBlock(int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.EndBlock, offset);
    }
}
