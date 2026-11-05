// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

internal abstract class ElementBinding {
    public abstract void Retarget(Element target);

    public abstract void Activate();

    public abstract void Deactivate();
}
