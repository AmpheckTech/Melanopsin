// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;
using Melanopsin.Graphics.Text.Fonts;
using Melanopsin.UI;

namespace Melanopsin.UI.Demo.Views;

public sealed record TypeSpecimen(string Class, string Label, string Sample);

public sealed record TextSpecimen(string Label, string Sample);

public sealed record RichParagraph(string Label, IReadOnlyList<RichTextSpan> Spans);

internal static class TypographySpecimens {
    public static IReadOnlyList<TypeSpecimen> TypeScale { get; } = [
        new("type-display", "21 fu · display", "Melanopsin"),
        new("type-title", "16 fu · title", "Retained layout engine"),
        new("type-lead", "12 fu · lead", "Shaped once, cached between frames."),
        new("type-body", "11 fu · body", "Body copy keeps a comfortable measure."),
        new("type-small", "9 fu · small", "Small text stays legible at 100% scale."),
        new("type-caption", "8.5 fu · caption", "CAPTION · 8.5 FU"),
    ];

    public static IReadOnlyList<TextSpecimen> Bidi { get; } = [
        new("he · rtl base", "הטקסט נמדד ונשבר לפי כיווניות הבסיס של הפסקה."),
        new("ar · rtl base", "النص العربي يُشكَّل ويُكسَر حسب اتجاه الأساس."),
        new("rtl + latin", "שלום Melanopsin UI עולם"),
        new("rtl + digits · date", "الطلب رقم 12345 بتاريخ 2026-11-01"),
        new("rtl + digits · isolated", "الطلب رقم 12345 بتاريخ \u20662026-11-01\u2069"),
        new("rtl + digits · arabic-indic", "الطلب رقم ١٢٣٤٥ بتاريخ ٢٠٢٦/١١/٠١"),
        new("ltr + rtl island", "Melanopsin — שלום עולם — retained layout."),
        new("brackets · mirroring", "עברית (English) [123] עברית"),
        new("controls · lrm/rlm", "עברית \u200eabc 123\u200f עברית"),
        new(
            "wrap · rtl",
            "המנוע מודד כל שורה בנפרד, מסדר מחדש את הקטעים לפי רמת הכיווניות " +
            "ומדביק את סימני הפיסוק לקצה הנכון של השורה."),
    ];

    public static IReadOnlyList<TextSpecimen> Scripts { get; } = [
        new("devanagari · reorder", "देवनागरी लिपि में मात्राएँ पुनःक्रमित होती हैं।"),
        new("devanagari · conjuncts", "क्ष त्र ज्ञ श्री द्वि — conjunct clusters"),
        new("thai · no spaces", "ข้อความภาษาไทยไม่มีช่องว่างระหว่างคำ"),
        new("thai · marks", "น้ำ ขึ้น เก่ง ปี่ สวย — tone marks stack"),
        new("arabic · joining", "العربية تُوصَل حروفها حسب موقعها في الكلمة"),
        new("hebrew · niqqud", "שָׁלוֹם עוֹלָם — combining marks"),
        new("latin · combining", "e\u0301 a\u0308 n\u0303 a\u030a — combining accents"),
    ];

    public static IReadOnlyList<TextSpecimen> Emoji { get; } = [
        new("emoji · single", "😀 🎉 👍🏽 🚀 🧑‍💻"),
        new("emoji · zwj", "👨‍👩‍👧‍👦 👩🏽‍🚀 🏳️‍🌈"),
        new("emoji · flags", "🇨🇳 🇯🇵 🇺🇳 1️⃣ #️⃣"),
        new("emoji · presentation", "❤ ❤️ ☺ ☺️ ⚠ ⚠️"),
        new("emoji + cjk", "进度 80% ✅ 完成 · 文件已保存 💾"),
        new("emoji · rtl paragraph", "تم الحفظ بنجاح ✅ 🎉"),
        new("emoji · line height", "Text 🙂 emoji grows the line box."),
    ];

    private static readonly Color RichInk = new(223, 227, 231);
    private static readonly Color RichStrong = new(241, 244, 246);
    private static readonly Color RichMuted = new(150, 158, 167);
    private static readonly Color RichAccent = new(116, 185, 159);
    private static readonly FontFace? RichBoldFace = LoadFirstFace(
        Path.Combine(FontDirectory(), "DejaVuSans-Bold.ttf"),
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        Path.Combine(FontDirectory(), "seguisb.ttf"),
        Path.Combine(FontDirectory(), "arialbd.ttf"));
    private static readonly FontFace? RichMonoFace = LoadFirstFace(
        Path.Combine(FontDirectory(), "DejaVuSansMono.ttf"),
        "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
        Path.Combine(FontDirectory(), "consola.ttf"),
        Path.Combine(FontDirectory(), "cour.ttf"));

    public static IReadOnlyList<RichParagraph> Rich { get; } = [
        new(
            "inline · colour · size · face",
            [
                new("Retained ", RichInk),
                new("rich text", RichAccent, font: RichBoldFace),
                new(" flows through the same line breaker: ", RichInk),
                new("14 fu", RichStrong, fontSize: 14),
                new(" emphasis, ", RichInk),
                new("monospace", RichMuted, font: RichMonoFace),
                new(
                    " runs and colour all share one paragraph, so a sentence can " +
                    "mix treatments without nesting controls.",
                    RichInk),
            ]),
        new(
            "inline · cjk · emoji",
            [
                new("Progress 进度 ", RichInk),
                new("80%", RichAccent, fontSize: 13),
                new(" · 完成 ✅ 已保存 💾 · wrapped with the same line box", RichInk),
            ]),
        new(
            "inline · fallback inside a span",
            [
                new("Bold face falls back per rune: ", RichMuted),
                new("粵語 · हिन्दी · العربية", RichStrong, font: RichBoldFace),
            ]),
    ];

    private static string FontDirectory() {
        return OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Fonts")
            : "/usr/share/fonts";
    }

    private static FontFace? LoadFirstFace(params string[] candidates) {
        foreach (var candidate in candidates) {
            if (!File.Exists(candidate)) {
                continue;
            }

            try {
                return FontFace.Load(candidate);
            } catch (IOException) {
            } catch (InvalidDataException) {
            }
        }

        return null;
    }
}
