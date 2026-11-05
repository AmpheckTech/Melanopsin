// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Text;
using Melanopsin.UI.Markup.Compiler.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Melanopsin.UI.LanguageServer.Workspace;

internal sealed record LoadedProject(
    string ProjectPath,
    MSBuildWorkspace Workspace,
    Project Project,
    Compilation Compilation);

internal sealed record MarkupBuffer(string Path, string Text);

/// <summary>The markup, resource manifests and editor buffers of one project.</summary>
internal sealed record MarkupInputs(
    IReadOnlyList<MarkupSourceFile> Documents,
    IReadOnlyList<MarkupSourceFile> Resources,
    IReadOnlyList<MarkupSourceFile> Buffers,
    IReadOnlyList<McssSourceFile> Stylesheets);

internal sealed class MarkupWorkspace : IDisposable {
    private readonly ConcurrentDictionary<string, LoadedProject> _projects =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AnalyzedCompilation> _analyses =
        new(StringComparer.Ordinal);
    private readonly Func<IReadOnlyList<MarkupBuffer>> _buffers;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public MarkupWorkspace(Func<IReadOnlyList<MarkupBuffer>> buffers) {
        _buffers = buffers;
    }

    private sealed record AnalyzedCompilation(string Signature, Compilation Compilation);

    public async Task<Compilation> GetCompilationAsync(
        string documentPath,
        CancellationToken cancellationToken) {
        var loaded = await GetProjectAsync(documentPath, cancellationToken);
        return await AnalyzeAsync(loaded, cancellationToken);
    }

    public async Task<IReadOnlyList<McssSourceFile>> GetStylesheetsAsync(
        string documentPath,
        CancellationToken cancellationToken) {
        var loaded = await GetProjectAsync(documentPath, cancellationToken);
        var inputs = await CollectAsync(loaded, cancellationToken);
        return inputs.Stylesheets;
    }

    /// <summary>
    /// Markup-only components are not part of the project on disk, so they are contributed as
    /// symbol-only declarations before analysis runs. The open buffers then replace the generated
    /// tree of every document they cover, which is what makes the answers follow unsaved edits.
    /// </summary>
    private async Task<Compilation> AnalyzeAsync(
        LoadedProject loaded,
        CancellationToken cancellationToken) {
        var inputs = await CollectAsync(loaded, cancellationToken);
        var signature = Signature(inputs);
        if (_analyses.TryGetValue(loaded.ProjectPath, out var cached) &&
            cached.Signature == signature) {
            return cached.Compilation;
        }

        var compilation = MarkupLanguageService.AddMarkupComponents(
            loaded.Compilation,
            inputs.Documents);
        compilation = MarkupLanguageService.ApplyMarkupBuffers(
            compilation,
            inputs.Buffers,
            inputs.Resources);
        _analyses[loaded.ProjectPath] = new AnalyzedCompilation(signature, compilation);
        return compilation;
    }

