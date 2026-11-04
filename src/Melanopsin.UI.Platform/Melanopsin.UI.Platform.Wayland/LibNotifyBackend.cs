using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed class LibNotifyBackend : IDisposable {
    private const string DefaultAction = "default";
    private const string ActionPrefix = "melanopsin.action.";
    private readonly object _gate = new();
    private readonly LibNotifyNative _native;
    private readonly Action<NotificationActivatedEventArgs> _activatedSink;
    private readonly Dictionary<NotificationId, NotificationState> _states = [];
    private readonly HashSet<string> _serverCapabilities;
    private readonly LibNotifyNative.notify_action_callback _actionCallback;
    private readonly LibNotifyNative.notify_closed_callback _closedCallback;
    private bool _disposed;

    private LibNotifyBackend(
        LibNotifyNative native,
        Action<NotificationActivatedEventArgs> activatedSink) {
        _native = native;
        _activatedSink = activatedSink;
        _actionCallback = HandleAction;
        _closedCallback = HandleClosed;
        _serverCapabilities = ReadServerCapabilities(native);
        Capabilities = CreateCapabilities(native, _serverCapabilities);
    }

    public NotificationCapabilities Capabilities { get; }

    public static LibNotifyBackend? TryCreate(
        Action<NotificationActivatedEventArgs> activatedSink) {
        ArgumentNullException.ThrowIfNull(activatedSink);
        var native = LibNotifyNative.TryCreate();
        if (native is null) {
            return null;
        }

        try {
            return new LibNotifyBackend(native, activatedSink);
        } catch {
            native.Dispose();
            return null;
        }
    }

    public ValueTask<NotificationResult> ShowAsync(
        LinuxNotificationRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate) {
            var portable = request.Portable;
            if (portable.Actions.Count != 0 &&
                !_serverCapabilities.Contains("actions")) {
                return ValueTask.FromResult(
                    NotificationResult.Rejected(NotificationFeature.Actions));
            }

            var omitted = GetOmittedFeatures(portable, request.Presentation);
            if (!Show(portable, request.Presentation)) {
                return ValueTask.FromResult(NotificationResult.Unavailable());
            }

            var applied = GetAppliedFeatures(portable, request.Presentation) &
                ~omitted;
            return ValueTask.FromResult(omitted == NotificationFeature.None
                ? NotificationResult.Accepted(applied)
                : NotificationResult.Degraded(applied, omitted));
        }
    }

    public bool Dismiss(NotificationId id) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate) {
            if (!_states.TryGetValue(id, out var state)) {
                return false;
            }

            if (_native.Close(state.Handle, out var error) == 0) {
                FreeError(error);
                return false;
            }

            FreeError(error);
            RemoveState(state);
            return true;
        }
    }

    public void Pump() {
        if (!Monitor.TryEnter(_gate)) {
            return;
        }

        try {
            if (!_disposed) {
                _native.Pump();
            }
        } finally {
            Monitor.Exit(_gate);
        }
    }

    public void Dispose() {
        lock (_gate) {
            if (_disposed) {
                return;
            }

            _disposed = true;
            foreach (var state in _states.Values.ToArray()) {
                RemoveState(state);
            }

            _native.Dispose();
        }
    }

    private bool Show(
        PortableNotificationRequest request,
        LinuxNotificationPresentation presentation) {
        _states.TryGetValue(request.Id, out var state);
        if (state is null ||
            _native.Update(
                state.Handle,
                request.Title,
                request.Body,
                request.Icon?.Source) == 0) {
            if (state is not null) {
                RemoveState(state);
            }

            var handle = _native.New(
                request.Title,
                request.Body,
                request.Icon?.Source);
            if (handle == IntPtr.Zero) {
                return false;
            }

            state = new NotificationState(request.Id, handle);
            _native.SetAppName(handle, "Melanopsin");
            state.ActionHandler = _native.SignalConnectData(
                handle,
                "action-activated",
                Marshal.GetFunctionPointerForDelegate(_actionCallback),
                GCHandle.ToIntPtr(state.CallbackHandle),
                IntPtr.Zero,
                0);
            state.ClosedHandler = _native.SignalConnectData(
                handle,
                "closed",
                Marshal.GetFunctionPointerForDelegate(_closedCallback),
                GCHandle.ToIntPtr(state.CallbackHandle),
                IntPtr.Zero,
                0);
            _states.Add(request.Id, state);
        }

        _native.ClearActions(state.Handle);
        state.SetActions(request.Actions);
        if (_serverCapabilities.Contains("actions")) {
            _native.AddAction(
                state.Handle,
                DefaultAction,
                string.Empty,
                Marshal.GetFunctionPointerForDelegate(_actionCallback),
                GCHandle.ToIntPtr(state.CallbackHandle),
                IntPtr.Zero);
        }

        for (var index = 0; index < request.Actions.Count; index++) {
            var action = request.Actions[index];
            _native.AddAction(
                state.Handle,
                $"{ActionPrefix}{index}",
                action.Label,
                Marshal.GetFunctionPointerForDelegate(_actionCallback),
                GCHandle.ToIntPtr(state.CallbackHandle),
                IntPtr.Zero);
        }

        ApplyPresentation(state.Handle, request, presentation);
        state.HasActivated = false;
        if (_native.Show(state.Handle, out var error) != 0) {
            FreeError(error);
            return true;
        }

        FreeError(error);
        RemoveState(state);
        return false;
    }

    private void ApplyPresentation(
        IntPtr handle,
        PortableNotificationRequest request,
        LinuxNotificationPresentation presentation) {
        var timeout = request.ExpiresAfter is { } expiry
            ? ToTimeout(expiry)
            : -1;
        _native.SetTimeout(handle, timeout);

        if (presentation.Category is { Length: > 0 } category) {
            _native.SetHintString(handle, "category", category);
        }

        if (presentation.DesktopEntry is { Length: > 0 } desktopEntry) {
            _native.SetHintString(handle, "desktop-entry", desktopEntry);
        }

        if (presentation.Sound is { Length: > 0 } sound &&
            _serverCapabilities.Contains("sound")) {
            _native.SetHintString(handle, "sound-file", sound);
        }

        if (presentation.Urgency is { } urgency) {
            _native.SetHintByte(handle, "urgency", urgency switch {
                LinuxNotificationUrgency.Low => 0,
                LinuxNotificationUrgency.Normal => 1,
                LinuxNotificationUrgency.Critical => 2,
                _ => 1,
            });
        }

    }

    private NotificationFeature GetOmittedFeatures(
        PortableNotificationRequest request,
        LinuxNotificationPresentation presentation) {
        var omitted = NotificationFeature.None;
        if (request.Body is not null &&
            !_serverCapabilities.Contains("body")) {
            omitted |= NotificationFeature.Body;
        }

        if (request.Icon is not null &&
            !_serverCapabilities.Contains("icon-static")) {
            omitted |= NotificationFeature.Icon;
        }

        if (request.Progress is not null) {
            omitted |= NotificationFeature.Progress;
        }

        if (presentation.Sound is { Length: > 0 } &&
            !_serverCapabilities.Contains("sound")) {
            omitted |= NotificationFeature.Sound;
        }

        return omitted;
    }

    private void HandleAction(
        IntPtr notification,
        IntPtr action,
        IntPtr userData) {
        lock (_gate) {
            var state = GetState(userData);
            var nativeActionId = Marshal.PtrToStringUTF8(action);
            if (state is null || string.IsNullOrEmpty(nativeActionId)) {
                return;
            }

            string? actionId = null;
            var kind = NotificationActivationKind.Body;
            if (nativeActionId != DefaultAction) {
                if (!state.TryGetActionId(nativeActionId, out actionId)) {
                    return;
                }

                kind = NotificationActivationKind.Action;
            }

            state.HasActivated = true;
            _activatedSink(new NotificationActivatedEventArgs {
                Id = state.Id,
                Kind = kind,
                ActionId = actionId,
            });
        }
    }

    private void HandleClosed(IntPtr notification, IntPtr userData) {
        lock (_gate) {
            var state = GetState(userData);
            if (state is null || state.IsRemoving) {
                return;
            }

            if (!state.HasActivated) {
                _activatedSink(new NotificationActivatedEventArgs {
                    Id = state.Id,
                    Kind = NotificationActivationKind.Dismissed,
                });
            }

            RemoveState(state);
        }
    }

    private void RemoveState(NotificationState state) {
        if (state.IsRemoving) {
            return;
        }

        state.IsRemoving = true;
        if (state.ActionHandler != 0) {
            _native.SignalHandlerDisconnect(state.Handle, state.ActionHandler);
        }

        if (state.ClosedHandler != 0) {
            _native.SignalHandlerDisconnect(state.Handle, state.ClosedHandler);
        }

        _states.Remove(state.Id);
        _native.ObjectUnref(state.Handle);
        if (state.CallbackHandle.IsAllocated) {
            state.CallbackHandle.Free();
        }
    }

    private static NotificationState? GetState(IntPtr userData) {
        if (userData == IntPtr.Zero) {
            return null;
        }

        return GCHandle.FromIntPtr(userData).Target as NotificationState;
    }

    private void FreeError(IntPtr error) {
        if (error != IntPtr.Zero) {
            _native.ErrorFree(error);
        }
    }

    private static int ToTimeout(TimeSpan value) {
        if (value == Timeout.InfiniteTimeSpan) {
            return 0;
        }

        var milliseconds = value.TotalMilliseconds;
        return milliseconds >= int.MaxValue
            ? int.MaxValue
            : Math.Max(1, (int)Math.Ceiling(milliseconds));
    }

    private static HashSet<string> ReadServerCapabilities(
        LibNotifyNative native) {
        var capabilities = new HashSet<string>(StringComparer.Ordinal);
        var list = native.GetServerCaps();
        try {
            for (var node = list; node != IntPtr.Zero;) {
                var entry = Marshal.PtrToStructure<LibNotifyNative.GList>(node);
                var capability = Marshal.PtrToStringUTF8(entry.Data);
                if (!string.IsNullOrEmpty(capability)) {
                    capabilities.Add(capability);
                }

                node = entry.Next;
            }
        } finally {
            if (list != IntPtr.Zero) {
                native.ListFree(list);
            }
        }

        return capabilities;
    }

    private static NotificationCapabilities CreateCapabilities(
        LibNotifyNative native,
        HashSet<string> serverCapabilities) {
        var features = NotificationFeature.Dismiss |
            NotificationFeature.Replacement |
            NotificationFeature.Expiry;
        if (serverCapabilities.Contains("body")) {
            features |= NotificationFeature.Body;
        }

        if (serverCapabilities.Contains("icon-static")) {
            features |= NotificationFeature.Icon;
        }

        if (serverCapabilities.Contains("actions")) {
            features |= NotificationFeature.Actions;
        }

        if (serverCapabilities.Contains("sound")) {
            features |= NotificationFeature.Sound;
        }

        return new NotificationCapabilities {
            CanSubmit = native.IsInitialized,
            Features = features,
            Limitation = "Progress is unavailable through the Desktop Notifications protocol.",
        };
    }

    private static NotificationFeature GetAppliedFeatures(
        PortableNotificationRequest request,
        LinuxNotificationPresentation presentation) {
        var features = NotificationFeature.Dismiss |
            NotificationFeature.Replacement;
        if (request.Body is not null) {
            features |= NotificationFeature.Body;
        }

        if (request.Icon is not null) {
            features |= NotificationFeature.Icon;
        }

        if (request.Actions.Count != 0) {
            features |= NotificationFeature.Actions;
        }

        if (request.Progress is not null) {
            features |= NotificationFeature.Progress;
        }

        if (request.ExpiresAfter is not null) {
            features |= NotificationFeature.Expiry;
        }

        if (presentation.Sound is { Length: > 0 }) {
            features |= NotificationFeature.Sound;
        }

        return features;
    }

    private sealed class NotificationState {
        public NotificationState(NotificationId id, IntPtr handle) {
            Id = id;
            Handle = handle;
            CallbackHandle = GCHandle.Alloc(this);
        }

        public NotificationId Id { get; }
        public IntPtr Handle { get; }
        public GCHandle CallbackHandle { get; }
        public ulong ActionHandler { get; set; }
        public ulong ClosedHandler { get; set; }
        public bool IsRemoving { get; set; }
        public bool HasActivated { get; set; }
        private Dictionary<string, string>? _actions;

        public void SetActions(IEnumerable<NotificationAction> actions) {
            _actions = actions
                .Select(static (action, index) => (action, index))
                .ToDictionary(
                    static item => $"{ActionPrefix}{item.index}",
                    static item => item.action.Id,
                    StringComparer.Ordinal);
        }

        public bool TryGetActionId(string nativeId, out string? actionId) {
            actionId = null;
            return _actions?.TryGetValue(nativeId, out actionId) == true;
        }
    }
}
