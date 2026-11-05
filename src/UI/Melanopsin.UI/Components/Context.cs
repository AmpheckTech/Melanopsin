// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Melanopsin.Reactive;

namespace Melanopsin.UI;

/// <summary>
/// A strongly typed value that can be provided to a UI subtree. Lookup starts at
/// the consumer and returns the nearest provider.
/// </summary>
public sealed class Context<T> {
    public T GetValue(Node consumer) {
        ArgumentNullException.ThrowIfNull(consumer);
        if (TryGetValue(consumer, out var value)) {
            return value;
        }

        throw new InvalidOperationException(
            $"No provider was found for context '{typeof(T).FullName}'.");
    }

    public T GetValue(Component consumer) {
        ArgumentNullException.ThrowIfNull(consumer);
        if (TryGetValue(consumer, out var value)) {
            return value;
        }

        throw new InvalidOperationException(
            $"No provider was found for context '{typeof(T).FullName}'.");
    }

    public bool TryGetValue(
        Node consumer,
        [MaybeNullWhen(false)] out T value) {
        ArgumentNullException.ThrowIfNull(consumer);
        for (var node = consumer; node is not null; node = node.Parent) {
            if (TryRead(node, out value)) {
                return true;
            }

            if (node is ComponentHost host && TryRead(host.Component, out value)) {
                return true;
            }
        }

        value = default;
        return false;
    }

    public bool TryGetValue(
        Component consumer,
        [MaybeNullWhen(false)] out T value) {
        ArgumentNullException.ThrowIfNull(consumer);
        if (TryRead(consumer, out value)) {
            return true;
        }

        return consumer.Host is { } host &&
               TryGetValue(host, out value);
    }

    /// <summary>
    /// Resolves a context from the component currently executing BuildCore().
    /// </summary>
    public T GetValue() {
        var component = ComponentBuildScope.Current ??
            throw new InvalidOperationException(
                "Context.GetValue() can only be used while a component is building.");
        if (TryGetValue(component, out var value)) {
            return value;
        }

        throw new InvalidOperationException(
            $"No provider was found for context '{typeof(T).FullName}'.");
    }

    public ContextScope<T> Provide(Node provider, T value) {
        ArgumentNullException.ThrowIfNull(provider);
        return ProvideCore(provider, value);
    }

    public ContextScope<T> Provide(Component provider, T value) {
        ArgumentNullException.ThrowIfNull(provider);
        return ProvideCore(provider, value);
    }

    private bool TryRead(
        IContextStore store,
        [MaybeNullWhen(false)] out T value) {
        if (store.ContextValues is { } values &&
            values.TryGetValue(this, out var candidate) &&
            candidate is ContextValue<T> binding) {
            value = binding.GetValue();
            return true;
        }

        value = default;
        return false;
    }

    private ContextScope<T> ProvideCore(
        IContextStore store,
        T value) {
        store.ContextValues ??= new Dictionary<object, object>(
            ReferenceEqualityComparer.Instance);
        var hadPrevious = store.ContextValues.TryGetValue(this, out var previous);
        ContextValue<T> binding;
        if (previous is ContextValue<T> existing) {
            binding = existing;
        } else {
            binding = new ContextValue<T>(value);
            store.ContextValues[this] = binding;
        }

        var previousValue = hadPrevious && previous is ContextValue<T> previousBinding
            ? previousBinding.Peek()
            : default;
        binding.SetValue(value);
        return new ContextScope<T>(
            store,
            this,
            binding,
            hadPrevious,
            previousValue);
    }
}
