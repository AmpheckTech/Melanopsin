using Melanopsin.UI;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Demo.Views;

[MarkupComponent]
public sealed partial class TypographySample : Component {
    public IReadOnlyList<TypeSpecimen> TypeScale => TypographySpecimens.TypeScale;

    public IReadOnlyList<TextSpecimen> BidiCases => TypographySpecimens.Bidi;

    public IReadOnlyList<TextSpecimen> ScriptCases => TypographySpecimens.Scripts;

    public IReadOnlyList<TextSpecimen> EmojiCases => TypographySpecimens.Emoji;

    public IReadOnlyList<RichParagraph> RichSpecimenRows => TypographySpecimens.Rich;

    public string TypeParagraph =>
        "公开的布局引擎在每一次失效后只重新测量受影响的子树，因此同一段文字可以在不同宽度下" +
        "自由折行。这里故意放了一段足够长的中文，用来观察断行位置、行间距以及标点的悬挂效果。";

    public string TypeMixedScript =>
        "Mixed script 0123456789 — “quotes”, (brackets) and … ellipsis. 中英混排时，" +
        "标点会按 Unicode 规则处理，数字与拉丁字母保持各自的字形宽度。";

    public string TypeLongLine =>
        "A single unwrapped line is measured once and trimmed with an ellipsis instead of wrapping.";
}
