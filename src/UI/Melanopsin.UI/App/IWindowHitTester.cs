// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.App;

/// <summary>
/// Read-only window tree access for pointer routing and focus. Hit results are
/// neutral snapshots so callers do not depend on rendering layout types.
/// </summary>
public interface IWindowHitTester : IWindowTreeQuery {
    WindowHitTarget? HitTest(WindowId windowId, float x, float y);
}
