// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using MsBuildTask = Microsoft.Build.Utilities.Task;

namespace Melanopsin.UI.ResourceCompiler;

public sealed class GenerateResourcePackTask : MsBuildTask {
    [Required]
    public string Manifest { get; set; } = string.Empty;

    public string? Values { get; set; }

    public string ProjectDirectory { get; set; } = string.Empty;

    public string? Output { get; set; }

    public string? OutputDirectory { get; set; }

    [Output]
    public ITaskItem? GeneratedPack { get; set; }

    public override bool Execute() {
        if (string.IsNullOrWhiteSpace(Output) ==
            string.IsNullOrWhiteSpace(OutputDirectory)) {
            Log.LogError(
                "Specify exactly one of Output or OutputDirectory.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(Manifest)) {
            Log.LogError("Manifest is required.");
            return false;
        }

        try {
            var values = ResolveValuesPath();
            var result = string.IsNullOrWhiteSpace(OutputDirectory)
                ? ResourcePackCompiler.Compile(Manifest, values, Output!)
                : ResourcePackCompiler.CompileToDirectory(
                    Manifest,
                    values,
                    OutputDirectory!);
            var pack = new TaskItem(result.OutputPath);
            pack.SetMetadata("Package", result.Package);
            pack.SetMetadata("LogicalName", result.Package + ".mresp");
            pack.SetMetadata("TargetPath", result.Package + ".mresp");
            GeneratedPack = pack;
            return true;
        } catch (BuildException exception) {
            Log.LogError(exception.Message);
            return false;
        } catch (Exception exception) {
            Log.LogErrorFromException(exception, showStackTrace: true);
            return false;
        }
    }

    private string? ResolveValuesPath() {
        if (string.IsNullOrWhiteSpace(Values)) {
            return null;
        }

        if (Path.IsPathFullyQualified(Values)) {
            return Values;
        }

        var directory = string.IsNullOrWhiteSpace(ProjectDirectory)
            ? Environment.CurrentDirectory
            : ProjectDirectory;
        return Path.GetFullPath(Path.Combine(directory, Values));
    }
}
