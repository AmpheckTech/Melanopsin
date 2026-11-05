// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.App;

// TODO(resources): Define whether Object values are runtime-only or serializable.
// Resource pack version 1 currently stores no object graph.
/// <summary>
/// Describes the semantic contract of a resource. The value type alone is not
/// enough because messages and plain strings share a storage representation, as
/// do images and opaque binary data, but require different validation and
/// decoding.
/// </summary>
public enum ResourceKind {
    String,
    Message,
    Image,
    Font,
    Color,
    Length,
    Number,
    Binary,
    Object,
}
