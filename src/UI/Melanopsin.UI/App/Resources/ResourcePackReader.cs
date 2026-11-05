using System.Text;
using Melanopsin.Graphics;
using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.UI.App;

internal static class ResourcePackReader {
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static IReadOnlyList<ResourcePackValue> Read(
        Stream input,
        ResourceContract contract) {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(contract);
        using var reader = new BinaryReader(
            input,
            Encoding.UTF8,
            leaveOpen: true);

        var magic = reader.ReadBytes(ResourcePackFormat.Magic.Length);
        if (!magic.AsSpan().SequenceEqual(ResourcePackFormat.Magic)) {
            throw new InvalidDataException(
                "The stream is not a Melanopsin resource pack.");
        }

        var version = reader.ReadUInt16();
        if (version != ResourcePackFormat.Version) {
            throw new InvalidDataException(
                $"Resource pack version '{version}' is not supported.");
        }

        var flags = reader.ReadUInt16();
        if (flags != 0) {
            throw new InvalidDataException(
                $"Resource pack flags '0x{flags:X4}' are not supported.");
        }

        var resourceCount = reader.ReadUInt32();
        if (resourceCount > ResourcePackFormat.MaxResources) {
            throw new InvalidDataException(
                "The resource pack declares too many resources.");
        }

        var values = new List<ResourcePackValue>(checked((int)resourceCount));
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var totalValues = 0;
        long totalBlobBytes = 0;
        long totalDecodedBytes = 0;
        long totalTextBytes = 0;
        for (var resourceIndex = 0;
             resourceIndex < resourceCount;
             resourceIndex++) {
            ReadResource(
                reader,
                contract,
                seenIds,
                values,
                ref totalValues,
                ref totalBlobBytes,
                ref totalDecodedBytes,
                ref totalTextBytes);
        }

        if (reader.BaseStream.ReadByte() != -1) {
            throw new InvalidDataException(
                "The resource pack contains trailing data.");
        }

        return values;
    }

    private static void ReadResource(
        BinaryReader reader,
        ResourceContract contract,
        HashSet<string> seenIds,
        List<ResourcePackValue> values,
        ref int totalValues,
        ref long totalBlobBytes,
        ref long totalDecodedBytes,
        ref long totalTextBytes) {
        var id = ReadText(reader, "resource id", ref totalTextBytes);
        if (string.IsNullOrWhiteSpace(id)) {
            throw new InvalidDataException(
                "The resource pack contains an empty resource id.");
        }

        if (!seenIds.Add(id)) {
            throw new InvalidDataException(
                $"Resource id '{id}' appears more than once in the pack.");
        }

        if (!contract.TryGetDescriptor(id, out var descriptor) ||
            descriptor is null) {
            throw new InvalidDataException(
                $"Resource id '{id}' is not declared by the application contract.");
        }

        var kind = (ResourceKind)reader.ReadByte();
        if (kind != descriptor.Kind) {
            throw new InvalidDataException(
                $"Resource id '{id}' has kind '{kind}' in the pack " +
                $"but kind '{descriptor.Kind}' in the contract.");
        }

        var valueCount = reader.ReadUInt32();
        if (valueCount > ResourcePackFormat.MaxValuesPerResource) {
            throw new InvalidDataException(
                $"Resource id '{id}' declares too many variants.");
        }

        var seenVariants = new HashSet<ResourceVariant>();
        for (var valueIndex = 0; valueIndex < valueCount; valueIndex++) {
            var variant = ReadVariant(
                reader,
                descriptor,
                ref totalTextBytes);
            if (!seenVariants.Add(variant)) {
                throw new InvalidDataException(
                    $"Resource id '{id}' contains a duplicate variant.");
            }

            var blobLength = reader.ReadUInt32();
            if (blobLength > ResourcePackFormat.MaxBlobBytes) {
                throw new InvalidDataException(
                    $"Resource id '{id}' contains a value that is too large.");
            }

            if (checked(totalBlobBytes + blobLength) >
                ResourcePackFormat.MaxTotalBlobBytes) {
                throw new InvalidDataException(
                    "The resource pack exceeds the total encoded data budget.");
            }

            var decodedUpperBound =
                ResourcePackCodec.EstimateDecodedBytesUpperBound(
                    descriptor.Kind,
                    blobLength);
            if (decodedUpperBound >
                ResourcePackFormat.MaxTotalDecodedBytes - totalDecodedBytes) {
                throw new InvalidDataException(
                    "The resource pack exceeds the total decoded data budget.");
            }

            if (checked(totalValues + 1) >
                ResourcePackFormat.MaxTotalValues) {
                throw new InvalidDataException(
                    "The resource pack contains too many total values.");
            }

            var blob = reader.ReadBytes(checked((int)blobLength));
            if (blob.Length != blobLength) {
                throw new EndOfStreamException(
                    $"Resource id '{id}' ended before its value was complete.");
            }

            var value = ResourcePackCodec.Decode(descriptor, blob);
            if (!descriptor.ValueType.IsInstanceOfType(value)) {
                throw new InvalidDataException(
                    $"Resource id '{id}' contains a value that does not match " +
                    $"'{descriptor.ValueType.FullName}'.");
            }

            totalBlobBytes += blobLength;
            var decodedBytes = ResourcePackCodec.EstimateDecodedBytes(value);
            if (checked(totalDecodedBytes + decodedBytes) >
                ResourcePackFormat.MaxTotalDecodedBytes) {
                throw new InvalidDataException(
                    "The resource pack exceeds the total decoded data budget.");
            }

            totalDecodedBytes += decodedBytes;
            values.Add(new ResourcePackValue(id, variant, value));
            totalValues++;
        }
    }

