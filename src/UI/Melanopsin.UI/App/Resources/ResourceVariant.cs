// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.App;

/// <summary>
/// The context used to select a resource value. The dimensions are intentionally
/// independent so culture changes do not force a node tree rebuild and theme
/// changes do not implicitly change the selected language.
/// </summary>
public sealed class ResourceVariant : IEquatable<ResourceVariant> {
    private string? _culture;
    private string? _theme;
    private string? _style;
    private float _displayScale = 1.0f;

    public static readonly ResourceVariant Default = new();

    /// <summary>
    /// Gets or sets the BCP-47 culture name used to select localized values.
    /// The name is stored as text so resource packs remain usable with
    /// invariant globalization and do not depend on an installed ICU database.
    /// </summary>
    public string? Culture {
        get => _culture;
        init {
            if (value is not null && string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentException(
                    "A resource culture must be a non-empty BCP-47 name.",
                    nameof(value));
            }

            _culture = value;
        }
    }

    public string? Theme {
        get => _theme;
        init => _theme = ValidateDimension(value, nameof(value));
    }

    public string? Style {
        get => _style;
        init => _style = ValidateDimension(value, nameof(value));
    }

    public float DisplayScale {
        get => _displayScale;
        init {
            if (!float.IsFinite(value) || value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A display scale must be finite and greater than zero.");
            }

            _displayScale = value;
        }
    }

    public bool Equals(ResourceVariant? other) {
        return other is not null &&
               string.Equals(Culture, other.Culture, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Theme, other.Theme, StringComparison.Ordinal) &&
               string.Equals(Style, other.Style, StringComparison.Ordinal) &&
               DisplayScale.Equals(other.DisplayScale);
    }

    public override bool Equals(object? obj) {
        return Equals(obj as ResourceVariant);
    }

    public override int GetHashCode() {
        return HashCode.Combine(
            Culture?.ToUpperInvariant(),
            Theme,
            Style,
            DisplayScale);
    }

    private static string? ValidateDimension(
        string? value,
        string parameterName) {
        if (value is null) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException(
                "A resource variant dimension cannot be empty or whitespace.",
                parameterName);
        }

        return value;
    }
}
