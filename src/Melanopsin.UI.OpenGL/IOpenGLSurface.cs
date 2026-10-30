namespace Melanopsin.UI.OpenGL;

internal interface IOpenGLSurface : IDisposable {
    ReadOnlyMemory<byte> Render(
        int width,
        int height,
        Color clearColor,
        Action<int, int>? content);
}