    private static ResourceVariant ReadVariant(
        BinaryReader reader,
        ResourceDescriptor descriptor,
        ref long totalTextBytes) {
        var flags = (ResourceVariantKinds)reader.ReadByte();
        if ((flags & ~descriptor.Variants) != 0 ||
            (flags & ResourceVariantKinds.None) != 0) {
            throw new InvalidDataException(
                $"Resource id '{descriptor.Id}' contains an undeclared variant dimension.");
        }

        var culture = (flags & ResourceVariantKinds.Culture) != 0
            ? ReadVariantText(
                reader,
                descriptor,
                "culture",
                ref totalTextBytes)
            : null;
        var theme = (flags & ResourceVariantKinds.Theme) != 0
            ? ReadVariantText(
                reader,
                descriptor,
                "theme",
                ref totalTextBytes)
            : null;
        var style = (flags & ResourceVariantKinds.Style) != 0
            ? ReadVariantText(
                reader,
                descriptor,
                "style",
                ref totalTextBytes)
            : null;
        var displayScale = (flags & ResourceVariantKinds.DisplayScale) != 0
            ? reader.ReadSingle()
            : 1.0f;
        if (!float.IsFinite(displayScale) || displayScale <= 0) {
            throw new InvalidDataException(
                $"Resource id '{descriptor.Id}' contains an invalid display scale.");
        }

        return new ResourceVariant {
            Culture = culture,
            Theme = theme,
            Style = style,
            DisplayScale = displayScale,
        };
    }

    private static string ReadText(
        BinaryReader reader,
        string fieldName,
        ref long totalTextBytes) {
        var length = reader.ReadUInt32();
        if (length > ResourcePackFormat.MaxTextBytes) {
            throw new InvalidDataException(
                $"The resource pack {fieldName} is too long.");
        }

        var memoryBytes = checked((long)length * sizeof(char));
        if (memoryBytes >
            ResourcePackFormat.MaxTotalTextBytes - totalTextBytes) {
            throw new InvalidDataException(
                "The resource pack exceeds the total text metadata budget.");
        }

        var bytes = reader.ReadBytes(checked((int)length));
        if (bytes.Length != length) {
            throw new EndOfStreamException(
                $"The resource pack ended while reading its {fieldName}.");
        }

        try {
            var text = StrictUtf8.GetString(bytes);
            totalTextBytes += memoryBytes;
            return text;
        } catch (DecoderFallbackException exception) {
            throw new InvalidDataException(
                $"The resource pack {fieldName} is not valid UTF-8.",
                exception);
        }
    }

    private static string ReadVariantText(
        BinaryReader reader,
        ResourceDescriptor descriptor,
        string fieldName,
        ref long totalTextBytes) {
        var value = ReadText(reader, fieldName, ref totalTextBytes);
        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidDataException(
                $"Resource id '{descriptor.Id}' contains an empty " +
                $"{fieldName}.");
        }

        return value;
    }
}
