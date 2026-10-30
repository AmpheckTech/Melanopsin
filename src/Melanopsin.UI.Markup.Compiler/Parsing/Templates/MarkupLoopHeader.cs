namespace Melanopsin.UI.Markup.Compiler.Parsing.Templates;

internal readonly record struct MarkupLoopHeader(
    string Variable,
    string Iterable,
    string? Track);
