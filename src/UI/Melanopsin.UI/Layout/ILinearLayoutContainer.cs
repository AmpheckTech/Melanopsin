// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Layout;

/// <summary>
/// Describes a container whose children flow along a single linear axis.
/// The layout engine uses this contract without depending on concrete controls.
/// </summary>
public interface ILinearLayoutContainer {
    StackOrientation Orientation { get; }

    Length Spacing { get; }
}
