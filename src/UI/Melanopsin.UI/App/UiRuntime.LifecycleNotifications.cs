// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.ExceptionServices;

namespace Melanopsin.UI.App;

public sealed partial class UiRuntime {
    private readonly Queue<UiLifecycleState> _lifecycleNotifications = new();
    private bool _lifecycleNotificationPumpActive;
    private Action<UiLifecycleState>? _lifecycleChanged;

    public event Action<UiLifecycleState>? LifecycleChanged {
        add {
            lock (_gate) {
                _lifecycleChanged += value;
            }
        }

        remove {
            lock (_gate) {
                _lifecycleChanged -= value;
            }
        }
    }

    private bool EnqueueLifecycleNotificationLocked(
        UiLifecycleState state,
        bool canPump) {
        _lifecycleNotifications.Enqueue(state);
        if (!canPump || _lifecycleNotificationPumpActive) {
            return false;
        }

        _lifecycleNotificationPumpActive = true;
        return true;
    }

    private void PumpLifecycleNotifications() {
        try {
            while (true) {
                UiLifecycleState state;
                Action<UiLifecycleState>? handlers;
                lock (_gate) {
                    if (_lifecycleNotifications.Count == 0) {
                        _lifecycleNotificationPumpActive = false;
                        return;
                    }

                    state = _lifecycleNotifications.Dequeue();
                    handlers = _lifecycleChanged;
                }

                Exception? failure = null;
                if (handlers is not null) {
                    foreach (var handler in handlers.GetInvocationList()) {
                        try {
                            ((Action<UiLifecycleState>)handler)(state);
                        } catch (Exception exception) {
                            failure = CombineFailures(failure, exception);
                        }
                    }
                }

                if (failure is not null) {
                    ExceptionDispatchInfo.Capture(failure).Throw();
                }
            }
        } catch {
            lock (_gate) {
                _lifecycleNotifications.Clear();
                _lifecycleNotificationPumpActive = false;
            }

            throw;
        }
    }
}
