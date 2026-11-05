// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// Identifies a coordinate reference system within an authority namespace.
/// </summary>
/// <remarks>
/// The identifier is metadata, not a definition. Resolving an identifier to a
/// datum, ellipsoid, axis convention, or coordinate operation is the
/// responsibility of a registry.
/// </remarks>
public readonly record struct CoordinateReferenceSystemId {
    /// <summary>
    /// Initializes a coordinate reference system identifier.
    /// </summary>
    public CoordinateReferenceSystemId(string authority, int code) {
        if (string.IsNullOrWhiteSpace(authority)) {
            throw new ArgumentException(
                "An authority name is required.",
                nameof(authority));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(code);

        Authority = authority.ToUpperInvariant();
        Code = code;
    }

    /// <summary>Gets the normalized authority name.</summary>
    public string Authority { get; }

    /// <summary>Gets the authority-assigned code.</summary>
    public int Code { get; }

    /// <summary>Creates an EPSG identifier.</summary>
    public static CoordinateReferenceSystemId Epsg(int code) {
        return new CoordinateReferenceSystemId("EPSG", code);
    }

    public override string ToString() {
        return $"{Authority}:{Code}";
    }
}
