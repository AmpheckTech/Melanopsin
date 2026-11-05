// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.App;

/// <summary>
/// Selects where a resource pack is loaded from. Application packs should use
/// <see cref="Embedded"/> or <see cref="AppDirectory"/>; explicit files are
/// intended for externally distributed overlays.
/// </summary>
public sealed class ResourceStorage {
    private const byte EmbeddedKind = 0;
    private const byte AppDirectoryKind = 1;
    private const byte FileKind = 2;
    private const byte ApplicationKind = 3;

    private readonly byte _kind;

    private ResourceStorage(byte kind, string? path = null) {
        _kind = kind;
        Path = path;
    }

    public static ResourceStorage Embedded { get; } = new(EmbeddedKind);

    public static ResourceStorage AppDirectory { get; } = new(AppDirectoryKind);

    /// <summary>
    /// Loads the application pack from its embedded location when present, then
    /// falls back to the application directory. This keeps application code
    /// independent from the build-time deployment choice.
    /// </summary>
    public static ResourceStorage Application { get; } = new(ApplicationKind);

    internal string? Path { get; }

    internal bool IsEmbedded => _kind == EmbeddedKind;

    internal bool IsAppDirectory => _kind == AppDirectoryKind;

    internal bool IsFile => _kind == FileKind;

    internal bool IsApplication => _kind == ApplicationKind;

    internal string DisplayName => _kind switch {
        EmbeddedKind => "Embedded",
        AppDirectoryKind => "AppDirectory",
        ApplicationKind => "Application",
        _ => Path!,
    };

    public static ResourceStorage File(string path) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new ResourceStorage(FileKind, path);
    }
}
