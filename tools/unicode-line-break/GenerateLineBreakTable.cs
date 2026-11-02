// Generates the packed UAX #14 line break table that Melanopsin.Graphics embeds
// as Text/Shaping/LineBreakData.bin, together with the LineBreakClass enum that
// the class codes in that table index into.
//
//   dotnet run tools/unicode-line-break/GenerateLineBreakTable.cs
//
// The Unicode Character Database files are downloaded from unicode.org and
// cached in the system temporary directory. Pass --ucd <directory> to read them
// from a local copy instead.

using System.Buffers.Binary;
using System.Net.Http;
using System.Text;

const int LastCodePoint = 0x10FFFF;
const string UcdVersion = "17.0.0";
const string UcdBaseUrl = "https://www.unicode.org/Public/17.0.0/ucd/";

// The table header carries the version of the source data so that a stale
// resource is rejected instead of silently used.
const int SourceVersion = 170000;

// The position of a class in this list is its code in the binary table, so the
// list and the LineBreakClass enum written below must stay in step.
string[] classOrder = [
    "BK", "CR", "LF", "NL", "SP", "ZW", "ZWJ", "CM", "WJ", "GL",
    "AI", "AL", "HL", "ID", "EB", "EM", "AK", "AP", "AS", "VF", "VI",
    "B2", "BA", "BB", "CB", "CJ", "CL", "CP", "EX", "H2", "H3", "HH", "HY",
    "IN", "IS", "JL", "JT", "JV", "NS", "NU", "OP", "PO", "PR", "QU", "RI",
    "SA", "SG", "SY", "XX",
];

// East_Asian_Width defaults to W for unassigned code points in the CJK blocks
// rather than to the N of the @missing line.
(int Start, int End)[] wideDefaults = [
    (0x3400, 0x4DBF),
    (0x4E00, 0x9FFF),
    (0xF900, 0xFAFF),
    (0x20000, 0x2FFFD),
    (0x30000, 0x3FFFD),
];

var options = ParseOptions(args);
var ucdDirectory = options.UcdDirectory ?? await DownloadUcdAsync();
var shapingDirectory = Path.Combine(
    RepositoryRoot(),
    "src",
    "Melanopsin.Graphics",
    "Text",
    "Shaping");
var outputPath = options.OutputPath ?? Path.Combine(shapingDirectory, "LineBreakData.bin");
var enumPath = options.EnumPath ?? Path.Combine(shapingDirectory, "LineBreakClass.cs");

var lineBreak = RangeTable.Parse(Path.Combine(ucdDirectory, "LineBreak.txt"));
var categories = RangeTable.Parse(Path.Combine(ucdDirectory, "UnicodeData.txt"), unicodeData: true);
var eastAsian = RangeTable.Parse(Path.Combine(ucdDirectory, "EastAsianWidth.txt"));
var pictographic = RangeTable
    .Parse(Path.Combine(ucdDirectory, "emoji-data.txt"))
    .Only("Extended_Pictographic");

var rows = Distil(lineBreak, categories, eastAsian, pictographic);
Validate(rows);

