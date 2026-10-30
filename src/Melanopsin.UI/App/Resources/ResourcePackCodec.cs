using System.Buffers.Binary;
using System.Text;
using Melanopsin.Graphics;
using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.UI.App;

internal static class ResourcePackCodec {
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static byte[] Encode(
        ResourceDescriptor descriptor,
        object? value) {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (value is null) {
            throw new ArgumentException(
                $"Resource '{descriptor.Id}' cannot have a null value.",
                nameof(value));
        }

        return descriptor.Kind switch {
            ResourceKind.String or ResourceKind.Message =>
                EncodeString(descriptor, value),
            ResourceKind.Image => EncodeImage(descriptor, value),
            ResourceKind.Font => EncodeFont(descriptor, value),
            ResourceKind.Color => EncodeColor(descriptor, value),
            ResourceKind.Length => EncodeLength(descriptor, value),
            ResourceKind.Number => EncodeNumber(descriptor, value),
            ResourceKind.Binary => EncodeBinary(descriptor, value),
            _ => throw new NotSupportedException(
                $"Resource kind '{descriptor.Kind}' is not supported by " +
                "resource pack version 1."),
        };
    }

    public static object Decode(
        ResourceDescriptor descriptor,
        ReadOnlySpan<byte> data) {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.Kind switch {
            ResourceKind.String or ResourceKind.Message => DecodeString(descriptor, data),
            ResourceKind.Image => DecodeImage(descriptor, data),
            ResourceKind.Font => DecodeFont(descriptor, data),
            ResourceKind.Color => DecodeColor(descriptor, data),
            ResourceKind.Length => DecodeLength(descriptor, data),
            ResourceKind.Number => DecodeNumber(descriptor, data),
            ResourceKind.Binary => DecodeBinary(descriptor, data),
            _ => throw new InvalidDataException(
                $"Resource kind '{descriptor.Kind}' is not supported by " +
                "resource pack version 1."),
        };
    }

    private static byte[] EncodeString(
        ResourceDescriptor descriptor,
        object value) {
        EnsureType<string>(descriptor, value);
        return EncodeText(descriptor, (string)value);
    }

    private static byte[] EncodeImage(
        ResourceDescriptor descriptor,
        object value) {
        EnsureType<ImageResource>(descriptor, value);
        var image = (ImageResource)value;
        var mediaType = image.MediaType is null
            ? Array.Empty<byte>()
            : EncodeText(descriptor, image.MediaType);
        if (mediaType.Length > ushort.MaxValue) {
            throw new ArgumentException(
                $"Resource '{descriptor.Id}' has an image media type that is too long.",
                nameof(value));
        }

        var output = new byte[2 + mediaType.Length + image.Data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(output, (ushort)mediaType.Length);
        mediaType.CopyTo(output.AsSpan(2));
        image.Data.Span.CopyTo(output.AsSpan(2 + mediaType.Length));
        return output;
    }

    private static byte[] EncodeFont(
        ResourceDescriptor descriptor,
        object value) {
        EnsureType<FontFace>(descriptor, value);
        return ((FontFace)value).Data.ToArray();
    }

    private static byte[] EncodeColor(
        ResourceDescriptor descriptor,
        object value) {
        EnsureType<Color>(descriptor, value);
        var color = (Color)value;
        return [color.Red, color.Green, color.Blue, color.Alpha];
    }

    private static byte[] EncodeLength(
        ResourceDescriptor descriptor,
        object value) {
        EnsureType<Length>(descriptor, value);
        var length = (Length)value;
        if (!float.IsFinite(length.Value) ||
            length.Value < 0 ||
            length.Unit is < LengthUnit.Auto or > LengthUnit.Point) {
            throw new ArgumentException(
                $"Resource '{descriptor.Id}' has an invalid length.",
                nameof(value));
        }

        var output = new byte[5];
        BinaryPrimitives.WriteSingleLittleEndian(output, length.Value);
        output[4] = checked((byte)length.Unit);
        return output;
    }

    private static byte[] EncodeNumber(
        ResourceDescriptor descriptor,
        object value) {
        EnsureType<double>(descriptor, value);
        var number = (double)value;
        if (!double.IsFinite(number)) {
            throw new ArgumentException(
                $"Resource '{descriptor.Id}' has a non-finite number.",
                nameof(value));
        }

        var output = new byte[sizeof(double)];
        BinaryPrimitives.WriteDoubleLittleEndian(output, number);
        return output;
    }

    private static byte[] EncodeBinary(
        ResourceDescriptor descriptor,
        object value) {
        EnsureType<byte[]>(descriptor, value);
        return ((byte[])value).ToArray();
    }

