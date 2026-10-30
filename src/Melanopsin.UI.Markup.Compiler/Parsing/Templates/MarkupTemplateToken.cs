using System.Xml.Linq;

namespace Melanopsin.UI.Markup.Compiler.Parsing.Templates;

internal readonly record struct MarkupTemplateToken(
    MarkupTemplateTokenKind Kind,
    int Offset,
    string Value = "",
    XElement? Element = null) {
    public static MarkupTemplateToken Text(string value, int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.Text, offset, value);
    }

    public static MarkupTemplateToken FromElement(XElement element, int offset) {
        return new MarkupTemplateToken(
            MarkupTemplateTokenKind.Element,
            offset,
            Element: element);
    }

    public static MarkupTemplateToken If(string condition, int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.If, offset, condition);
    }

    public static MarkupTemplateToken ElseIf(string condition, int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.ElseIf, offset, condition);
    }

    public static MarkupTemplateToken Else(int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.Else, offset);
    }

    public static MarkupTemplateToken For(string header, int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.For, offset, header);
    }

    public static MarkupTemplateToken Empty(int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.Empty, offset);
    }

    public static MarkupTemplateToken Switch(string expression, int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.Switch, offset, expression);
    }

    public static MarkupTemplateToken Case(string pattern, int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.Case, offset, pattern);
    }

    public static MarkupTemplateToken Default(int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.Default, offset);
    }

    public static MarkupTemplateToken EndBlock(int offset) {
        return new MarkupTemplateToken(MarkupTemplateTokenKind.EndBlock, offset);
    }
}