var table = Pack(rows);
File.WriteAllBytes(outputPath, table);
File.WriteAllText(enumPath, EmitEnum(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

Console.WriteLine(
    $"line break table: {rows.Length} ranges, {table.Length} bytes " +
    $"from {UcdVersion} written to {outputPath}");
return;

// --------------------------------------------------------------------------

// Walks the whole code space and merges neighbouring code points that share a
// line break class and flags into one range.
(int Start, int End, int Class, int Flags)[] Distil(
    RangeTable lineBreak,
    RangeTable categories,
    RangeTable eastAsian,
    RangeTable pictographic) {
    var rows = new List<(int Start, int End, int Class, int Flags)>();
    var current = (Start: 0, End: 0, Class: 0, Flags: 0);
    for (var codePoint = 0; codePoint <= LastCodePoint; codePoint++) {
        var name = lineBreak.Lookup(codePoint) ?? "XX";
        var classCode = Array.IndexOf(classOrder, name);
        if (classCode < 0) {
            throw new InvalidOperationException($"Unknown line break class '{name}'.");
        }

        var flags = FlagsAt(codePoint, name);
        if (current.End + 1 == codePoint &&
            current.Class == classCode &&
            current.Flags == flags) {
            current = (current.Start, codePoint, classCode, flags);
            continue;
        }

        if (codePoint > 0) {
            rows.Add(current);
        }

        current = (codePoint, codePoint, classCode, flags);
    }

    rows.Add(current);
    return [.. rows];

    int FlagsAt(int codePoint, string lineBreakClass) {
        var flags = 0;
        var width = eastAsian.Lookup(codePoint) ??
            (wideDefaults.Any(range => codePoint >= range.Start && codePoint <= range.End)
                ? "W"
                : "N");
        if (width is "F" or "W" or "H") {
            flags |= 1;
        }

        var category = categories.Lookup(codePoint) ?? "Cn";
        if (category == "Pi") {
            flags |= 2;
        } else if (category == "Pf") {
            flags |= 4;
        }

        if (category == "Cn" && pictographic.Lookup(codePoint) is not null) {
            flags |= 8;
        }

        if (lineBreakClass == "SA" && category is "Mn" or "Mc") {
            flags |= 16;
        }

        return flags;
    }
}

// The reader derives the end of a range from the next start, so the ranges must
// partition the code space.
void Validate((int Start, int End, int Class, int Flags)[] rows) {
    if (rows.Length == 0 || rows[0].Start != 0 || rows[^1].End != LastCodePoint) {
        throw new InvalidOperationException("The table does not cover the whole code space.");
    }

    for (var index = 1; index < rows.Length; index++) {
        if (rows[index].Start != rows[index - 1].End + 1) {
            throw new InvalidOperationException(
                $"The table has a gap or overlap at U+{rows[index].Start:X4}.");
        }
    }

    if (classOrder.Length > 64) {
        throw new InvalidOperationException("The class codes no longer fit in six bits.");
    }
}

// A row is 21 bits of range start, 6 bits of class, and 5 bits of flags; the
// ranges are contiguous and sorted, so the end of a range is implied by the
// start of the next one.
byte[] Pack((int Start, int End, int Class, int Flags)[] rows) {
    const int HeaderSize = 16;
    const int RowSize = 4;
    var header = new byte[HeaderSize];
    "MLBX"u8.CopyTo(header);
    header[4] = 1;
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), rows.Length);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), SourceVersion);

    var table = new byte[HeaderSize + (rows.Length * RowSize)];
    header.CopyTo(table, 0);
    for (var index = 0; index < rows.Length; index++) {
        var (start, _, lineBreakClass, flags) = rows[index];
        var packed = (uint)start |
            ((uint)lineBreakClass << 21) |
            ((uint)flags << 27);
        BinaryPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(HeaderSize + (index * RowSize)),
            packed);
    }

    return table;
}

string EmitEnum() {
    var builder = new StringBuilder();
    builder.AppendLine("// <auto-generated />");
    builder.AppendLine("// Generated by tools/unicode-line-break/GenerateLineBreakTable.cs from the");
    builder.Append("// Unicode Character Database ").Append(UcdVersion).AppendLine(". Do not edit by hand.");
    builder.AppendLine("namespace Melanopsin.Graphics.Text;");
    builder.AppendLine();
    builder.AppendLine("/// <summary>");
    builder.AppendLine("/// The Unicode <c>Line_Break</c> property values defined by UAX #14,");
    builder.Append("/// as published with Unicode ").Append(UcdVersion).AppendLine(". The order of the");
    builder.AppendLine("/// members is the order of the class codes in the embedded line break table.");
    builder.AppendLine("/// </summary>");
    builder.AppendLine("internal enum LineBreakClass : byte {");
    foreach (var name in classOrder) {
        builder.Append("    ").Append(name).AppendLine(",");
    }

    builder.AppendLine("}");
    return builder.ToString();
}

async Task<string> DownloadUcdAsync() {
    var cache = Path.Combine(Path.GetTempPath(), $"melanopsin-ucd-{UcdVersion}");
    Directory.CreateDirectory(cache);
    using var client = new HttpClient();
    string[] files = ["LineBreak.txt", "UnicodeData.txt", "EastAsianWidth.txt", "emoji-data.txt"];
    foreach (var name in files) {
        var path = Path.Combine(cache, name);
        if (File.Exists(path)) {
            continue;
        }

        Console.WriteLine($"downloading {name}");
        var text = await client.GetStringAsync(new Uri(UcdBaseUrl + name));
        await File.WriteAllTextAsync(path, text);
    }

    return cache;
}

string RepositoryRoot() {
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null &&
        !File.Exists(Path.Combine(directory.FullName, "Melanopsin.slnx"))) {
        directory = directory.Parent;
    }

    return directory?.FullName ??
        throw new InvalidOperationException("Run this tool from inside the repository.");
}