    private static object DecodeString(
        ResourceDescriptor descriptor,
        ReadOnlySpan<byte> data) {
        EnsureType<string>(descriptor, typeof(string));
        return DecodeText(descriptor, data);
    }

    private static object DecodeImage(
        ResourceDescriptor descriptor,
        ReadOnlySpan<byte> data) {
        EnsureType<ImageResource>(descriptor, typeof(ImageResource));
        if (data.Length < 2) {
            throw InvalidBlob(descriptor);
        }

        var mediaTypeLength = BinaryPrimitives.ReadUInt16LittleEndian(data);
        if (mediaTypeLength > data.Length - 2) {
            throw InvalidBlob(descriptor);
        }

        var mediaType = mediaTypeLength == 0
            ? null
            : DecodeText(descriptor, data.Slice(2, mediaTypeLength));
        try {
            return new ImageResource(
                data[(2 + mediaTypeLength)..].ToArray(),
                mediaType);
        } catch (ArgumentException exception) {
            throw InvalidBlob(descriptor, exception);
        }
    }

    private static object DecodeFont(
        ResourceDescriptor descriptor,
        ReadOnlySpan<byte> data) {
        EnsureType<FontFace>(descriptor, typeof(FontFace));
        try {
            return FontFace.Parse(data);
        } catch (Exception exception) when (
            exception is ArgumentException or
            InvalidDataException or
            OverflowException or
            IndexOutOfRangeException or
            EndOfStreamException) {
            throw InvalidBlob(descriptor, exception);
        }
    }

    private static object DecodeColor(
        ResourceDescriptor descriptor,
        ReadOnlySpan<byte> data) {
        EnsureType<Color>(descriptor, typeof(Color));
        if (data.Length != 4) {
            throw InvalidBlob(descriptor);
        }

        return new Color(data[0], data[1], data[2], data[3]);
    }

    private static object DecodeLength(
        ResourceDescriptor descriptor,
        ReadOnlySpan<byte> data) {
        EnsureType<Length>(descriptor, typeof(Length));
        if (data.Length != 5 || data[4] > (byte)LengthUnit.Point) {
            throw InvalidBlob(descriptor);
        }

        var value = BinaryPrimitives.ReadSingleLittleEndian(data);
        if (!float.IsFinite(value) || value < 0) {
            throw InvalidBlob(descriptor);
        }

        return new Length(value, (LengthUnit)data[4]);
    }

    private static object DecodeNumber(
        ResourceDescriptor descriptor,
        ReadOnlySpan<byte> data) {
        EnsureType<double>(descriptor, typeof(double));
        if (data.Length != sizeof(double)) {
            throw InvalidBlob(descriptor);
        }

        var value = BinaryPrimitives.ReadDoubleLittleEndian(data);
        return double.IsFinite(value)
            ? value
            : throw InvalidBlob(descriptor);
    }

    private static object DecodeBinary(
        ResourceDescriptor descriptor,
        ReadOnlySpan<byte> data) {
        EnsureType<byte[]>(descriptor, typeof(byte[]));
        return data.ToArray();
    }

    private static void EnsureType<T>(
        ResourceDescriptor descriptor,
        object value) {
        if (value is not T) {
            throw new ArgumentException(
                $"Resource '{descriptor.Id}' requires a value of type " +
                $"'{typeof(T).FullName}'.",
                nameof(value));
        }
    }

    private static void EnsureType<T>(
        ResourceDescriptor descriptor,
        Type actualType) {
        if (actualType != typeof(T)) {
            throw new ArgumentException(
                $"Resource '{descriptor.Id}' requires a value of type " +
                $"'{typeof(T).FullName}'.",
                nameof(actualType));
        }
    }

    private static byte[] EncodeText(
        ResourceDescriptor descriptor,
        string value) {
        try {
            return StrictUtf8.GetBytes(value);
        } catch (EncoderFallbackException exception) {
            throw new ArgumentException(
                $"Resource '{descriptor.Id}' contains invalid UTF-16 text.",
                nameof(value),
                exception);
        }
    }

    private static string DecodeText(
        ResourceDescriptor descriptor,
        ReadOnlySpan<byte> data) {
        try {
            return StrictUtf8.GetString(data);
        } catch (DecoderFallbackException exception) {
            throw InvalidBlob(descriptor, exception);
        }
    }

    private static InvalidDataException InvalidBlob(
        ResourceDescriptor descriptor,
        Exception? innerException = null) {
        return new InvalidDataException(
            $"Resource '{descriptor.Id}' contains an invalid {descriptor.Kind} value.",
            innerException);
    }
}
