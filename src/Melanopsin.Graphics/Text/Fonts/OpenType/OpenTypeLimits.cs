namespace Melanopsin.Graphics.Text.Fonts.OpenType;

internal static class OpenTypeLimits {
    // Large CJK collections are routinely tens of megabytes. This ceiling
    // still leaves room for those files while preventing an untrusted path or
    // resource from turning into an unbounded allocation.
    public const int MaximumFontBytes = 256 * 1024 * 1024;
    public const int MaximumCollectionFaces = 4096;
    public const int MaximumTablesPerFace = 4096;
    public const int MaximumCmapRecords = 4096;
    public const int MaximumNameRecords = 4096;
    public const int MaximumNameTableBytes = 4 * 1024 * 1024;
}
