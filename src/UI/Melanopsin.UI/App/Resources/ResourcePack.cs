// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Melanopsin.UI.App;

/// <summary>
/// An immutable provider backed by one versioned .mresp file. Packs only carry
/// values for identities declared by the application contract; they never
/// extend or redefine that contract.
/// </summary>
public sealed class ResourcePack : ResourceProvider {
    private readonly IReadOnlyDictionary<string, ResourceDescriptor> _descriptors;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ResourcePackValue>> _values;
    private readonly IReadOnlyList<ResourceDescriptor> _descriptorList;

    private ResourcePack(
        ResourceContract contract,
        IReadOnlyList<ResourcePackValue> values) {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(values);

        var descriptors = new Dictionary<string, ResourceDescriptor>(StringComparer.Ordinal);
        var grouped = new Dictionary<string, List<ResourcePackValue>>(StringComparer.Ordinal);
        var orderedDescriptors = new List<ResourceDescriptor>();
        foreach (var value in values) {
            ArgumentNullException.ThrowIfNull(value);
            if (!contract.TryGetDescriptor(value.Id, out var descriptor) ||
                descriptor is null) {
                throw new ArgumentException(
                    $"Resource id '{value.Id}' is not declared by the application contract.",
                    nameof(values));
            }

            if (descriptors.TryAdd(value.Id, descriptor)) {
                orderedDescriptors.Add(descriptor);
            }

            if (!grouped.TryGetValue(value.Id, out var resourceValues)) {
                resourceValues = [];
                grouped.Add(value.Id, resourceValues);
            }

            resourceValues.Add(value);
        }

        _descriptors = descriptors;
        _values = grouped.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<ResourcePackValue>)pair.Value,
            StringComparer.Ordinal);
        _descriptorList = orderedDescriptors.AsReadOnly();
    }

    public static ResourcePack Open(
        Stream input,
        ResourceContract contract) {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(contract);
        var values = ResourcePackReader.Read(input, contract);
        return new ResourcePack(contract, values);
    }

    public static ResourcePack Open(
        string path,
        ResourceContract contract) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contract);
        using var input = System.IO.File.OpenRead(path);
        return Open(input, contract);
    }

    public static ResourcePack Open(
        ResourceStorage storage,
        ResourceContract contract) {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(contract);
        if (storage.IsFile) {
            return Open(storage.Path!, contract);
        }

        var package = contract.Package;
        if (string.IsNullOrWhiteSpace(package)) {
            throw new InvalidOperationException(
                "Embedded and application-directory resource packs require a " +
                "package name on the resource contract.");
        }

        var fileName = package + ".mresp";
        if (storage.IsAppDirectory) {
            return Open(Path.Combine(AppContext.BaseDirectory, fileName), contract);
        }

        var assembly = ResolveAssembly(contract);
        var embedded = assembly.GetManifestResourceStream(fileName);
        if (embedded is not null) {
            using (embedded) {
                return Open(embedded, contract);
            }
        }

        if (storage.IsApplication) {
            var applicationPath = Path.Combine(AppContext.BaseDirectory, fileName);
            if (File.Exists(applicationPath)) {
                return Open(applicationPath, contract);
            }
        }

        throw new InvalidOperationException(
            $"Resource pack '{fileName}' was not found embedded in " +
            $"'{assembly.GetName().Name}'" +
            (storage.IsApplication
                ? $" or in '{AppContext.BaseDirectory}'."
                : "."));
    }

    public override IReadOnlyCollection<ResourceDescriptor> Descriptors =>
        _descriptorList;

    public T Get<T>(
        ResourceKey<T> key,
        ResourceVariant? variant = null) {
        if (string.IsNullOrWhiteSpace(key.Id)) {
            throw new ArgumentException("A resource key must be initialized.", nameof(key));
        }

        if (TryGet(key, variant ?? ResourceVariant.Default, out var value)) {
            return value;
        }

        throw new KeyNotFoundException(
            $"Resource '{key.Id}' was not found as '{typeof(T).FullName}'.");
    }

    public override bool TryGet<T>(
        ResourceKey<T> key,
        ResourceVariant variant,
        [MaybeNullWhen(false)] out T value) {
        ArgumentNullException.ThrowIfNull(variant);
        if (!_descriptors.TryGetValue(key.Id, out var descriptor) ||
            descriptor.ValueType != typeof(T) ||
            !_values.TryGetValue(key.Id, out var values)) {
            value = default;
            return false;
        }

        var selected = ResourceVariantMatcher.Select(values, variant, descriptor);
        if (selected?.Value is not T typedValue) {
            value = default;
            return false;
        }

        value = typedValue;
        return true;
    }

    private static Assembly ResolveAssembly(ResourceContract contract) {
        return contract.Assembly ??
            Assembly.GetEntryAssembly() ??
            typeof(ResourcePack).Assembly;
    }
}
