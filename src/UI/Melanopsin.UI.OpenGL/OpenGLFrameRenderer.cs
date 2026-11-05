// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.OpenGL;

internal static class OpenGLFrameRenderer {
    public static ReadOnlyMemory<byte> Render(
        int width,
        int height,
        Color clearColor,
        Action<int, int>? content,
        uint readBuffer,
        ref byte[] readback,
        ref byte[] pixels) {
        var rowBytes = checked(width * 4);
        var length = checked(rowBytes * height);
        if (readback.Length != length) {
            readback = new byte[length];
            pixels = new byte[length];
        }

        OpenGLNative.glViewport(0, 0, width, height);
        OpenGLNative.glClearColor(
            clearColor.Red / 255f,
            clearColor.Green / 255f,
            clearColor.Blue / 255f,
            clearColor.Alpha / 255f);
        OpenGLNative.glClear(OpenGLNative.GlColorBufferBit);
        if (content is not null) {
            content(width, height);
        } else {
            DrawDefaultContent(width, height);
        }

        OpenGLNative.glFinish();
        ReadPixels(width, height, readBuffer, readback, pixels, rowBytes);
        return pixels;
    }

    private static void DrawDefaultContent(int width, int height) {
        OpenGLNative.glDisable(OpenGLNative.GlDepthTest);
        OpenGLNative.glMatrixMode(OpenGLNative.GlProjection);
        OpenGLNative.glLoadIdentity();
        OpenGLNative.glOrtho(0, width, height, 0, -1, 1);
        OpenGLNative.glMatrixMode(OpenGLNative.GlModelView);
        OpenGLNative.glLoadIdentity();

        OpenGLNative.glBegin(OpenGLNative.GlTriangles);
        OpenGLNative.glColor4f(0.12f, 0.72f, 0.76f, 1);
        OpenGLNative.glVertex2f(width * 0.08f, height * 0.82f);
        OpenGLNative.glColor4f(0.35f, 0.36f, 0.95f, 1);
        OpenGLNative.glVertex2f(width * 0.48f, height * 0.16f);
        OpenGLNative.glColor4f(0.95f, 0.45f, 0.35f, 1);
        OpenGLNative.glVertex2f(width * 0.68f, height * 0.78f);
        OpenGLNative.glEnd();

        OpenGLNative.glBegin(OpenGLNative.GlLines);
        OpenGLNative.glColor4f(1, 1, 1, 0.18f);
        for (var index = 1; index < 6; index++) {
            var y = height * index / 6f;
            OpenGLNative.glVertex2f(0, y);
            OpenGLNative.glVertex2f(width, y);
        }

        OpenGLNative.glEnd();
    }

    private static unsafe void ReadPixels(
        int width,
        int height,
        uint readBuffer,
        byte[] readback,
        byte[] pixels,
        int rowBytes) {
        OpenGLNative.glReadBuffer(readBuffer);
        OpenGLNative.glPixelStorei(OpenGLNative.GlPackAlignment, 4);
        fixed (byte* destination = readback) {
            OpenGLNative.glReadPixels(
                x: 0,
                y: 0,
                width,
                height,
                OpenGLNative.GlBgra,
                OpenGLNative.GlUnsignedByte,
                (IntPtr)destination);
        }

        for (var row = 0; row < height; row++) {
            readback.AsSpan(
                    (height - 1 - row) * rowBytes,
                    rowBytes)
                .CopyTo(pixels.AsSpan(row * rowBytes, rowBytes));
        }
    }
}
