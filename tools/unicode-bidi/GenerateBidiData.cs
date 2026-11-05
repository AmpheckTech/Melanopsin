// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

// Generates the packed UAX #9 property tables embedded by Melanopsin.Graphics.
//
//   dotnet run tools/unicode-bidi/GenerateBidiData.cs
//
// Unicode Character Database files are downloaded from unicode.org and cached
// in the system temporary directory. Pass --ucd <directory> to use a local copy.

using System.Buffers.Binary;
using System.Net.Http;
using System.Text;

const int LastCodePoint = 0x10FFFF;
const string UcdVersion = "17.0.0";
const string UcdBaseUrl = "https://www.unicode.org/Public/17.0.0/ucd/";
const int SourceVersion = 170000;

string[] classOrder = [
    "L", "R", "AL", "EN", "ES", "ET", "AN", "CS", "NSM", "BN", "B",
    "S", "WS", "ON", "LRE", "RLE", "LRO", "RLO", "PDF", "LRI", "RLI",
    "FSI", "PDI",
];

var longNames = new Dictionary<string, string>(StringComparer.Ordinal) {
    ["Left_To_Right"] = "L",
    ["Right_To_Left"] = "R",
    ["Arabic_Letter"] = "AL",
    ["European_Terminator"] = "ET",
};

var options = ParseOptions(args);
var ucdDirectory = options.UcdDirectory ?? await DownloadUcdAsync();
var shapingDirectory = Path.Combine(
    RepositoryRoot(),
    "src",
    "Melanopsin.Graphics",
    "Text",
    "Shaping");
var outputPath = options.OutputPath ?? Path.Combine(shapingDirectory, "BidiData.bin");
var enumPath = options.EnumPath ?? Path.Combine(shapingDirectory, "BidiClass.cs");

var classes = ReadBidiClasses(Path.Combine(ucdDirectory, "DerivedBidiClass.txt"));
var rows = Distil(classes);
var brackets = ReadBrackets(Path.Combine(ucdDirectory, "BidiBrackets.txt"));
Validate(rows, brackets);

var table = Pack(rows, brackets);
File.WriteAllBytes(outputPath, table);
File.WriteAllText(enumPath, EmitEnum(), new UTF8Encoding(false));

Console.WriteLine(
    $"bidi data: {rows.Length} class ranges, {brackets.Length} bracket pairs, " +
    $"{table.Length} bytes from {UcdVersion} written to {outputPath}");
return;

// --------------------------------------------------------------------------

byte[] ReadBidiClasses(string path) {
    const int DefaultClass = 0;
    var result = new byte[LastCodePoint + 1];
    var defaults = new List<(int Start, int End, byte Class)>();
    var explicitRanges = new List<(int Start, int End, byte Class)>();

    foreach (var raw in File.ReadLines(path)) {
        var line = raw.Trim();
        if (line.StartsWith("# @missing:", StringComparison.Ordinal)) {
            var fields = line[12..].Split(';', 2);
            var (start, end) = ParseRange(fields[0].Trim());
            defaults.Add((start, end, ParseClass(fields[1].Trim())));
            continue;
        }

        var comment = line.IndexOf('#');
        if (comment >= 0) {
            line = line[..comment].Trim();
        }

        if (line.Length == 0) {
            continue;
        }

        var values = line.Split(';', 2);
        var (rangeStart, rangeEnd) = ParseRange(values[0].Trim());
        explicitRanges.Add((rangeStart, rangeEnd, ParseClass(values[1].Trim())));
    }

    Array.Fill(result, (byte)DefaultClass);
    foreach (var (start, end, value) in defaults) {
        Array.Fill(result, value, start, end - start + 1);
    }

    foreach (var (start, end, value) in explicitRanges) {
        Array.Fill(result, value, start, end - start + 1);
    }

    return result;

    byte ParseClass(string name) {
        if (!longNames.TryGetValue(name, out var shortName)) {
            shortName = name;
        }

        var value = Array.IndexOf(classOrder, shortName);
        if (value < 0) {
            throw new InvalidOperationException($"Unknown bidi class '{name}'.");
        }

        return (byte)value;
    }
}

(int Start, int End, byte Class)[] Distil(byte[] classes) {
    var rows = new List<(int Start, int End, byte Class)>();
    var current = (Start: 0, End: 0, Class: classes[0]);
    for (var codePoint = 1; codePoint <= LastCodePoint; codePoint++) {
        if (classes[codePoint] == current.Class) {
            current.End = codePoint;
            continue;
        }

        rows.Add(current);
        current = (codePoint, codePoint, classes[codePoint]);
    }

    rows.Add(current);
    return [.. rows];
}

(int CodePoint, int Paired, bool IsOpen)[] ReadBrackets(string path) {
    var result = new List<(int CodePoint, int Paired, bool IsOpen)>();
    foreach (var raw in File.ReadLines(path)) {
        var comment = raw.IndexOf('#');
        var line = (comment >= 0 ? raw[..comment] : raw).Trim();
        if (line.Length == 0) {
            continue;
        }

        var fields = line.Split(';');
        if (fields.Length < 3) {
            throw new InvalidOperationException($"Malformed bracket row: {raw}");
        }

        var codePoint = Convert.ToInt32(fields[0].Trim(), 16);
        var paired = Convert.ToInt32(fields[1].Trim(), 16);
        var type = fields[2].Trim();
        if (type is not "o" and not "c") {
            throw new InvalidOperationException($"Unknown bracket type '{type}'.");
        }

        result.Add((codePoint, paired, type == "o"));
    }

    result.Sort(static (left, right) => left.CodePoint.CompareTo(right.CodePoint));
    return [.. result];
}

