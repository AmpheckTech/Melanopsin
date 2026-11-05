// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed class DesktopPortalNotifications : IDisposable {
    private const string BusName = "org.freedesktop.portal.Desktop";
    private const string ObjectPath = "/org/freedesktop/portal/desktop";
    private const string InterfaceName = "org.freedesktop.portal.Notification";
    private const string DefaultAction = "melanopsin.default";
    private const string ActionPrefix = "melanopsin.action.";
    private readonly object _gate = new();
    private readonly Action<NotificationActivatedEventArgs> _activatedSink;
    private readonly GLibPortalNative? _native;
    private readonly GCHandle _selfHandle;
    private readonly GLibPortalNative.SignalCallback _signalCallback;
    private readonly Dictionary<NotificationId, Dictionary<string, string>>
        _actionsByNotification = [];
    private readonly HashSet<NotificationId> _activeNotifications = [];
    private uint _signalSubscription;
    private uint _version;
    private bool _disposed;

    public DesktopPortalNotifications(
        Action<NotificationActivatedEventArgs> activatedSink) {
        ArgumentNullException.ThrowIfNull(activatedSink);
        _activatedSink = activatedSink;
        _native = GLibPortalNative.TryCreate();
        _signalCallback = HandleActionInvoked;
        if (_native is null) {
            Capabilities = UnavailableCapabilities(
                "The XDG desktop portal and GIO are unavailable.");
            return;
        }

        _version = ReadVersion(_native);
        if (_version == 0) {
            Capabilities = UnavailableCapabilities(
                "The XDG desktop portal notification interface is unavailable.");
            _native.Dispose();
            _native = null;
            return;
        }

        _selfHandle = GCHandle.Alloc(this);
        _signalSubscription = _native.SignalSubscribe(
            _native.Connection,
            sender: BusName,
            InterfaceName,
            "ActionInvoked",
            ObjectPath,
            arg0: null,
            flags: 0,
            Marshal.GetFunctionPointerForDelegate(_signalCallback),
            GCHandle.ToIntPtr(_selfHandle),
            IntPtr.Zero);
        if (_signalSubscription == 0) {
            _selfHandle.Free();
            _native.Dispose();
            _native = null;
            Capabilities = UnavailableCapabilities(
                "The portal notification activation signal is unavailable.");
            return;
        }

        Capabilities = CreateCapabilities(_version);
    }

    public NotificationCapabilities Capabilities { get; }

    public ValueTask<NotificationResult> ShowAsync(
        LinuxNotificationRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        return new ValueTask<NotificationResult>(
            Task.Run(
                () => Show(request, cancellationToken),
                cancellationToken));
    }

    public ValueTask<bool> DismissAsync(
        NotificationId id,
        CancellationToken cancellationToken = default) {
        return new ValueTask<bool>(
            Task.Run(() => Dismiss(id, cancellationToken), cancellationToken));
    }

    public void Pump() {
        if (!Monitor.TryEnter(_gate)) {
            return;
        }

        try {
            if (!_disposed) {
                _native?.Pump();
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
            if (_native is not null && _signalSubscription != 0) {
                _native.SignalUnsubscribe(
                    _native.Connection,
                    _signalSubscription);
                _signalSubscription = 0;
            }

            if (_selfHandle.IsAllocated) {
                _selfHandle.Free();
            }

            _native?.Dispose();
            _actionsByNotification.Clear();
            _activeNotifications.Clear();
        }
    }

    private NotificationResult Show(
        LinuxNotificationRequest request,
        CancellationToken cancellationToken) {
        lock (_gate) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (_native is null) {
                return NotificationResult.Unavailable();
            }

            var portable = request.Portable;
            var omitted = GetOmittedFeatures(request);
            var parameters = CreateNotificationParameters(request, omitted);
            if (parameters == IntPtr.Zero ||
                !Call(
                    "AddNotification",
                    parameters,
                    replySignature: null,
                    cancellationToken)) {
                return NotificationResult.Unavailable();
            }

            _activeNotifications.Add(portable.Id);
            _actionsByNotification[portable.Id] = CreateActionMap(portable);
            var applied = GetAppliedFeatures(request) & ~omitted;
            return omitted == NotificationFeature.None
                ? NotificationResult.Accepted(applied)
                : NotificationResult.Degraded(applied, omitted);
        }
    }

    private bool Dismiss(
        NotificationId id,
        CancellationToken cancellationToken) {
        lock (_gate) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (_native is null || !_activeNotifications.Contains(id)) {
                return false;
            }

            var parameters = CreateStringTuple(id);
            if (parameters == IntPtr.Zero ||
                !Call(
                    "RemoveNotification",
                    parameters,
                    replySignature: null,
                    cancellationToken)) {
                return false;
            }

            _activeNotifications.Remove(id);
            _actionsByNotification.Remove(id);
            return true;
        }
    }

    private bool Call(
        string method,
        IntPtr parameters,
        string? replySignature,
        CancellationToken cancellationToken) {
        if (_native is null) {
            Unref(parameters);
            return false;
        }

        var cancellable = _native.CancellableNew();
        var registration = cancellationToken.Register(() => {
            _native.CancellableCancel(cancellable);
        });
        var replyType = CreateType(replySignature);
        IntPtr result;
        try {
            result = _native.ConnectionCallSync(
                _native.Connection,
                BusName,
                ObjectPath,
                InterfaceName,
                method,
                parameters,
                replyType,
                flags: 0,
                timeoutMilliseconds: 3000,
                cancellable,
                out var error);
            if (error != IntPtr.Zero) {
                _native.ErrorFree(error);
            }
        } finally {
            registration.Dispose();
            Unref(parameters);
            if (replyType != IntPtr.Zero) {
                _native.VariantTypeFree(replyType);
            }

            _native.ObjectUnref(cancellable);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (result == IntPtr.Zero) {
            return false;
        }

        Unref(result);
        return true;
    }

    private IntPtr CreateNotificationParameters(
        LinuxNotificationRequest request,
        NotificationFeature omitted) {
        if (_native is null) {
            return IntPtr.Zero;
        }

        var notificationBuilder = new PortalVariantBuilder(_native, "a{sv}");
        var portable = request.Portable;
        notificationBuilder.AddVariantEntry(
            "title",
            _native.VariantNewString(portable.Title));
        if (portable.Body is { Length: > 0 } body) {
            notificationBuilder.AddVariantEntry(
                "body",
                _native.VariantNewString(body));
        }

        if (portable.Icon is { } icon &&
            !omitted.HasFlag(NotificationFeature.Icon)) {
            AddIcon(notificationBuilder, icon.Source);
        }

        AddActions(notificationBuilder, portable);
        AddPresentation(notificationBuilder, request.Presentation, omitted);
        var notification = notificationBuilder.End();
        var tuple = new PortalVariantBuilder(_native, "(sa{sv})");
        tuple.AddString(portable.Id.ToString());
        tuple.AddValue(notification);
        return _native.VariantRefSink(tuple.End());
    }

    private void AddIcon(PortalVariantBuilder notification, string source) {
        if (_native is null) {
            return;
        }

        if (_version == 1) {
            notification.AddVariantEntry(
                "icon",
                _native.VariantNewString(source));
            return;
        }

        var names = new PortalVariantBuilder(_native, "as");
        names.AddString(source);
        var iconBuilder = new PortalVariantBuilder(_native, "(sv)");
        iconBuilder.AddString("themed");
        iconBuilder.AddValue(
            _native.VariantNewVariant(names.End()));
        notification.AddVariantEntry("icon", iconBuilder.End());
    }

    private void AddActions(
        PortalVariantBuilder notification,
        PortableNotificationRequest request) {
        if (_native is null) {
            return;
        }

        notification.AddVariantEntry(
            "default-action",
            _native.VariantNewString(DefaultAction));
        if (request.Actions.Count == 0) {
            return;
        }

        var buttons = new PortalVariantBuilder(_native, "aa{sv}");
        for (var index = 0; index < request.Actions.Count; index++) {
            var action = request.Actions[index];
            var button = new PortalVariantBuilder(_native, "a{sv}");
            button.AddVariantEntry("label", _native.VariantNewString(action.Label));
            button.AddVariantEntry(
                "action",
                _native.VariantNewString($"{ActionPrefix}{index}"));
            buttons.AddValue(button.End());
        }

        notification.AddVariantEntry("buttons", buttons.End());
    }

    private void AddPresentation(
        PortalVariantBuilder notification,
        LinuxNotificationPresentation presentation,
        NotificationFeature omitted) {
        if (_native is null) {
            return;
        }

        if (presentation.Urgency is { } urgency) {
            notification.AddVariantEntry(
                "priority",
                _native.VariantNewString(urgency switch {
                    LinuxNotificationUrgency.Low => "low",
                    LinuxNotificationUrgency.Critical => "urgent",
                    _ => "normal",
                }));
        }

        if (presentation.Category is { Length: > 0 } category) {
            notification.AddVariantEntry(
                "category",
                _native.VariantNewString(category));
        }

        if (!omitted.HasFlag(NotificationFeature.Sound) &&
            presentation.Sound is { Length: > 0 } sound) {
            notification.AddVariantEntry(
                "sound",
                _native.VariantNewString(sound));
        }
    }

    private IntPtr CreateStringTuple(NotificationId id) {
        if (_native is null) {
            return IntPtr.Zero;
        }

        var tuple = new PortalVariantBuilder(_native, "(s)");
        tuple.AddString(id.ToString());
        return _native.VariantRefSink(tuple.End());
    }

    private NotificationFeature GetOmittedFeatures(
        LinuxNotificationRequest request) {
        var omitted = NotificationFeature.None;
        if (request.Portable.Progress is not null) {
            omitted |= NotificationFeature.Progress;
        }

        if (request.Portable.ExpiresAfter is not null) {
            omitted |= NotificationFeature.Expiry;
        }

        if (request.Presentation.Sound is { Length: > 0 } sound &&
            (_version < 2 ||
             sound is not ("default" or "silent"))) {
            omitted |= NotificationFeature.Sound;
        }

        return omitted;
    }

    private static NotificationFeature GetAppliedFeatures(
        LinuxNotificationRequest request) {
        var portable = request.Portable;
        var features = NotificationFeature.Dismiss |
            NotificationFeature.Replacement;
        if (portable.Body is not null) {
            features |= NotificationFeature.Body;
        }

        if (portable.Icon is not null) {
            features |= NotificationFeature.Icon;
        }

        if (portable.Actions.Count != 0) {
            features |= NotificationFeature.Actions;
        }

        if (request.Presentation.Sound is { Length: > 0 }) {
            features |= NotificationFeature.Sound;
        }

        return features;
    }

    private void HandleActionInvoked(
        IntPtr connection,
        IntPtr senderName,
        IntPtr objectPath,
        IntPtr interfaceName,
        IntPtr signalName,
        IntPtr parameters,
        IntPtr userData) {
        if (userData == IntPtr.Zero ||
            GCHandle.FromIntPtr(userData).Target is not
                DesktopPortalNotifications notifications) {
            return;
        }

        lock (notifications._gate) {
            if (!notifications._disposed) {
                notifications.DispatchActionInvoked(parameters);
            }
        }
    }

    private void DispatchActionInvoked(IntPtr parameters) {
        if (_native is null || parameters == IntPtr.Zero) {
            return;
        }

        var idValue = GetTupleString(parameters, 0);
        var action = GetTupleString(parameters, 1);
        if (!long.TryParse(
                idValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var rawId) ||
            string.IsNullOrEmpty(action)) {
            return;
        }

        var id = new NotificationId(rawId);
        if (!_activeNotifications.Contains(id)) {
            return;
        }

        string? actionId = null;
        var kind = NotificationActivationKind.Body;
        if (action != DefaultAction &&
            (!_actionsByNotification.TryGetValue(id, out var actions) ||
             !actions.TryGetValue(action, out actionId))) {
            return;
        }

        if (action != DefaultAction) {
            kind = NotificationActivationKind.Action;
        }

        _activatedSink(new NotificationActivatedEventArgs {
            Id = id,
            Kind = kind,
            ActionId = actionId,
        });
    }

    private string? GetTupleString(IntPtr tuple, nuint index) {
        if (_native is null) {
            return null;
        }

        var child = _native.VariantGetChildValue(tuple, index);
        try {
            var value = _native.VariantGetString(child, out _);
            return Marshal.PtrToStringUTF8(value);
        } finally {
            _native.VariantUnref(child);
        }
    }

    private static Dictionary<string, string> CreateActionMap(
        PortableNotificationRequest request) {
        var actions = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < request.Actions.Count; index++) {
            actions[$"{ActionPrefix}{index}"] = request.Actions[index].Id;
        }

        return actions;
    }

    private IntPtr CreateType(string? signature) {
        return _native is null || signature is null
            ? IntPtr.Zero
            : _native.VariantTypeNew(signature);
    }

    private void Unref(IntPtr value) {
        if (value != IntPtr.Zero) {
            _native?.VariantUnref(value);
        }
    }

    private static uint ReadVersion(GLibPortalNative native) {
        var parameters = new PortalVariantBuilder(native, "(ss)");
        parameters.AddString(InterfaceName);
        parameters.AddString("version");
        var parameterValue = native.VariantRefSink(parameters.End());
        var replyType = native.VariantTypeNew("(v)");
        try {
            var reply = native.ConnectionCallSync(
                native.Connection,
                "org.freedesktop.portal.Desktop",
                ObjectPath,
                "org.freedesktop.DBus.Properties",
                "Get",
                parameterValue,
                replyType,
                flags: 0,
                timeoutMilliseconds: 1000,
                IntPtr.Zero,
                out var error);
            if (error != IntPtr.Zero) {
                native.ErrorFree(error);
            }

            if (reply == IntPtr.Zero) {
                return 0;
            }

            try {
                var wrapped = native.VariantGetChildValue(reply, 0);
                try {
                    var value = native.VariantGetVariant(wrapped);
                    try {
                        return native.VariantGetUInt32(value);
                    } finally {
                        native.VariantUnref(value);
                    }
                } finally {
                    native.VariantUnref(wrapped);
                }
            } finally {
                native.VariantUnref(reply);
            }
        } finally {
            native.VariantUnref(parameterValue);
            native.VariantTypeFree(replyType);
        }
    }

    private static NotificationCapabilities CreateCapabilities(uint version) {
        var features = NotificationFeature.Actions |
            NotificationFeature.Body |
            NotificationFeature.Dismiss |
            NotificationFeature.Icon |
            NotificationFeature.Replacement;
        if (version >= 2) {
            features |= NotificationFeature.Sound;
        }

        return new NotificationCapabilities {
            CanSubmit = true,
            Features = features,
            Limitation = "The portal does not expose notification progress or application-controlled expiry.",
        };
    }

    private static NotificationCapabilities UnavailableCapabilities(
        string limitation) {
        return new NotificationCapabilities {
            CanSubmit = false,
            Features = NotificationFeature.None,
            Limitation = limitation,
        };
    }
}
