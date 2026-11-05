namespace Melanopsin.UI.LanguageServer.Protocol;

internal sealed record OpenDocument(string Uri, string Path, string Text, int Version);

internal sealed class DocumentStore {
    private readonly Dictionary<string, OpenDocument> _documents =
        new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public void Open(string uri, string path, string text, int version) {
        lock (_lock) {
            _documents[uri] = new OpenDocument(uri, path, text, version);
        }
    }

    public void Update(string uri, string text, int version) {
        lock (_lock) {
            if (_documents.TryGetValue(uri, out var document)) {
                _documents[uri] = document with { Text = text, Version = version };
            }
        }
    }

    public void Close(string uri) {
        lock (_lock) {
            _documents.Remove(uri);
        }
    }

    public IReadOnlyList<OpenDocument> Snapshot() {
        lock (_lock) {
            return _documents.Values.ToArray();
        }
    }

    public bool TryGet(string uri, out OpenDocument document) {
        lock (_lock) {
            return _documents.TryGetValue(uri, out document!);
        }
    }
}
