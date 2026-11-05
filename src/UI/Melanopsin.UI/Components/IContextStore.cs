// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

internal interface IContextStore {
    Dictionary<object, object>? ContextValues { get; set; }
}
