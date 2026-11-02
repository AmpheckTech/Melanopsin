using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Melanopsin.UI.App;

namespace Melanopsin.UI.ResourceCompiler;

internal static partial class ResourceValueReader {
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
            if (!contract.Contract.TryGetDescriptor(id, out var descriptor) ||
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

}
