using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Melanopsin.Graphics;
using Melanopsin.Graphics.Text.Fonts;
using Melanopsin.UI.App;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.ResourceCompiler;

internal static class ManifestReader {
    private const string ResourceNamespace = "https://melanopsin.dev/resources";

    public static ManifestContract Read(string path) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = System.IO.Path.GetFullPath(path);
        XDocument document;
        try {
            document = XDocument.Load(
                fullPath,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        } catch (XmlException exception) {
            throw new BuildException(
                $"Resource manifest '{fullPath}' is not valid XML: {exception.Message}",
                exception);
        }

        var root = document.Root ??
            throw new BuildException(
                $"Resource manifest '{fullPath}' has no root element.");
        if (root.Name.NamespaceName != ResourceNamespace ||
            root.Name.LocalName is not "Resources" and not "ResourceManifest") {
            throw new BuildException(
                $"Resource manifest '{fullPath}' must use the " +
                $"'{ResourceNamespace}' namespace and a Resources root element.");
        }

        var package = ReadRequiredAttribute(root, XName.Get("package", ResourceNamespace))
            .Trim();
        if (SyntaxFactory.ParseName(package).ContainsDiagnostics) {
            throw new BuildException(
                $"Resource package '{package}' is not a valid C# namespace.");
        }

        foreach (var className in ReadClassNames(root)) {
            if (!SyntaxFacts.IsValidIdentifier(className)) {
                throw new BuildException(
                    $"Resource class '{className}' is not a valid C# class name.");
            }
        }

        var descriptors = new List<ResourceDescriptor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var categoryElement in root.Elements()) {
            if (!TryGetCategory(
                    categoryElement.Name.LocalName,
                    out var entryName,
                    out var kind,
                    out var valueType)) {
                throw new BuildException(
                    $"Resource manifest '{fullPath}' contains unknown category " +
                    $"'{categoryElement.Name.LocalName}'.");
            }

            foreach (var entry in categoryElement.Elements()) {
                if (!string.Equals(
                        entry.Name.LocalName,
                        entryName,
                        StringComparison.Ordinal)) {
                    throw new BuildException(
                        $"Resource category '{categoryElement.Name.LocalName}' " +
                        $"requires '{entryName}' entries.");
                }

                var identifier = ReadRequiredAttribute(entry, "id").Trim();
                if (!SyntaxFacts.IsValidIdentifier(identifier)) {
                    throw new BuildException(
                        $"Resource id '{identifier}' is not a valid identifier.");
                }

                var id = categoryElement.Name.LocalName + "." + identifier;
                if (!seen.Add(id)) {
                    throw new BuildException(
                        $"Resource '{id}' is declared more than once.");
                }

                descriptors.Add(new ResourceDescriptor(
                    id,
                    kind,
                    valueType,
                    ReadVariants(entry, fullPath, id)));
            }
        }

        if (descriptors.Count == 0) {
            throw new BuildException(
                $"Resource manifest '{fullPath}' must declare at least one resource.");
        }

        return new ManifestContract(fullPath, package, descriptors);
    }

    private static IEnumerable<string> ReadClassNames(XElement root) {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attribute in root.Attributes()) {
            if (attribute.Name.NamespaceName != ResourceNamespace ||
                !string.Equals(
                    attribute.Name.LocalName,
                    "class",
                    StringComparison.Ordinal)) {
                continue;
            }

            var value = attribute.Value.Trim();
            if (string.IsNullOrWhiteSpace(value)) {
                throw new BuildException(
                    "Resource manifest element 'Resources' requires 'class'.");
            }

            values.Add(value);
        }

        return values;
    }

    private static string ReadRequiredAttribute(
        XElement element,
        XName name) {
        var value = element.Attribute(name)?.Value.Trim();
        if (string.IsNullOrWhiteSpace(value)) {
            throw new BuildException(
                $"Resource manifest element '{element.Name.LocalName}' requires " +
                $"'{name.LocalName}'.");
        }

        return value;
    }

    private static ResourceVariantKinds ReadVariants(
        XElement entry,
        string path,
        string id) {
        var attribute = entry.Attribute("variants");
        if (attribute is null || string.IsNullOrWhiteSpace(attribute.Value)) {
            return ResourceVariantKinds.None;
        }

        var result = ResourceVariantKinds.None;
        var seen = new HashSet<ResourceVariantKinds>();
        foreach (var part in attribute.Value.Split(',')) {
            var name = part.Trim();
            var kind = name switch {
                "Culture" => ResourceVariantKinds.Culture,
                "Theme" => ResourceVariantKinds.Theme,
                "Style" => ResourceVariantKinds.Style,
                "DisplayScale" => ResourceVariantKinds.DisplayScale,
                _ => throw new BuildException(
                    $"Resource '{id}' in '{path}' declares unknown variant " +
                    $"'{name}'."),
            };
            if (!seen.Add(kind)) {
                throw new BuildException(
                    $"Resource '{id}' in '{path}' declares variant '{name}' " +
                    "more than once.");
            }

            result |= kind;
        }

        return result;
    }

    private static bool TryGetCategory(
        string category,
        out string entryName,
        out ResourceKind kind,
        out Type valueType) {
        switch (category) {
            case "Strings":
                entryName = "String";
                kind = ResourceKind.String;
                valueType = typeof(string);
                return true;
            case "Messages":
                entryName = "Message";
                kind = ResourceKind.Message;
                valueType = typeof(string);
                return true;
            case "Images":
                entryName = "Image";
                kind = ResourceKind.Image;
                valueType = typeof(ImageResource);
                return true;
            case "Fonts":
                entryName = "Font";
                kind = ResourceKind.Font;
                valueType = typeof(FontFace);
                return true;
            case "Colors":
                entryName = "Color";
                kind = ResourceKind.Color;
                valueType = typeof(Color);
                return true;
            case "Lengths":
                entryName = "Length";
                kind = ResourceKind.Length;
                valueType = typeof(Length);
                return true;
            case "Numbers":
                entryName = "Number";
                kind = ResourceKind.Number;
                valueType = typeof(double);
                return true;
            case "Binaries":
                entryName = "Binary";
                kind = ResourceKind.Binary;
                valueType = typeof(byte[]);
                return true;
            default:
                entryName = string.Empty;
                kind = default;
                valueType = typeof(object);
                return false;
        }
    }

}
