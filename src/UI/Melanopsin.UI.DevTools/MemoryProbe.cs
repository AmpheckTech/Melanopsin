// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Animation;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.DevTools;

/// <summary>
/// An invisible node that reports UI frame ticks to the owning
/// <see cref="DevToolsComponent"/>. It never requests another frame.
/// </summary>
[MarkupElement]
public sealed class MemoryProbe : Control, IAnimationParticipant {
    public MemoryProbe()
        : base("MemoryProbe") {
    }

    /// <summary>
    /// Invoked once per UI frame while the probe is mounted.
    /// </summary>
    public Action? Tick { get; set; }

    public bool AdvanceAnimation(float elapsedSeconds) {
        Tick?.Invoke();
        return false;
    }
}
