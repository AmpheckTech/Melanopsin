// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Mathematics;

/// <summary>
/// Selects the ordering used when reading or writing matrix values from a
/// contiguous buffer.
/// </summary>
/// <remarks>
/// Storage order is an interop concern. It does not change matrix value
/// semantics or the column-vector multiplication order.
/// </remarks>
public enum MatrixStorageOrder {
    /// <summary>
    /// Consecutive values fill one matrix row at a time.
    /// </summary>
    RowMajor,

    /// <summary>
    /// Consecutive values fill one matrix column at a time.
    /// </summary>
    ColumnMajor,
}
