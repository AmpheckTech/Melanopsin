using Melanopsin.UI.App;

namespace Melanopsin.UI.ResourceCompiler;

public static class ResourcePackCompiler {
    public static ResourcePackBuildResult Compile(
        string manifestPath,
        string? valuesPath,
        string outputPath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var manifest = ManifestReader.Read(manifestPath);
        var resolvedValuesPath = ResolveValuesPath(manifest.Path, valuesPath);
        return CompileCore(
            manifest,
            resolvedValuesPath,
            Path.GetFullPath(outputPath));
    }

    public static ResourcePackBuildResult CompileToDirectory(
        string manifestPath,
        string? valuesPath,
        string outputDirectory) {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var manifest = ManifestReader.Read(manifestPath);
        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullOutputDirectory);
        var resolvedValuesPath = ResolveValuesPath(manifest.Path, valuesPath);
        return CompileCore(
            manifest,
            resolvedValuesPath,
            Path.Combine(fullOutputDirectory, manifest.Package + ".mresp"));
    }

    private static ResourcePackBuildResult CompileCore(
        ManifestContract manifest,
        string? resolvedValuesPath,
        string outputPath) {
        var values = ResourceValueReader.Read(manifest, resolvedValuesPath);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = outputPath + ".tmp";
        try {
            using (var output = File.Create(temporaryPath)) {
                ResourcePackWriter.Write(output, manifest.Contract, values);
            }

            File.Move(temporaryPath, outputPath, overwrite: true);
        } finally {
            if (File.Exists(temporaryPath)) {
                File.Delete(temporaryPath);
            }
        }

        return new ResourcePackBuildResult(
            outputPath,
            manifest.Package,
            manifest.Descriptors.Count,
            values.Count);
    }

    private static string? ResolveValuesPath(
        string manifestPath,
        string? valuesPath) {
        if (!string.IsNullOrWhiteSpace(valuesPath)) {
            return Path.GetFullPath(valuesPath);
        }

        var candidate = Path.ChangeExtension(manifestPath, ".mresp.xml");
        return File.Exists(candidate) ? candidate : null;
    }
}
