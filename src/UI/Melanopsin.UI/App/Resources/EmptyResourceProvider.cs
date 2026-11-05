// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Melanopsin.UI.App;

/// <summary>
/// An empty contract used as the default context value before an application
/// opens its resource pack.
/// </summary>
public sealed class EmptyResourceProvider : ResourceProvider {
    public static readonly EmptyResourceProvider Instance = new();

    private EmptyResourceProvider() {
    }

    public override IReadOnlyCollection<ResourceDescriptor> Descriptors =>
        Array.Empty<ResourceDescriptor>();

    public override bool TryGet<T>(
        ResourceKey<T> key,
        ResourceVariant variant,
        [MaybeNullWhen(false)] out T value) {
        value = default;
        return false;
    }
}
