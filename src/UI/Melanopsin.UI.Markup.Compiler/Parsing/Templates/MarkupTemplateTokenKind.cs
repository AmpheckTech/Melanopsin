namespace Melanopsin.UI.Markup.Compiler.Parsing.Templates;

internal enum MarkupTemplateTokenKind {
    Text,
    Element,
    If,
    ElseIf,
    Else,
    For,
    Empty,
    Switch,
    Case,
    Default,
    Code,
    EndBlock,
}
