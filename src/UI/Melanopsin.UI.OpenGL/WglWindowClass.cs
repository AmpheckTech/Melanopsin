// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Melanopsin.UI.OpenGL;

internal static class WglWindowClass {
    private static readonly object RegistrationLock = new();
    private static readonly OpenGLNative.WindowProcedure WindowProcedure =
        static (window, message, wParam, lParam) =>
            OpenGLNative.DefWindowProcW(window, message, wParam, lParam);
    private static IntPtr _instance;
    private static string? _className;

    public static IntPtr CreateWindow() {
        EnsureRegistered();
        var handle = OpenGLNative.CreateWindowExW(
            extendedStyle: 0,
            _className!,
            "Melanopsin OpenGL surface",
            OpenGLNative.WsPopup,
            x: 0,
            y: 0,
            width: 1,
            height: 1,
            parent: IntPtr.Zero,
            menu: IntPtr.Zero,
            _instance,
            parameter: IntPtr.Zero);
        if (handle == IntPtr.Zero) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "CreateWindowExW failed.");
        }

        return handle;
    }

    private static void EnsureRegistered() {
        if (_className is not null) {
            return;
        }

        lock (RegistrationLock) {
            if (_className is not null) {
                return;
            }

            _instance = OpenGLNative.GetModuleHandleW(null);
            if (_instance == IntPtr.Zero) {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "GetModuleHandleW failed.");
            }

            var className = $"Melanopsin.OpenGL.{Environment.ProcessId}";
            var windowClass = new WndClassEx {
                Size = (uint)Marshal.SizeOf<WndClassEx>(),
                Instance = _instance,
                WindowProcedure = Marshal.GetFunctionPointerForDelegate(WindowProcedure),
                Background = new IntPtr(1),
                ClassName = className,
            };
            var atom = OpenGLNative.RegisterClassExW(ref windowClass);
            if (atom == 0) {
                var error = Marshal.GetLastWin32Error();
                if (error != 1410) {
                    throw new Win32Exception(error, "RegisterClassExW failed.");
                }
            }

            _className = className;
        }
    }
}
