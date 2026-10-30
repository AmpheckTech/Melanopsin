namespace Melanopsin.UI.ResourceCompiler;

public sealed class ResourcePackBuildResult {
    public ResourcePackBuildResult(
        string outputPath,
        string package,
        int descriptorCount,
        int valueCount) {
        OutputPath = outputPath;
        Package = package;
        DescriptorCount = descriptorCount;
        ValueCount = valueCount;
    }

    public string OutputPath { get; }

    public string Package { get; }

    public int DescriptorCount { get; }

    public int ValueCount { get; }
}
