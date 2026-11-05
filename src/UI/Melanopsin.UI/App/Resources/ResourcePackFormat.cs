using System.Text;

namespace Melanopsin.UI.App;

internal static class ResourcePackFormat {
    public const ushort Version = 1;
    public const int MaxResources = 65_536;
    public const int MaxValuesPerResource = 65_536;
    public const int MaxTextBytes = 1_048_576;
    public const long MaxTotalTextBytes = 32L * 1024 * 1024;
    public const int MaxBlobBytes = 256 * 1024 * 1024;
    public const int MaxTotalValues = 1_048_576;
    public const long MaxTotalBlobBytes = 512L * 1024 * 1024;
    public const long MaxTotalDecodedBytes = 512L * 1024 * 1024;

    public static readonly byte[] Magic = Encoding.ASCII.GetBytes("MRSP");
}
