// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

/// <summary>
/// Builder used by source-generated views.
/// </summary>
public sealed class Builder {
    private readonly Stack<Element> _openElements = new();
    private readonly List<Node> _roots = [];

    public void Open<T>()
        where T : Element, new() {
        Open(new T());
    }

    public void Open(Element element) {
        ArgumentNullException.ThrowIfNull(element);
        if (_openElements.TryPeek(out var parent)) {
            parent.AppendChild(element);
        } else {
            _roots.Add(element);
        }

        _openElements.Push(element);
    }

    public void Open(string type) {
        Open(new GenericElement(type));
    }

    public void Close() {
        if (_openElements.Count == 0) {
            throw new InvalidOperationException("Cannot close an element because no element is open.");
        }

        _openElements.Pop();
    }

    public void Set(string name, object? value) {
        if (!_openElements.TryPeek(out var element)) {
            throw new InvalidOperationException("An attribute can only be set while an element is open.");
        }

        element.SetAttributeValue(name, value);
    }

    public void Text(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return;
        }

        if (!_openElements.TryPeek(out var element)) {
            throw new InvalidOperationException("Text nodes must be inside an element.");
        }

        element.AppendContent(value);
    }

    public Node Build() {
        if (_openElements.Count != 0) {
            throw new InvalidOperationException(
                $"Cannot build the UI tree while {_openElements.Count} element(s) remain open.");
        }

        return _roots.Count switch {
            1 => _roots[0],
            0 => throw new InvalidOperationException("The markup did not produce a root element."),
            _ => throw new InvalidOperationException(
                "Markup must produce exactly one root element. Wrap multiple roots in a container."),
        };
    }
}
