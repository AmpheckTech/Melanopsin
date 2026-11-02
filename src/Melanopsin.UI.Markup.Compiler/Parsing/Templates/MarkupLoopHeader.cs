namespace Melanopsin.UI.Markup.Compiler.Parsing.Templates;

/// <summary>
/// The three parts of an <c>@for</c> header. The offsets locate the iterable and the track
/// expression in the document, so the generated loop keeps pointing at what the author wrote.
/// </summary>
internal readonly record struct MarkupLoopHeader(
    string Variable,
    int IterableOffset,
    string Iterable,
    int TrackOffset,
    string? Track);
