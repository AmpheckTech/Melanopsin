// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Melanopsin.Mathematics.Coordinates;

/// <summary>
/// An immutable registry of geodetic coordinate reference systems and
/// coordinate operations.
/// </summary>
/// <remarks>
/// The registry is a small, explicit data catalog rather than a complete EPSG
/// database. Built-in entries identify common systems without inventing
/// transformation parameters. Callers add authoritative operations through
/// <see cref="Builder"/> and then resolve paths with
/// <see cref="GeodeticTransformSolver"/>.
/// </remarks>
public sealed class GeodeticCrsRegistry {
    private readonly Dictionary<CoordinateReferenceSystemId, GeodeticCrs> _systems;
    private readonly ReadOnlyCollection<GeodeticCoordinateOperation> _operations;

    private GeodeticCrsRegistry(
        Dictionary<CoordinateReferenceSystemId, GeodeticCrs> systems,
        GeodeticCoordinateOperation[] operations) {
        foreach (var operation in operations) {
            if (!systems.ContainsKey(operation.Source.Id)) {
                throw new InvalidOperationException(
                    $"Operation '{operation.Name}' references unregistered " +
                    $"source CRS '{operation.Source.Id}'.");
            }

            if (!systems.ContainsKey(operation.Target.Id)) {
                throw new InvalidOperationException(
                    $"Operation '{operation.Name}' references unregistered " +
                    $"target CRS '{operation.Target.Id}'.");
            }
        }

        _systems = systems;
        _operations = Array.AsReadOnly(operations);
    }

    /// <summary>
    /// Gets a new builder containing the built-in WGS84 and CGCS2000
    /// definitions.
    /// </summary>
    public static Builder CreateDefaultBuilder() {
        return new Builder()
            .AddCrs(GeodeticCrs.Wgs84)
            .AddCrs(GeodeticCrs.Cgcs2000);
    }

    /// <summary>Creates the default registry.</summary>
    public static GeodeticCrsRegistry CreateDefault() {
        return CreateDefaultBuilder().Build();
    }

    /// <summary>Creates an empty registry builder.</summary>
    public static Builder CreateBuilder() {
        return new Builder();
    }

    /// <summary>Gets the registered coordinate operations.</summary>
    public IReadOnlyList<GeodeticCoordinateOperation> Operations => _operations;

    /// <summary>Attempts to resolve a coordinate reference system.</summary>
    public bool TryGet(
        CoordinateReferenceSystemId id,
        [NotNullWhen(true)] out GeodeticCrs? system) {
        return _systems.TryGetValue(id, out system);
    }

    /// <summary>Builds a registry from explicit coordinate system definitions.</summary>
    public sealed class Builder {
        private readonly Dictionary<CoordinateReferenceSystemId, GeodeticCrs> _systems = [];
        private readonly List<GeodeticCoordinateOperation> _operations = [];

        /// <summary>Adds a coordinate reference system definition.</summary>
        public Builder AddCrs(GeodeticCrs system) {
            ArgumentNullException.ThrowIfNull(system);
            if (!_systems.TryAdd(system.Id, system)) {
                throw new ArgumentException(
                    $"Coordinate reference system '{system.Id}' is already registered.",
                    nameof(system));
            }

            return this;
        }

        /// <summary>Adds a directed coordinate operation.</summary>
        public Builder AddOperation(GeodeticCoordinateOperation operation) {
            ArgumentNullException.ThrowIfNull(operation);
            _operations.Add(operation);
            return this;
        }

        /// <summary>Builds an immutable registry.</summary>
        public GeodeticCrsRegistry Build() {
            return new GeodeticCrsRegistry(
                new Dictionary<CoordinateReferenceSystemId, GeodeticCrs>(
                    _systems),
                [.. _operations]);
        }
    }
}