Options ParseOptions(string[] arguments) {
    var parsed = new Options();
    for (var index = 0; index < arguments.Length; index++) {
        switch (arguments[index]) {
            case "--ucd":
                parsed.UcdDirectory = Next(arguments, ref index);
                break;
            case "--output":
                parsed.OutputPath = Next(arguments, ref index);
                break;
            case "--enum":
                parsed.EnumPath = Next(arguments, ref index);
                break;
            default:
                throw new ArgumentException($"Unknown argument '{arguments[index]}'.");
        }
    }

    return parsed;

    static string Next(string[] arguments, ref int index) {
        if (index + 1 >= arguments.Length) {
            throw new ArgumentException($"'{arguments[index]}' needs a value.");
        }

        return arguments[++index];
    }
}

/// <summary>
/// A sorted set of code point ranges with one value each, read from a Unicode
/// Character Database property file.
/// </summary>
sealed class RangeTable {
    private readonly int[] _starts;
    private readonly int[] _ends;
    private readonly string[] _values;

    private RangeTable(int[] starts, int[] ends, string[] values) {
        _starts = starts;
        _ends = ends;
        _values = values;
    }

    /// <summary>
    /// Reads a file of <c>range; value</c> lines, or the general categories of
    /// <c>UnicodeData.txt</c> when <paramref name="unicodeData"/> is set.
    /// </summary>
    public static RangeTable Parse(string path, bool unicodeData = false) {
        var ranges = new List<(int Start, int End, string Value)>();
        if (unicodeData) {
            var pendingStart = -1;
            var pendingValue = string.Empty;
            foreach (var line in File.ReadLines(path)) {
                var fields = line.Split(';');
                if (fields.Length < 3) {
                    continue;
                }

                var codePoint = Convert.ToInt32(fields[0], 16);
                if (fields[1].EndsWith(", First>", StringComparison.Ordinal)) {
                    pendingStart = codePoint;
                    pendingValue = fields[2];
                } else if (fields[1].EndsWith(", Last>", StringComparison.Ordinal)) {
                    ranges.Add((pendingStart, codePoint, pendingValue));
                } else {
                    ranges.Add((codePoint, codePoint, fields[2]));
                }
            }
        } else {
            foreach (var raw in File.ReadLines(path)) {
                var comment = raw.IndexOf('#');
                var line = (comment >= 0 ? raw[..comment] : raw).Trim();
                if (line.Length == 0) {
                    continue;
                }

                var fields = line.Split(';');
                var bounds = fields[0].Trim();
                var value = fields[1].Trim();
                var separator = bounds.IndexOf("..", StringComparison.Ordinal);
                if (separator < 0) {
                    var codePoint = Convert.ToInt32(bounds, 16);
                    ranges.Add((codePoint, codePoint, value));
                } else {
                    ranges.Add((
                        Convert.ToInt32(bounds[..separator], 16),
                        Convert.ToInt32(bounds[(separator + 2)..], 16),
                        value));
                }
            }
        }

        ranges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        var starts = new int[ranges.Count];
        var ends = new int[ranges.Count];
        var values = new string[ranges.Count];
        for (var index = 0; index < ranges.Count; index++) {
            (starts[index], ends[index], values[index]) = ranges[index];
        }

        return new RangeTable(starts, ends, values);
    }

    /// <summary>
    /// Returns the ranges that carry <paramref name="value"/>, as a table of
    /// their own.
    /// </summary>
    public RangeTable Only(string value) {
        var ranges = new List<(int Start, int End, string Value)>();
        for (var index = 0; index < _starts.Length; index++) {
            if (_values[index] == value) {
                ranges.Add((_starts[index], _ends[index], _values[index]));
            }
        }

        var starts = new int[ranges.Count];
        var ends = new int[ranges.Count];
        var values = new string[ranges.Count];
        for (var index = 0; index < ranges.Count; index++) {
            (starts[index], ends[index], values[index]) = ranges[index];
        }

        return new RangeTable(starts, ends, values);
    }

    /// <summary>
    /// Returns the value of the range that contains <paramref name="codePoint"/>,
    /// or <see langword="null"/> when no range does.
    /// </summary>
    public string? Lookup(int codePoint) {
        var index = Array.BinarySearch(_starts, codePoint);
        if (index < 0) {
            index = ~index - 1;
        }

        return index >= 0 && codePoint <= _ends[index] ? _values[index] : null;
    }
}

sealed class Options {
    public string? UcdDirectory { get; set; }

    public string? OutputPath { get; set; }

    public string? EnumPath { get; set; }
}
