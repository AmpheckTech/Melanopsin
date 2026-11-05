// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Collections.ObjectModel;
using System.Reflection;

namespace Melanopsin.UI.App;

/// <summary>
/// The immutable set of resource identities and value contracts an application
/// may resolve. Packs and overlays can supply values, but cannot extend it.
/// </summary>
public sealed class ResourceContract {
    private readonly IReadOnlyList<ResourceDescriptor> _descriptors;
    private readonly IReadOnlyDictionary<string, ResourceDescriptor> _byId;

    public ResourceContract(IEnumerable<ResourceDescriptor> descriptors)
        : this(descriptors, package: null, assembly: null) {
    }

    public ResourceContract(
        IEnumerable<ResourceDescriptor> descriptors,
        string? package,
        Assembly? assembly) {
        ArgumentNullException.ThrowIfNull(descriptors);
        if (package is not null && string.IsNullOrWhiteSpace(package)) {
            throw new ArgumentException(
                "A resource package name cannot be empty.",
                nameof(package));
        }

        var ordered = new List<ResourceDescriptor>();
        var byId = new Dictionary<string, ResourceDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors) {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (!byId.TryAdd(descriptor.Id, descriptor)) {
                throw new ArgumentException(
                    $"The resource contract contains duplicate id '{descriptor.Id}'.",
                    nameof(descriptors));
            }

            ordered.Add(descriptor);
        }

        _descriptors = ordered.AsReadOnly();
        _byId = new ReadOnlyDictionary<string, ResourceDescriptor>(byId);
        Package = package;
        Assembly = assembly;
    }

    public IReadOnlyList<ResourceDescriptor> Descriptors => _descriptors;

    public string? Package { get; }

    public Assembly? Assembly { get; }

    public bool TryGetDescriptor(
        string id,
        out ResourceDescriptor? descriptor) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out descriptor);
    }

    /// <summary>
    /// Creates a resolver rooted in this contract and opens the requested pack
    /// as its base provider. Additional packs can be layered by opening them
    /// through the returned resolver.
    /// </summary>
    public ResourceResolver Open(ResourceStorage storage) {
        ArgumentNullException.ThrowIfNull(storage);
        var resolver = new ResourceResolver(this);
        resolver.Open(storage);
        return resolver;
    }

    internal void ValidateProvider(
        ResourceProvider provider,
        string parameterName) {
        ArgumentNullException.ThrowIfNull(provider);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var descriptor in provider.Descriptors) {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (!seen.Add(descriptor.Id)) {
                throw new ArgumentException(
                    $"The provider contains duplicate id '{descriptor.Id}'.",
                    parameterName);
            }

            if (!_byId.TryGetValue(descriptor.Id, out var contract)) {
                throw new ArgumentException(
                    $"Resource id '{descriptor.Id}' is not declared by the application contract.",
                    parameterName);
            }

            if (contract.Kind != descriptor.Kind ||
                contract.ValueType != descriptor.ValueType ||
                contract.Variants != descriptor.Variants) {
                throw new ArgumentException(
                    $"Resource id '{descriptor.Id}' does not match the application contract.",
                    parameterName);
            }
        }
    }
}
