using System.Text;
using Melanopsin.Graphics;
using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.UI.App;

/// <summary>
/// Writes the versioned binary resource pack format consumed by
/// <see cref="ResourcePack"/>. Build tooling uses this writer; applications
/// normally load packs produced at build time.
/// </summary>
public static class ResourcePackWriter {
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static void Write(
        Stream output,
        ResourceContract contract,
        IEnumerable<ResourcePackValue> values) {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(values);

        var order = new List<string>();
        var grouped = new Dictionary<string, List<ResourcePackValue>>(
            StringComparer.Ordinal);
        foreach (var value in values) {
            ArgumentNullException.ThrowIfNull(value);
            if (!grouped.TryGetValue(value.Id, out var resourceValues)) {
                resourceValues = [];
                grouped.Add(value.Id, resourceValues);
                order.Add(value.Id);
            }

            resourceValues.Add(value);
        }

        if (order.Count > ResourcePackFormat.MaxResources) {
            throw new ArgumentException(
                "The resource pack declares too many resources.",
                nameof(values));
        }

        var descriptors = new Dictionary<string, ResourceDescriptor>(
            StringComparer.Ordinal);
        foreach (var descriptor in contract.Descriptors) {
            descriptors.Add(descriptor.Id, descriptor);
        }

        ValidatePack(descriptors, order, grouped);

        using var writer = new BinaryWriter(
            output,
            Encoding.UTF8,
            leaveOpen: true);
        writer.Write(ResourcePackFormat.Magic);
        writer.Write(ResourcePackFormat.Version);
        writer.Write((ushort)0);
        writer.Write(checked((uint)order.Count));
        foreach (var id in order) {
            if (!descriptors.TryGetValue(id, out var descriptor)) {
                throw new ArgumentException(
                    $"Resource id '{id}' is not declared by the application contract.",
                    nameof(values));
            }

            WriteResource(writer, descriptor, grouped[id]);
        }

        writer.Flush();
    }

    private static void ValidatePack(
        IReadOnlyDictionary<string, ResourceDescriptor> descriptors,
        IReadOnlyList<string> order,
        IReadOnlyDictionary<string, List<ResourcePackValue>> grouped) {
        var totalValues = 0;
        long totalBlobBytes = 0;
        long totalDecodedBytes = 0;
        foreach (var id in order) {
            if (!descriptors.TryGetValue(id, out var descriptor)) {
                throw new ArgumentException(
                    $"Resource id '{id}' is not declared by the application contract.",
                    nameof(grouped));
            }

            _ = GetTextBytes(id);
            var resourceValues = grouped[id];
            if (resourceValues.Count > ResourcePackFormat.MaxValuesPerResource) {
                throw new ArgumentException(
                    $"Resource id '{descriptor.Id}' declares too many variants.",
                    nameof(grouped));
            }

            var seenVariants = new HashSet<ResourceVariant>();
            foreach (var value in resourceValues) {
                if (!seenVariants.Add(value.Variant)) {
                    throw new ArgumentException(
                        $"Resource id '{descriptor.Id}' contains a duplicate variant.",
                        nameof(grouped));
                }

                ValidateVariant(descriptor, value.Variant);
                var blob = ResourcePackCodec.Encode(descriptor, value.Value);
                if (blob.Length > ResourcePackFormat.MaxBlobBytes) {
                    throw new ArgumentException(
                        $"Resource id '{descriptor.Id}' contains a value that is too large.",
                        nameof(grouped));
                }

                totalValues = checked(totalValues + 1);
                if (totalValues > ResourcePackFormat.MaxTotalValues) {
                    throw new ArgumentException(
                        "The resource pack contains too many total values.",
                        nameof(grouped));
                }

                if (blob.Length > ResourcePackFormat.MaxTotalBlobBytes - totalBlobBytes) {
                    throw new ArgumentException(
                        "The resource pack exceeds the total encoded data budget.",
                        nameof(grouped));
                }

                totalBlobBytes += blob.Length;
                var decodedBytes = EstimateDecodedBytes(value.Value!);
                if (decodedBytes >
                    ResourcePackFormat.MaxTotalDecodedBytes - totalDecodedBytes) {
                    throw new ArgumentException(
                        "The resource pack exceeds the total decoded data budget.",
                        nameof(grouped));
                }

                totalDecodedBytes += decodedBytes;
            }
        }
    }

