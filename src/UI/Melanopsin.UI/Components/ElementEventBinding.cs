// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

internal sealed class ElementEventBinding : ElementBinding {
    private readonly Action<Element> _subscribe;
    private readonly Action<Element> _unsubscribe;
    private Element _target;
    private bool _isActive;

    public ElementEventBinding(
        Element target,
        Action<Element> subscribe,
        Action<Element> unsubscribe) {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(unsubscribe);
        _target = target;
        _subscribe = subscribe;
        _unsubscribe = unsubscribe;
    }

    public override void Retarget(Element target) {
        ArgumentNullException.ThrowIfNull(target);
        if (ReferenceEquals(_target, target)) {
            return;
        }

        if (_isActive) {
            _unsubscribe(_target);
        }

        _target = target;
        if (_isActive) {
            _subscribe(_target);
        }
    }

    public override void Activate() {
        if (_isActive) {
            return;
        }

        _isActive = true;
        _subscribe(_target);
    }

    public override void Deactivate() {
        if (!_isActive) {
            return;
        }

        _isActive = false;
        _unsubscribe(_target);
    }
}
