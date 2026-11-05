namespace Melanopsin.UI.App;

/// <summary>
/// A typed, stable identity for a resource contract. Keys are generated from a
/// resource manifest; runtime code never creates them from user-supplied paths.
/// </summary>
public readonly struct ResourceKey<T> : IEquatable<ResourceKey<T>> {
    public ResourceKey(string id) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public Type ValueType => typeof(T);

    public bool Equals(ResourceKey<T> other) {
        return string.Equals(Id, other.Id, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) {
        return obj is ResourceKey<T> other && Equals(other);
    }

    public override int GetHashCode() {
        return Id is null ? 0 : StringComparer.Ordinal.GetHashCode(Id);
    }

    public override string ToString() {
        return Id ?? string.Empty;
    }

    public static bool operator ==(ResourceKey<T> left, ResourceKey<T> right) {
        return left.Equals(right);
    }

    public static bool operator !=(ResourceKey<T> left, ResourceKey<T> right) {
        return !left.Equals(right);
    }
}