void Validate(
    (int Start, int End, byte Class)[] rows,
    (int CodePoint, int Paired, bool IsOpen)[] brackets) {
    if (rows.Length == 0 || rows[0].Start != 0 || rows[^1].End != LastCodePoint) {
        throw new InvalidOperationException("The bidi class table does not cover Unicode.");
    }

    for (var index = 1; index < rows.Length; index++) {
        if (rows[index].Start != rows[index - 1].End + 1) {
            throw new InvalidOperationException(
                $"The bidi class table has a gap at U+{rows[index].Start:X4}.");
        }
    }

    var bracketMap = brackets.ToDictionary(static item => item.CodePoint);
    foreach (var bracket in brackets) {
        if (!bracketMap.TryGetValue(bracket.Paired, out var paired) ||
            paired.Paired != bracket.CodePoint ||
            paired.IsOpen == bracket.IsOpen) {
            throw new InvalidOperationException(
                $"Bracket U+{bracket.CodePoint:X4} has an invalid partner.");
        }
    }

    if (classOrder.Length > 32) {
        throw new InvalidOperationException("Bidi classes no longer fit in five bits.");
    }
}

// Header: 24 bytes. Class rows are four bytes, with 21 bits of range start and
// five bits of class. Bracket records are eight bytes and pack both code points
// into 21 bits each, plus one bit for the opening/closing type.
byte[] Pack(
    (int Start, int End, byte Class)[] rows,
    (int CodePoint, int Paired, bool IsOpen)[] brackets) {
    const int HeaderSize = 24;
    const int RowSize = 4;
    const int BracketSize = 8;
    var table = new byte[
        HeaderSize + (rows.Length * RowSize) + (brackets.Length * BracketSize)];
    "MBDX"u8.CopyTo(table);
    table[4] = 1;
    BinaryPrimitives.WriteInt32LittleEndian(table.AsSpan(8), rows.Length);
    BinaryPrimitives.WriteInt32LittleEndian(table.AsSpan(12), SourceVersion);
    BinaryPrimitives.WriteInt32LittleEndian(table.AsSpan(16), brackets.Length);

    for (var index = 0; index < rows.Length; index++) {
        var packed = (uint)rows[index].Start | ((uint)rows[index].Class << 21);
        BinaryPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(HeaderSize + (index * RowSize)),
            packed);
    }

    var bracketOffset = HeaderSize + (rows.Length * RowSize);
    for (var index = 0; index < brackets.Length; index++) {
        var bracket = brackets[index];
        var packed = (uint)bracket.CodePoint |
            ((ulong)(uint)bracket.Paired << 21) |
            ((bracket.IsOpen ? 1UL : 0UL) << 42);
        BinaryPrimitives.WriteUInt64LittleEndian(
            table.AsSpan(bracketOffset + (index * BracketSize)),
            packed);
    }

    return table;
}

string EmitEnum() {
    var builder = new StringBuilder();
    builder.AppendLine("// <auto-generated />");
    builder.AppendLine("// Generated by tools/unicode-bidi/GenerateBidiData.cs from the");
    builder.Append("// Unicode Character Database ").Append(UcdVersion).AppendLine(". Do not edit by hand.");
    builder.AppendLine("namespace Melanopsin.Graphics.Text;");
    builder.AppendLine();
    builder.AppendLine("internal enum BidiClass : byte {");
    foreach (var name in classOrder) {
        builder.Append("    ").Append(name).AppendLine(",");
    }

    builder.AppendLine("}");
    return builder.ToString();
}

(int Start, int End) ParseRange(string value) {
    var separator = value.IndexOf("..", StringComparison.Ordinal);
    if (separator < 0) {
        var codePoint = Convert.ToInt32(value, 16);
        return (codePoint, codePoint);
    }

    return (
        Convert.ToInt32(value[..separator], 16),
        Convert.ToInt32(value[(separator + 2)..], 16));
}

async Task<string> DownloadUcdAsync() {
    var cache = Path.Combine(Path.GetTempPath(), $"melanopsin-ucd-{UcdVersion}");
    Directory.CreateDirectory(cache);
    using var client = new HttpClient();
    foreach (var name in new[] { "extracted/DerivedBidiClass.txt", "BidiBrackets.txt" }) {
        var destination = Path.Combine(cache, Path.GetFileName(name));
        if (File.Exists(destination)) {
            continue;
        }

        Console.WriteLine($"downloading {name}");
        var text = await client.GetStringAsync(new Uri(UcdBaseUrl + name));
        await File.WriteAllTextAsync(destination, text);
    }

    return cache;
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

string RepositoryRoot() {
    var directory = new DirectoryInfo(Environment.CurrentDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Melanopsin.slnx"))) {
        directory = directory.Parent;
    }

    return directory?.FullName ??
        throw new InvalidOperationException("Could not find the repository root.");
}

sealed class Options {
    public string? UcdDirectory { get; set; }

    public string? OutputPath { get; set; }

    public string? EnumPath { get; set; }
}
