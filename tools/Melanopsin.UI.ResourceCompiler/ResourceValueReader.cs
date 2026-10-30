using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Melanopsin.Graphics;
using Melanopsin.Graphics.Text.Fonts;
using Melanopsin.UI.App;

namespace Melanopsin.UI.ResourceCompiler;

internal static class ResourceValueReader {
    private const string ResourceNamespace = "https://melanopsin.dev/resources";

    public static IReadOnlyList<ResourcePackValue> Read(
        ManifestContract contract,
        string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return Array.Empty<ResourcePackValue>();
        }

        var fullPath = System.IO.Path.GetFullPath(path);
        XDocument document;
        try {
            document = XDocument.Load(
                fullPath,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        } catch (XmlException exception) {
            throw new BuildException(
                $"Resource values '{fullPath}' are not valid XML: " +
                exception.Message,
                exception);
        }

        var root = document.Root ??
            throw new BuildException(
                $"Resource values '{fullPath}' have no root element.");
        if (root.Name.NamespaceName != ResourceNamespace ||
            root.Name.LocalName is not "ResourcePack" and not "Values") {
            throw new BuildException(
                $"Resource values '{fullPath}' must use the '" +
                $"{ResourceNamespace}' namespace and a ResourcePack or Values " +
                "root element.");
        }

        var package = ReadPackage(root);
        if (!string.Equals(package, contract.Package, StringComparison.Ordinal)) {
            throw new BuildException(
                $"Resource values '{fullPath}' declare package '{package}', " +
                $"but manifest '{contract.Path}' declares " +
                $"'{contract.Package}'.");
        }

        var values = new List<ResourcePackValue>();
        var seenVariants = new HashSet<(string Id, ResourceVariant Variant)>();
        foreach (var element in root.Elements()) {
            if (!string.Equals(
                    element.Name.LocalName,
                    "Value",
                    StringComparison.Ordinal) ||
                (element.Name.NamespaceName.Length != 0 &&
                 element.Name.NamespaceName != ResourceNamespace)) {
                throw new BuildException(
                    $"Resource values '{fullPath}' may contain only Value " +
                    "elements.");
            }

            var id = ReadRequiredAttribute(element, "id").Trim();
            if (!contract.TryGetDescriptor(id, out var descriptor) ||
                descriptor is null) {
                throw new BuildException(
                    $"Resource value '{id}' is not declared by manifest " +
                    $"'{contract.Path}'.");
            }

            var variant = ReadVariant(element, descriptor, fullPath, id);
            if (!seenVariants.Add((id, variant))) {
                throw new BuildException(
                    $"Resource value '{id}' in '{fullPath}' contains a " +
                    "duplicate variant.");
            }

            var value = ReadValue(element, descriptor, fullPath);
            values.Add(new ResourcePackValue(id, variant, value));
        }

        return values;
    }

    private static string ReadPackage(XElement root) {
        var namespaced = root.Attribute(XName.Get("package", ResourceNamespace));
        var plain = root.Attribute("package");
        var value = (namespaced ?? plain)?.Value.Trim();
        if (string.IsNullOrWhiteSpace(value)) {
            throw new BuildException(
                "A resource values root must declare m:package or package.");
        }

        return value;
    }

    private static ResourceVariant ReadVariant(
        XElement element,
        ResourceDescriptor descriptor,
        string path,
        string id) {
        var cultureText = ReadOptionalAttribute(element, "culture");
        var theme = ReadOptionalAttribute(element, "theme");
        var style = ReadOptionalAttribute(element, "style");
        var scaleText = ReadOptionalAttribute(element, "displayScale") ??
            ReadOptionalAttribute(element, "scale");

        string? culture = null;
        if (cultureText is not null) {
            RequireVariant(descriptor, ResourceVariantKinds.Culture, path, id);
            if (string.IsNullOrWhiteSpace(cultureText)) {
                throw new BuildException(
                    $"Resource value '{id}' in '{path}' declares unknown " +
                    $"culture '{cultureText}'.");
            }

            culture = cultureText;
        }

        if (theme is not null) {
            RequireVariant(descriptor, ResourceVariantKinds.Theme, path, id);
        }

        if (style is not null) {
            RequireVariant(descriptor, ResourceVariantKinds.Style, path, id);
        }

        var displayScale = 1.0f;
        if (scaleText is not null) {
            RequireVariant(
                descriptor,
                ResourceVariantKinds.DisplayScale,
                path,
                id);
            if (!float.TryParse(
                    scaleText,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out displayScale) ||
                !float.IsFinite(displayScale) ||
                displayScale <= 0) {
                throw new BuildException(
                    $"Resource value '{id}' in '{path}' declares invalid " +
                    $"display scale '{scaleText}'.");
            }
        }

        return new ResourceVariant {
            Culture = culture,
            Theme = theme,
            Style = style,
            DisplayScale = displayScale,
        };
    }

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

        var valueText = text ?? string.Empty;
        if (source is not null) {
            valueText = ReadText(ResolveSourcePath(path, source));
        }

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

    private static void RequireVariant(
        ResourceDescriptor descriptor,
        ResourceVariantKinds kind,
        string path,
        string id) {
        if ((descriptor.Variants & kind) == 0) {
            throw new BuildException(
                $"Resource value '{id}' in '{path}' uses variant '{kind}' " +
                "that is not declared by the manifest.");
        }
    }

    private static string? ReadOptionalAttribute(
        XElement element,
        string name) {
        var value = element.Attribute(name)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ReadRequiredAttribute(
        XElement element,
        string name) {
        var value = ReadOptionalAttribute(element, name);
        if (value is null) {
            throw new BuildException(
                $"Resource value '{element.Name.LocalName}' requires '{name}'.");
        }

        return value;
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
        if (!Length.TryParse(text, out var length)) {
            throw new BuildException(
                $"Resource value '{id}' in '{path}' has invalid length '{text}'.");
        }

        return length;
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
