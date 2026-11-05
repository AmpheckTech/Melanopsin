// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Reactive;

/// <summary>
/// A consumer whose work must be repeated after a dependency changes.
/// </summary>
public interface IReactiveInvalidationTarget {
    void InvalidateFromReactiveSource();
}
