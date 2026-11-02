using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.UI.Desktop;

internal static class SystemFonts {
    public static FontFace LoadPrimary() {
        var configured = Environment.GetEnvironmentVariable("MELANOPSIN_FONT");
        if (!string.IsNullOrWhiteSpace(configured)) {
            return FontFace.Load(configured);
        }

        string[] candidates = OperatingSystem.IsWindows()
            ? [
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Fonts",
                    "segoeui.ttf"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Fonts",
                    "arial.ttf"),
            ]
            : [
                "/usr/share/fonts/TTF/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            ];
        var path = candidates.FirstOrDefault(File.Exists) ??
            throw new FileNotFoundException(
                "Set MELANOPSIN_FONT to a TrueType font file.");
        return FontFace.Load(path);
    }

    public static IEnumerable<(string Path, int FaceIndex)> LoadFallbacks() {
        var configured = Environment.GetEnvironmentVariable(
            "MELANOPSIN_FALLBACK_FONTS");
        if (string.Equals(configured, "none", StringComparison.OrdinalIgnoreCase)) {
            return [];
        }

        if (!string.IsNullOrWhiteSpace(configured)) {
            return configured.Split(
                    Path.PathSeparator,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(TryParse)
                .OfType<(string Path, int FaceIndex)>()
                .ToArray();
        }

        return Groups()
            .Select(group => group.Select(TryParse).FirstOrDefault(
                static font => font is not null))
            .OfType<(string Path, int FaceIndex)>()
            .ToArray();
    }

    private static (string Path, int FaceIndex)? TryParse(string entry) {
        var separator = entry.LastIndexOf('#');
        var faceIndex = 0;
        var path = entry;
        if (separator > 0 &&
            int.TryParse(entry.AsSpan(separator + 1), out var parsed) &&
            parsed >= 0) {
            path = entry[..separator];
            faceIndex = parsed;
        }

        return File.Exists(path) ? (path, faceIndex) : null;
    }

    private static string[][] Groups() {
        if (OperatingSystem.IsWindows()) {
            var fonts = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Fonts");
            return [
                [Path.Combine(fonts, "msyh.ttc"), Path.Combine(fonts, "simsun.ttc")],
                [Path.Combine(fonts, "seguiemj.ttf")],
                [Path.Combine(fonts, "segoeuib.ttf")],
            ];
        }

        return [
            [
                "/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc#2",
                "/usr/share/fonts/TTF/simsun.ttc",
            ],
            [
                "/usr/share/fonts/noto/NotoColorEmoji.ttf",
                "/usr/share/fonts/TTF/seguiemj.ttf",
            ],
            ["/usr/share/fonts/noto/NotoSansHebrew-Regular.ttf"],
            ["/usr/share/fonts/noto/NotoSansArabic-Regular.ttf"],
            ["/usr/share/fonts/noto/NotoSansDevanagari-Regular.ttf"],
            ["/usr/share/fonts/noto/NotoSansThai-Regular.ttf"],
        ];
    }
}
