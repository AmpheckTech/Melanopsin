using Melanopsin.Graphics;
using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.UI.App;

/// <summary>
/// Immutable metadata for one resource contract entry.
/// </summary>
public sealed class ResourceDescriptor {
    public ResourceDescriptor(
        string id,
        ResourceKind kind,
        Type valueType,
        ResourceVariantKinds variants = ResourceVariantKinds.None) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(valueType);
        if ((variants & ~KnownVariants) != ResourceVariantKinds.None) {
            throw new ArgumentOutOfRangeException(
                nameof(variants),
                variants,
                "The resource descriptor contains an unknown variant dimension.");
        }

        if (!IsValueTypeCompatible(kind, valueType)) {
            throw new ArgumentException(
                $"Resource kind '{kind}' cannot use value type " +
                $"'{valueType.FullName}'.",
                nameof(valueType));
        }

        Id = id;
        Kind = kind;
        ValueType = valueType;
        Variants = variants;
    }

    public string Id { get; }

    public ResourceKind Kind { get; }

    public Type ValueType { get; }

    public ResourceVariantKinds Variants { get; }

    public override string ToString() {
        return Id;
    }

    private const ResourceVariantKinds KnownVariants =
        ResourceVariantKinds.Culture |
        ResourceVariantKinds.Theme |
        ResourceVariantKinds.Style |
        ResourceVariantKinds.DisplayScale;

    private static bool IsValueTypeCompatible(
        ResourceKind kind,
        Type valueType) {
        return kind switch {
            ResourceKind.String or ResourceKind.Message =>
                valueType == typeof(string),
            ResourceKind.Image => valueType == typeof(ImageResource),
            ResourceKind.Font => valueType == typeof(FontFace),
            ResourceKind.Color => valueType == typeof(Color),
            ResourceKind.Length => valueType == typeof(Length),
            ResourceKind.Number => valueType == typeof(double),
            ResourceKind.Binary => valueType == typeof(byte[]),
            ResourceKind.Object => valueType == typeof(object),
            _ => false,
        };
    }
}
