// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Reactive;

namespace Melanopsin.UI.App;

/// <summary>
/// Resolves a resource contract through a base provider and named overlays.
/// Overlays may only replace keys the base declares; replacing one by name keeps
/// its position. Resolution participates in the reactive dependency graph.
/// </summary>
public sealed class ResourceResolver : ResourceProvider {
    private readonly object _gate = new();
    private ResourceProvider? _baseProvider;
    private readonly Dictionary<string, ResourceProvider> _overlays =
        new(StringComparer.Ordinal);
    private readonly ResourceContract _contract;
    private readonly List<string> _overlayOrder = [];
    private readonly ResourceInvalidationSource _invalidations = new();
    private ResourceVariant _variant = ResourceVariant.Default;

    public ResourceResolver(ResourceProvider? baseProvider = null)
        : this(
            new ResourceContract(baseProvider?.Descriptors ??
                EmptyResourceProvider.Instance.Descriptors),
            baseProvider) {
    }

    public ResourceResolver(
        ResourceContract contract,
        ResourceProvider? baseProvider = null) {
        ArgumentNullException.ThrowIfNull(contract);
        _contract = contract;
        _baseProvider = baseProvider;
    }

    public ResourceVariant Variant {
        get {
            lock (_gate) {
                return _variant;
            }
        }
        set {
            ArgumentNullException.ThrowIfNull(value);
            lock (_gate) {
                if (_variant.Equals(value)) {
                    return;
                }

                _variant = value;
            }

            NotifyChanged();
        }
    }

    public override IReadOnlyCollection<ResourceDescriptor> Descriptors =>
        _contract.Descriptors;

    public ResourceContract Contract => _contract;

    public override bool TryGet<T>(
        ResourceKey<T> key,
        ResourceVariant variant,
        out T value) {
        ArgumentNullException.ThrowIfNull(variant);
        ResourceProvider? baseProvider;
        ResourceProvider[] overlays;
        lock (_gate) {
            baseProvider = _baseProvider;
            overlays = new ResourceProvider[_overlayOrder.Count];
            for (var index = 0; index < _overlayOrder.Count; index++) {
                overlays[index] = _overlays[_overlayOrder[index]];
            }
        }

        for (var index = overlays.Length - 1; index >= 0; index--) {
            if (overlays[index].TryGet(key, variant, out var overlayValue)) {
                value = overlayValue;
                return true;
            }
        }

        if (baseProvider is not null &&
            baseProvider.TryGet(key, variant, out var baseValue)) {
            value = baseValue;
            return true;
        }

        value = default!;
        return false;
    }

    public T Get<T>(ResourceKey<T> key) {
        ReactiveRuntime.Track(_invalidations);
        if (string.IsNullOrWhiteSpace(key.Id)) {
            throw new ArgumentException("A resource key must be initialized.", nameof(key));
        }
        if (TryGet(key, Variant, out var value)) {
            return value;
        }

        throw new KeyNotFoundException(
            $"Resource '{key.Id}' was not found as '{typeof(T).FullName}'.");
    }

    public void SetOverlay(string name, ResourceProvider overlay) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(overlay);
        ValidateOverlay(overlay);

        lock (_gate) {
            SetOverlayCore(name, overlay);
        }

        NotifyChanged();
    }

    /// <summary>
    /// Opens a pack. The first becomes the base provider; later packs are overlays
    /// in open order, and an explicit name replaces an overlay in place.
    /// </summary>
    public ResourcePack Open(
        ResourceStorage storage,
        string? overlayName = null) {
        ArgumentNullException.ThrowIfNull(storage);
        if (overlayName is not null) {
            ArgumentException.ThrowIfNullOrWhiteSpace(overlayName);
        }

        var pack = ResourcePack.Open(storage, _contract);
        lock (_gate) {
            if (_baseProvider is null) {
                _baseProvider = pack;
            } else {
                SetOverlayCore(overlayName ?? storage.DisplayName, pack);
            }
        }

        NotifyChanged();
        return pack;
    }

    public bool RemoveOverlay(string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var removed = false;
        lock (_gate) {
            removed = _overlays.Remove(name);
            if (removed) {
                _overlayOrder.Remove(name);
            }
        }

        if (removed) {
            NotifyChanged();
        }

        return removed;
    }

    public bool TryGetDescriptor(string id, out ResourceDescriptor? descriptor) {
        return _contract.TryGetDescriptor(id, out descriptor);
    }

    private void ValidateOverlay(ResourceProvider overlay) {
        _contract.ValidateProvider(overlay, nameof(overlay));
    }

    private void SetOverlayCore(string name, ResourceProvider overlay) {
        if (_overlays.TryAdd(name, overlay)) {
            _overlayOrder.Add(name);
        } else {
            _overlays[name] = overlay;
        }
    }

    private void NotifyChanged() {
        _invalidations.Invalidate();
    }
}
