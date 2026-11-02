using System.Globalization;
using System.Xml.Linq;
using Melanopsin.Graphics;
using Melanopsin.Graphics.Text.Fonts;
using Melanopsin.UI.App;

namespace Melanopsin.UI.ResourceCompiler;

internal static partial class ResourceValueReader {
    private static readonly (string Suffix, LengthUnit Unit)[] LengthSuffixes = [
        ("rem", LengthUnit.Rem),
        ("su", LengthUnit.SpaceUnit),
        ("ru", LengthUnit.RadiusUnit),
        ("fu", LengthUnit.FontUnit),
        ("iu", LengthUnit.IconUnit),
        ("hx", LengthUnit.Hairline),
        ("vw", LengthUnit.ViewportWidth),
        ("vh", LengthUnit.ViewportHeight),
        ("fr", LengthUnit.Fraction),
        ("em", LengthUnit.Em),
        ("dp", LengthUnit.DeviceIndependentPixel),
        ("sp", LengthUnit.ScaleIndependentPixel),
        ("pt", LengthUnit.Point),
        ("px", LengthUnit.Pixels),
        ("%", LengthUnit.Percent),
    ];

    private static object ReadValue(
        XElement element,
        ResourceDescriptor descriptor,
        string path) {
        var source = ReadOptionalAttribute(element, "src");
        var text = ReadOptionalAttribute(element, "value");
        if (source is not null && text is not null) {
            throw new BuildException(
                $"Resource value '{descriptor.Id}' in '{path}' cannot specify " +
                "both value and src.");
        }

        if (source is null && text is null) {
            text = element.Value;
        }

        if (descriptor.Kind is ResourceKind.Image or ResourceKind.Font or ResourceKind.Binary) {
            return ReadBinaryValue(element, descriptor, path, source);
        }

        var valueText = source is null
            ? text ?? string.Empty
            : ReadText(ResolveSourcePath(path, source));
        return descriptor.Kind switch {
            ResourceKind.String or ResourceKind.Message => valueText,
            ResourceKind.Color => ParseColor(descriptor.Id, path, valueText),
            ResourceKind.Length => ParseLength(
                descriptor.Id,
                path,
                valueText),
            ResourceKind.Number => ParseNumber(
                descriptor.Id,
                path,
                valueText),
            _ => throw new BuildException(
                $"Resource kind '{descriptor.Kind}' is not supported by the " +
                "resource compiler."),
        };
    }

    private static object ReadBinaryValue(
        XElement element,
        ResourceDescriptor descriptor,
        string path,
        string? source) {
        if (source is null) {
            throw new BuildException(
                $"Resource value '{descriptor.Id}' in '{path}' requires src.");
        }

        var sourcePath = ResolveSourcePath(path, source);
        var bytes = ReadBytes(sourcePath);
        return descriptor.Kind switch {
            ResourceKind.Image => new ImageResource(
                bytes,
                ReadOptionalAttribute(element, "mediaType") ??
                    MediaTypeFromPath(sourcePath)),
            ResourceKind.Font => FontFace.Parse(bytes, sourcePath),
            _ => bytes,
        };
    }

    private static string ResolveSourcePath(string valuesPath, string source) {
        if (System.IO.Path.IsPathRooted(source)) {
            return System.IO.Path.GetFullPath(source);
        }

        return System.IO.Path.GetFullPath(
            System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(valuesPath) ??
                    Environment.CurrentDirectory,
                source));
    }

    private static byte[] ReadBytes(string path) {
        try {
            return File.ReadAllBytes(path);
        } catch (IOException exception) {
            throw new BuildException(
                $"Could not read resource source '{path}': {exception.Message}",
                exception);
        }
    }

    private static string ReadText(string path) {
        try {
            return File.ReadAllText(path);
        } catch (IOException exception) {
            throw new BuildException(
                $"Could not read resource source '{path}': {exception.Message}",
                exception);
        }
    }

    private static Color ParseColor(
        string id,
        string path,
        string text) {
        var value = text.Trim();
        if (value.StartsWith('#')) {
            value = value[1..];
        }

        if (value.Length is 3 or 4) {
            var expanded = new char[value.Length * 2];
            for (var index = 0; index < value.Length; index++) {
                expanded[index * 2] = value[index];
                expanded[(index * 2) + 1] = value[index];
            }

            value = new string(expanded);
        }

        var alpha = byte.MaxValue;
        if (value.Length is not 6 and not 8 ||
            !byte.TryParse(value[..2], NumberStyles.HexNumber, null, out var red) ||
            !byte.TryParse(value[2..4], NumberStyles.HexNumber, null, out var green) ||
            !byte.TryParse(value[4..6], NumberStyles.HexNumber, null, out var blue) ||
            (value.Length == 8 &&
             !byte.TryParse(value[6..8], NumberStyles.HexNumber, null, out alpha))) {
            throw new BuildException(
                $"Resource value '{id}' in '{path}' has invalid color '{text}'.");
        }

        return new Color(red, green, blue, alpha);
    }

    private static Length ParseLength(
        string id,
        string path,
        string text) {
        var value = text.Trim();
        if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase)) {
            return Length.Auto;
        }

        var unit = LengthUnit.Pixels;
        foreach (var candidate in LengthSuffixes) {
            if (!value.EndsWith(
                    candidate.Suffix,
                    StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            value = value[..^candidate.Suffix.Length];
            unit = candidate.Unit;
            break;
        }

        if (!float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var amount) ||
            !float.IsFinite(amount) ||
            amount < 0) {
            throw new BuildException(
                $"Resource value '{id}' in '{path}' has invalid length '{text}'.");
        }

        return new Length(amount, unit);
    }

    private static double ParseNumber(
        string id,
        string path,
        string text) {
        if (!double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number) ||
            !double.IsFinite(number)) {
            throw new BuildException(
                $"Resource value '{id}' in '{path}' has invalid number '{text}'.");
        }

        return number;
    }

    private static string? MediaTypeFromPath(string path) {
        return System.IO.Path.GetExtension(path).ToLowerInvariant() switch {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".ico" => "image/x-icon",
            _ => null,
        };
    }
}
