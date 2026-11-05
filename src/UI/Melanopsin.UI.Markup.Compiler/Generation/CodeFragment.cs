// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Generation;

/// <summary>
/// A C# snippet under construction. Parts copied out of markup - an interpolation, an event
/// handler, a directive condition - keep the document offset they had, so the writer can point the
/// compiler back at the source.
/// </summary>
internal sealed class CodeFragment {
    private readonly List<Part> _parts = [];

    private CodeFragment() {
    }

    /// <summary>An empty fragment.</summary>
    public static CodeFragment Empty => new();

    /// <summary>A fragment that is generated code.</summary>
    public static CodeFragment Text(string text) {
        return new CodeFragment().Add(text);
    }

    /// <summary>A fragment copied out of the document at <paramref name="offset"/>.</summary>
    public static CodeFragment Source(string text, int offset) {
        return new CodeFragment().AddSource(text, offset);
    }

    /// <summary>A parenthesized fragment, which never changes the code it wraps.</summary>
    public static CodeFragment Parenthesize(CodeFragment fragment) {
        return Text("(").Add(fragment).Add(")");
    }

    public IReadOnlyList<Part> Parts => _parts;

    /// <summary>Appends generated code.</summary>
    public CodeFragment Add(string text) {
        if (text.Length != 0) {
            _parts.Add(new Part(text, -1));
        }

        return this;
    }

    /// <summary>Appends another fragment, keeping its own parts.</summary>
    public CodeFragment Add(CodeFragment fragment) {
        _parts.AddRange(fragment._parts);
        return this;
    }

    /// <summary>Appends code copied from the document, which a <c>#line</c> can point back at.</summary>
    public CodeFragment AddSource(string text, int offset) {
        if (text.Length != 0) {
            _parts.Add(new Part(text, offset));
        }

        return this;
    }

    /// <summary>A run of the snippet: <paramref name="offset"/> is where it sits in markup, or -1.</summary>
    public readonly record struct Part(string Text, int Offset) {
        public bool IsSource => Offset >= 0;
    }
}
