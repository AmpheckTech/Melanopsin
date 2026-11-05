// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Platform.Wayland;

internal sealed class PortalVariantBuilder : IDisposable {
    private readonly GLibPortalNative _native;
    private readonly IntPtr _builder;
    private bool _ended;

    public PortalVariantBuilder(GLibPortalNative native, string type) {
        _native = native;
        var typeHandle = native.VariantTypeNew(type);
        try {
            _builder = native.VariantBuilderNew(typeHandle);
        } finally {
            native.VariantTypeFree(typeHandle);
        }
    }

    public void AddString(string value) {
        AddValue(_native.VariantNewString(value));
    }

    public void AddValue(IntPtr value) {
        ObjectDisposedException.ThrowIf(_ended, this);
        _native.VariantBuilderAddValue(_builder, value);
    }

    public void AddVariantEntry(string key, IntPtr value) {
        var keyVariant = _native.VariantNewString(key);
        var wrappedValue = _native.VariantNewVariant(value);
        AddValue(_native.VariantNewDictEntry(keyVariant, wrappedValue));
    }

    public IntPtr End() {
        ObjectDisposedException.ThrowIf(_ended, this);
        _ended = true;
        try {
            return _native.VariantBuilderEnd(_builder);
        } finally {
            _native.VariantBuilderUnref(_builder);
        }
    }

    public void Dispose() {
        if (!_ended) {
            _ended = true;
            _native.VariantBuilderUnref(_builder);
        }
    }
}
