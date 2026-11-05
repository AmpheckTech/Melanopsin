// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.App;

/// <summary>
/// One value in a resource pack. The value type must match the contract entry
/// identified by <see cref="Id"/>.
/// </summary>
public sealed class ResourcePackValue {
    public ResourcePackValue(
        string id,
        ResourceVariant variant,
        object? value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(variant);
        Id = id;
        Variant = variant;
        Value = value;
    }

    public string Id { get; }

    public ResourceVariant Variant { get; }

    public object? Value { get; }
}
