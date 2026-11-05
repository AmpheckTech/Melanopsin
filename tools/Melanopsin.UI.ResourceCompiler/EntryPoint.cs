// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.ResourceCompiler;

internal static class EntryPoint {
    public static int Main(string[] args) {
        if (args.Length == 0 ||
            args[0] is "-h" or "--help" or "/?") {
            WriteUsage();
            return args.Length == 0 ? 1 : 0;
        }

        string? manifest = null;
        string? values = null;
        string? output = null;
        string? outputDirectory = null;
        for (var index = 0; index < args.Length; index++) {
            var argument = args[index];
            if (index + 1 >= args.Length) {
                Console.Error.WriteLine($"Missing value for '{argument}'.");
                return 1;
            }

            var value = args[++index];
            switch (argument) {
                case "--manifest":
                    manifest = value;
                    break;
                case "--values":
                    values = value;
                    break;
                case "--output":
                    output = value;
                    break;
                case "--output-dir":
                    outputDirectory = value;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option '{argument}'.");
                    return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(manifest) ||
            (string.IsNullOrWhiteSpace(output) &&
             string.IsNullOrWhiteSpace(outputDirectory)) ||
            (!string.IsNullOrWhiteSpace(output) &&
             !string.IsNullOrWhiteSpace(outputDirectory))) {
            WriteUsage();
            return 1;
        }

        try {
            var result = string.IsNullOrWhiteSpace(outputDirectory)
                ? ResourcePackCompiler.Compile(manifest, values, output!)
                : ResourcePackCompiler.CompileToDirectory(
                    manifest,
                    values,
                    outputDirectory!);
            Console.WriteLine(
                $"Compiled {result.ValueCount} values for '{result.Package}' " +
                $"into '{result.OutputPath}'.");
            return 0;
        } catch (BuildException exception) {
            Console.Error.WriteLine(exception.Message);
            return 1;
        } catch (Exception exception) {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void WriteUsage() {
        Console.WriteLine(
            "Usage: Melanopsin.UI.ResourceCompiler " +
            "--manifest <manifest.mres> " +
            "[--values <values.mresp.xml>] " +
            "(--output <package.mresp> | --output-dir <directory>)");
    }
}