    private async Task<MarkupInputs> CollectAsync(
        LoadedProject loaded,
        CancellationToken cancellationToken) {
        var byPath = new Dictionary<string, string>(StringComparer.Ordinal);
        var resources = new List<MarkupSourceFile>();
        var stylesheetsByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var document in loaded.Project.AdditionalDocuments) {
            if (document.FilePath is null) {
                continue;
            }

            if (document.FilePath.EndsWith(".mres", StringComparison.OrdinalIgnoreCase)) {
                resources.Add(new MarkupSourceFile(
                    document.FilePath,
                    (await document.GetTextAsync(cancellationToken)).ToString()));
                continue;
            }

            if (document.FilePath.EndsWith(".mcss", StringComparison.OrdinalIgnoreCase)) {
                var stylesheetText = await document.GetTextAsync(cancellationToken);
                stylesheetsByPath[document.FilePath] = stylesheetText.ToString();
                continue;
            }

            if (!document.FilePath.EndsWith(".mxml", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var text = await document.GetTextAsync(cancellationToken);
            byPath[document.FilePath] = text.ToString();
        }

        var buffers = new List<MarkupSourceFile>();
        foreach (var buffer in _buffers()) {
            if (buffer.Path.EndsWith(".mcss", StringComparison.OrdinalIgnoreCase)) {
                // Unsaved stylesheets must participate in imports and token navigation too.
                stylesheetsByPath[buffer.Path] = buffer.Text;
                continue;
            }

            if (!buffer.Path.EndsWith(".mxml", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            // Unsaved editors win over the copy on disk.
            byPath[buffer.Path] = buffer.Text;
            buffers.Add(new MarkupSourceFile(buffer.Path, buffer.Text));
        }

        var documents = new List<MarkupSourceFile>(byPath.Count);
        foreach (var entry in byPath.OrderBy(static entry => entry.Key, StringComparer.Ordinal)) {
            documents.Add(new MarkupSourceFile(entry.Key, entry.Value));
        }

        var stylesheets = stylesheetsByPath
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .Select(static entry => new McssSourceFile(entry.Key, entry.Value))
            .ToArray();
        return new MarkupInputs(documents, resources, buffers, stylesheets);
    }

    private static string Signature(MarkupInputs inputs) {
        var builder = new StringBuilder();
        foreach (var document in inputs.Documents.Concat(inputs.Resources)) {
            builder.Append(document.Path).Append(':').Append(Hash(document.Text)).Append(';');
        }

        foreach (var stylesheet in inputs.Stylesheets) {
            builder.Append(stylesheet.Path).Append(':').Append(Hash(stylesheet.Text)).Append(';');
        }

        return builder.ToString();
    }

    private static uint Hash(string text) {
        var hash = 2166136261u;
        foreach (var character in text) {
            hash = (hash ^ character) * 16777619u;
        }

        return hash;
    }

    private async Task<LoadedProject> GetProjectAsync(
        string documentPath,
        CancellationToken cancellationToken) {
        var projectPath = LocateProject(documentPath);
        if (_projects.TryGetValue(projectPath, out var cached)) {
            return cached;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try {
            if (_projects.TryGetValue(projectPath, out cached)) {
                return cached;
            }

            var workspace = MSBuildWorkspace.Create(new Dictionary<string, string> {
                ["DesignTimeBuild"] = "true",
                ["BuildProjectReferences"] = "false",
            });
            workspace.RegisterWorkspaceFailedHandler(args =>
                Console.Error.WriteLine($"mxml workspace: {args.Diagnostic.Message}"));
            var project = await workspace.OpenProjectAsync(
                projectPath,
                cancellationToken: CancellationToken.None);
            var compilation = await project.GetCompilationAsync(CancellationToken.None) ??
                              throw new InvalidOperationException(
                                  $"The project '{projectPath}' did not produce a C# compilation.");
            var loaded = new LoadedProject(projectPath, workspace, project, compilation);
            if (!_projects.TryAdd(projectPath, loaded)) {
                workspace.Dispose();
            }

            _analyses.TryRemove(projectPath, out _);
            return loaded;
        } finally {
            _loadLock.Release();
        }
    }

    public void Invalidate(string path) {
        var fullPath = Path.GetFullPath(path);
        foreach (var entry in _projects.ToArray()) {
            if (!fullPath.StartsWith(
                    Path.GetDirectoryName(entry.Key) + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal)) {
                continue;
            }

            if (_projects.TryRemove(entry.Key, out var removed)) {
                removed.Workspace.Dispose();
            }

            _analyses.TryRemove(entry.Key, out _);
        }
    }

    public void Dispose() {
        foreach (var project in _projects.Values) {
            project.Workspace.Dispose();
        }

        _projects.Clear();
        _analyses.Clear();
        _loadLock.Dispose();
    }

    private static string LocateProject(string documentPath) {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(documentPath))!);
        while (directory is not null) {
            var projects = directory.GetFiles("*.csproj").OrderBy(
                static file => file.Name,
                StringComparer.Ordinal).ToArray();
            if (projects.Length > 0) {
                return projects[0].FullName;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"No C# project was found above '{documentPath}'.");
    }
}
