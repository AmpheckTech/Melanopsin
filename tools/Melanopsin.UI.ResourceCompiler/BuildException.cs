// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.ResourceCompiler;

internal sealed class BuildException : Exception {
    public BuildException(string message)
        : base(message) {
    }

    public BuildException(string message, Exception innerException)
        : base(message, innerException) {
    }
}
