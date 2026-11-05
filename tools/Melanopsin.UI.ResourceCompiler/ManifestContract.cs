// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.App;

namespace Melanopsin.UI.ResourceCompiler;

internal sealed class ManifestContract {
    public ManifestContract(
        string path,
        string package,
        IReadOnlyList<ResourceDescriptor> descriptors) {
        Path = path;
        Package = package;
        Contract = new ResourceContract(descriptors, package, assembly: null);
    }

    public string Path { get; }

    public string Package { get; }

    public ResourceContract Contract { get; }
}