    private static void WriteResource(
        BinaryWriter writer,
        ResourceDescriptor descriptor,
        IReadOnlyList<ResourcePackValue> values) {
        if (values.Count > ResourcePackFormat.MaxValuesPerResource) {
            throw new ArgumentException(
                $"Resource id '{descriptor.Id}' declares too many variants.",
                nameof(values));
        }

        WriteText(writer, descriptor.Id);
        writer.Write((byte)descriptor.Kind);
        writer.Write(checked((uint)values.Count));
        var seenVariants = new HashSet<ResourceVariant>();
        foreach (var value in values) {
            if (!seenVariants.Add(value.Variant)) {
                throw new ArgumentException(
                    $"Resource id '{descriptor.Id}' contains a duplicate variant.",
                    nameof(values));
            }

            WriteVariant(writer, descriptor, value.Variant);
            var blob = ResourcePackCodec.Encode(descriptor, value.Value);
            if (blob.Length > ResourcePackFormat.MaxBlobBytes) {
                throw new ArgumentException(
                    $"Resource id '{descriptor.Id}' contains a value that is too large.",
                    nameof(values));
            }

            writer.Write(checked((uint)blob.Length));
            writer.Write(blob);
        }
    }

    private static void WriteVariant(
        BinaryWriter writer,
        ResourceDescriptor descriptor,
        ResourceVariant variant) {
        var flags = GetVariantFlags(descriptor, variant);
        ValidateVariantText(variant, flags);
        writer.Write((byte)flags);
        if ((flags & ResourceVariantKinds.Culture) != 0) {
            WriteText(writer, variant.Culture!);
        }

        if ((flags & ResourceVariantKinds.Theme) != 0) {
            WriteText(writer, variant.Theme!);
        }

        if ((flags & ResourceVariantKinds.Style) != 0) {
            WriteText(writer, variant.Style!);
        }

        if ((flags & ResourceVariantKinds.DisplayScale) != 0) {
            writer.Write(variant.DisplayScale);
        }
    }

    private static void ValidateVariant(
        ResourceDescriptor descriptor,
        ResourceVariant variant) {
        var flags = GetVariantFlags(descriptor, variant);
        ValidateVariantText(variant, flags);
        if (!float.IsFinite(variant.DisplayScale) || variant.DisplayScale <= 0) {
            throw new ArgumentException(
                $"Resource id '{descriptor.Id}' contains an invalid display scale.",
                nameof(variant));
        }
    }

    private static ResourceVariantKinds GetVariantFlags(
        ResourceDescriptor descriptor,
        ResourceVariant variant) {
        var flags = ResourceVariantKinds.None;
        if (!string.IsNullOrWhiteSpace(variant.Culture)) {
            flags |= ResourceVariantKinds.Culture;
        }

        if (variant.Theme is not null) {
            flags |= ResourceVariantKinds.Theme;
        }

        if (variant.Style is not null) {
            flags |= ResourceVariantKinds.Style;
        }

        if (variant.DisplayScale != 1.0f) {
            flags |= ResourceVariantKinds.DisplayScale;
        }

        if ((flags & ~descriptor.Variants) != 0) {
            throw new ArgumentException(
                $"Resource id '{descriptor.Id}' contains variant data that is " +
                "not declared by the contract.",
                nameof(variant));
        }

        return flags;
    }

    private static void ValidateVariantText(
        ResourceVariant variant,
        ResourceVariantKinds flags) {
        if ((flags & ResourceVariantKinds.Culture) != 0) {
            _ = GetTextBytes(variant.Culture!);
        }

        if ((flags & ResourceVariantKinds.Theme) != 0) {
            _ = GetTextBytes(variant.Theme!);
        }

        if ((flags & ResourceVariantKinds.Style) != 0) {
            _ = GetTextBytes(variant.Style!);
        }
    }

    private static void WriteText(BinaryWriter writer, string value) {
        var bytes = GetTextBytes(value);
        writer.Write(checked((uint)bytes.Length));
        writer.Write(bytes);
    }

    private static byte[] GetTextBytes(string value) {
        byte[] bytes;
        try {
            bytes = StrictUtf8.GetBytes(value);
        } catch (EncoderFallbackException exception) {
            throw new ArgumentException(
                "A resource pack text value contains invalid UTF-16.",
                nameof(value),
                exception);
        }

        if (bytes.Length > ResourcePackFormat.MaxTextBytes) {
            throw new ArgumentException(
                "A resource pack text value is too long.",
                nameof(value));
        }

        return bytes;
    }

    private static long EstimateDecodedBytes(object value) {
        return value switch {
            string text => StrictUtf8.GetByteCount(text),
            ImageResource image => image.Data.Length,
            FontFace font => font.Data.Length,
            byte[] data => data.Length,
            _ => 0,
        };
    }
}
