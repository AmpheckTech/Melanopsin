using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Wayland;

/// <summary>
/// Restricts libdecor to its lightweight cairo decoration backend.
/// </summary>
/// <remarks>
/// libdecor tries every plugin in its directory by priority, so a distribution
/// that also ships the GTK backend maps the GLib/Pango/ICU stack (roughly 19 MiB
/// resident) into the process. Pointing <c>LIBDECOR_PLUGIN_DIR</c> at a private
/// directory that exposes only <c>libdecor-cairo.so</c> avoids that. A variable
/// supplied by the host application is respected, and every step is best effort.
/// </remarks>
internal static partial class LibDecorPluginDirectory {
    private const string EnvironmentVariable = "LIBDECOR_PLUGIN_DIR";
    private const string LibraryFileName = "libdecor-0.so";
    private const string PluginRootName = "libdecor";
    private const string PluginApiPrefix = "plugins-";
    private const string CairoPluginFileName = "libdecor-cairo.so";
    private const string GtkPluginFileName = "libdecor-gtk.so";
    private const string PrivateDirectoryPrefix = ".melanopsin-libdecor-";
    private const string MapsPath = "/proc/self/maps";
    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.UserExecute;

    private static readonly string[] WellKnownLibraryDirectories = [
        "/usr/lib",
        "/usr/lib64",
        "/usr/local/lib",
        "/usr/lib/x86_64-linux-gnu",
        "/usr/lib/aarch64-linux-gnu",
        "/usr/lib/riscv64-linux-gnu",
        "/usr/lib/arm-linux-gnueabihf",
    ];

    private static readonly object SyncRoot = new();
    private static bool _attempted;

    /// <summary>
    /// Makes libdecor use the cairo backend before the first <c>libdecor_new</c>
    /// call. Safe to call more than once.
    /// </summary>
    public static void PreferCairoBackend() {
        lock (SyncRoot) {
            if (_attempted) {
                return;
            }

            _attempted = true;
            try {
                Apply();
            } catch (Exception exception) when (exception is IOException or
                UnauthorizedAccessException or NotSupportedException) {
                // The default plugin search still works, so the process keeps it.
            }
        }
    }

    private static void Apply() {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) is not null) {
            return;
        }

        var pluginDirectory = LocatePluginDirectory();
        if (pluginDirectory is null) {
            return;
        }

        var cairoPlugin = Path.Combine(pluginDirectory, CairoPluginFileName);
        if (!File.Exists(cairoPlugin) ||
            !File.Exists(Path.Combine(pluginDirectory, GtkPluginFileName))) {
            // Without a GTK plugin there is nothing to keep out of the process.
            return;
        }

        var resolvedPlugin = new FileInfo(cairoPlugin).ResolveLinkTarget(
            returnFinalTarget: true)?.FullName ?? Path.GetFullPath(cairoPlugin);
        var privateDirectory = CreatePrivateDirectory(resolvedPlugin);
        if (privateDirectory is null) {
            return;
        }

        if (SetEnvironmentVariable(EnvironmentVariable, privateDirectory, 1) != 0) {
            Directory.Delete(privateDirectory, recursive: true);
        }
    }

    private static string? LocatePluginDirectory() {
        // Loading libdecor maps it, so the mapped path stays authoritative for
        // custom prefixes such as /nix or /opt.
        _ = NativeLibrary.TryLoad("libdecor-0.so.0", out _);

        var libraryPath = FindMappedLibraryPath();
        if (libraryPath is not null && Path.GetDirectoryName(libraryPath) is { } directory) {
            var plugins = FindPluginApiDirectory(directory);
            if (plugins is not null) {
                return plugins;
            }
        }

        foreach (var root in WellKnownLibraryDirectories) {
            var plugins = FindPluginApiDirectory(root);
            if (plugins is not null) {
                return plugins;
            }
        }

        return null;
    }

    private static string? FindMappedLibraryPath() {
        foreach (var line in File.ReadLines(MapsPath)) {
            var index = line.IndexOf('/');
            if (index < 0) {
                continue;
            }

            var path = line[index..].TrimEnd();
            if (path.Contains(LibraryFileName, StringComparison.Ordinal)) {
                return path;
            }
        }

        return null;
    }

    private static string? FindPluginApiDirectory(string libraryDirectory) {
        var root = Path.Combine(libraryDirectory, PluginRootName);
        if (!Directory.Exists(root)) {
            return null;
        }

        string[] candidates;
        try {
            candidates = Directory.GetDirectories(root, PluginApiPrefix + "*");
        } catch (Exception exception) when (exception is IOException or
            UnauthorizedAccessException) {
            return null;
        }

        if (candidates.Length == 0) {
            return null;
        }

        Array.Sort(candidates, StringComparer.Ordinal);
        return candidates[^1];
    }

    private static string? CreatePrivateDirectory(string cairoPlugin) {
        if (OperatingSystem.IsWindows()) {
            return null;
        }

        var baseDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (string.IsNullOrEmpty(baseDirectory) || !Directory.Exists(baseDirectory)) {
            baseDirectory = Path.GetTempPath();
        }

        var target = Path.Combine(
            baseDirectory,
            PrivateDirectoryPrefix +
            Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "-" +
            Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(target, PrivateDirectoryMode);
        File.SetUnixFileMode(target, PrivateDirectoryMode);
        var link = Path.Combine(target, CairoPluginFileName);
        _ = File.CreateSymbolicLink(link, cairoPlugin);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => {
            try {
                Directory.Delete(target, recursive: true);
            } catch (Exception exception) when (exception is IOException or
                UnauthorizedAccessException) {
                // Process shutdown must not fail because temporary cleanup did.
            }
        };
        return target;
    }

    [LibraryImport("libc", EntryPoint = "setenv", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int SetEnvironmentVariable(string name, string value, int overwrite);
}
