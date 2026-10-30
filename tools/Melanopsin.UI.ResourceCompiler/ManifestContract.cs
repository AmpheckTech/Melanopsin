using Melanopsin.UI.App;

namespace Melanopsin.UI.ResourceCompiler;

internal sealed class ManifestContract {
    private readonly IReadOnlyDictionary<string, ResourceDescriptor> _byId;

    public ManifestContract(
        string path,
        string package,
        IReadOnlyList<ResourceDescriptor> descriptors) {
        Path = path;
        Package = package;
        Descriptors = descriptors;
        _byId = descriptors.ToDictionary(
            static descriptor => descriptor.Id,
            StringComparer.Ordinal);
        Contract = new ResourceContract(descriptors, package, assembly: null);
    }

    public string Path { get; }

    public string Package { get; }

    public IReadOnlyList<ResourceDescriptor> Descriptors { get; }

    public ResourceContract Contract { get; }

    public bool TryGetDescriptor(
        string id,
        out ResourceDescriptor? descriptor) {
        return _byId.TryGetValue(id, out descriptor);
    }
}
