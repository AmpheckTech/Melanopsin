// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Melanopsin.UI.App;

/// <summary>
/// Supplies values for a resource contract. A provider must expose its complete
/// contract up front so overlays can be validated before they become visible.
/// </summary>
public abstract class ResourceProvider {
    public abstract IReadOnlyCollection<ResourceDescriptor> Descriptors { get; }

    public abstract bool TryGet<T>(
        ResourceKey<T> key,
        ResourceVariant variant,
        [MaybeNullWhen(false)] out T value);
}
