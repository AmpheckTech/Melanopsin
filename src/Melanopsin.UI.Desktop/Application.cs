using Melanopsin.Graphics.Backend.Skia;
using Melanopsin.UI.App;
using Melanopsin.UI.DevTools;
using Melanopsin.UI.Input;
using Melanopsin.UI.Platform.Wayland;
using Melanopsin.UI.Platform.Win32;

namespace Melanopsin.UI.Desktop;

public sealed class Application : IDisposable
{
    private readonly IPlatformWindowHost _platform;
    private readonly HarfBuzzTextShaper _textShaper;
    private readonly WindowManager _windows;
    private readonly global::Melanopsin.UI.Hosting.Application _host;
    private DevToolsController? _devTools;
    private Window? _window;
    private bool _disposed;

    private Application(
        IPlatformWindowHost platform,
        HarfBuzzTextShaper textShaper,
        WindowManager windows,
        global::Melanopsin.UI.Hosting.Application host,
        StyleSheet styleSheet)
    {
        _platform = platform;
        _textShaper = textShaper;
        _windows = windows;
        _host = host;
        Styles = styleSheet;
    }

    public StyleSheet Styles { get; }

    public WindowManager Windows => _windows;

    public UiRuntime Runtime => _host.Runtime;

    public IPlatformClipboard? Clipboard => _platform.Clipboard;

    public IPlatformNotifications? Notifications => _platform.Notifications;

    public static Application Default(
        Action<StyleSheet>? configureStyles = null) {
        return Default(new ApplicationOptions {
            ConfigureStyles = configureStyles,
        });
    }

    public static Application Default(ApplicationOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        var primaryFont = SystemFonts.LoadPrimary();
        var fallbackFonts = SystemFonts.LoadFallbacks()
            .Select(static font => FontFace.Load(font.Path, font.FaceIndex))
            .ToArray();
        return Create(
            primaryFont,
            fallbackFonts,
            options.ConfigureStyles,
            options.BackendFactory,
            SkiaRasterCacheOptions.Default,
            activeFrameRate: 60,
            idleFrameRate: 15,
            options.ViewportWidth,
            options.ViewportHeight);
    }

    /// <summary>
    /// Creates an application preset for memory-constrained devices. Fonts are
    /// limited to the supplied primary file and explicitly configured fallback
    /// files; host fallback fonts are never discovered implicitly.
    /// </summary>
    public static Application Embedded(
        string primaryFontPath,
        Action<StyleSheet>? configureStyles = null,
        Action<EmbeddedOptions>? configure = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryFontPath);
        return Embedded(new FontAsset(primaryFontPath), configureStyles, configure);
    }

    public static Application Embedded(
        FontAsset primaryFont,
        Action<StyleSheet>? configureStyles = null,
        Action<EmbeddedOptions>? configure = null) {
        var options = new EmbeddedOptions();
        configure?.Invoke(options);
        Validate(options);

        var primaryFace = FontFace.Load(primaryFont.Path, primaryFont.FaceIndex);
        var fallbackFonts = options.FallbackFonts
            .Select(static font => FontFace.Load(font.Path, font.FaceIndex))
            .ToArray();
        var rasterCacheOptions = new SkiaRasterCacheOptions(
            byteLimit: options.RasterCacheByteLimit,
            entryLimit: options.RasterCacheEntryLimit,
            entryByteLimit: Math.Min(
                options.RasterCacheByteLimit,
                SkiaRasterCacheOptions.Default.EntryByteLimit));
        return Create(
            primaryFace,
            fallbackFonts,
            configureStyles,
            backendFactory: null,
            rasterCacheOptions,
            options.ActiveFrameRate,
            options.IdleFrameRate,
            viewportWidth: 1280,
            viewportHeight: 720);
    }

    private static Application Create(
        FontFace primaryFont,
        IReadOnlyList<FontFace> fallbackFonts,
        Action<StyleSheet>? configureStyles,
        Func<FontFace, IRenderBackend>? backendFactory,
        SkiaRasterCacheOptions rasterCacheOptions,
        int activeFrameRate,
        int idleFrameRate,
        int viewportWidth,
        int viewportHeight) {
        IPlatformWindowHost? platform = null;
        HarfBuzzTextShaper? textShaper = null;
        WindowManager? windows = null;
        UiRuntime? runtime = null;
        global::Melanopsin.UI.Hosting.Application? host = null;
        IRenderBackend? backend = null;
        try
        {
            platform = OperatingSystem.IsWindows()
                ? new Win32PlatformHost()
                : new WaylandPlatformHost();
            var fonts = new FontCollection(primaryFont);
            foreach (var fallbackFont in fallbackFonts) {
                fonts.AddFallback(fallbackFont);
            }

            textShaper = new HarfBuzzTextShaper(fonts);
            var metrics = new UiMetrics(
                platform.DisplayScale,
                viewportWidth,
                viewportHeight);
            var styleSheet = new StyleSheet();
            configureStyles?.Invoke(styleSheet);
            backend = backendFactory?.Invoke(primaryFont) ??
                new SkiaRenderBackend(primaryFont, rasterCacheOptions);
            windows = new WindowManager(
                backend,
                platform,
                new StyleEngine(styleSheet, metrics),
                platformHost: platform,
                displayScale: platform.DisplayScale,
                layoutEngineFactory: styles =>
                    new FlowLayoutEngine(styles, textShaper, metrics),
                sceneComposerFactory: styles =>
                    new DefaultRenderSceneComposer(styles, textShaper));
            runtime = new UiRuntime();
            host = new global::Melanopsin.UI.Hosting.Application(
                windows,
                platform,
                RenderOptions.Default,
                new InputManager(windows),
                new FocusManager(windows),
                runtime: runtime);
            host.ActiveFrameRate = activeFrameRate;
            host.IdleFrameRate = idleFrameRate;
            return new Application(platform, textShaper, windows, host, styleSheet);
        }
        catch
        {
            if (host is not null)
            {
                host.Dispose();
            }
            else
            {
                runtime?.Dispose();
            }

            if (windows is not null)
            {
                windows.Dispose();
            }
            else
            {
                backend?.Dispose();
            }

            textShaper?.Dispose();
            platform?.Dispose();
            throw;
        }
    }

    private static void Validate(EmbeddedOptions options) {
        if (options.ActiveFrameRate is < 1 or > 240) {
            throw new ArgumentOutOfRangeException(
                nameof(options.ActiveFrameRate),
                "The active frame rate must be between 1 and 240.");
        }

        if (options.IdleFrameRate is < 1 or > 240) {
            throw new ArgumentOutOfRangeException(
                nameof(options.IdleFrameRate),
                "The idle frame rate must be between 1 and 240.");
        }

        if (options.RasterCacheByteLimit <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(options.RasterCacheByteLimit),
                "The raster cache limit must be positive.");
        }

        if (options.RasterCacheEntryLimit <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(options.RasterCacheEntryLimit),
                "The raster cache entry limit must be positive.");
        }
    }

    public Application EnableDevTools() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_devTools is null)
        {
            _devTools = new DevToolsController(_host);
            _host.PreviewKeyDown += OnPreviewKeyDown;
        }

        return this;
    }

    public void Run<TComponent>() where TComponent : Component, new()
    {
        Run(new TComponent());
    }

    public void Run<TComponent>(WindowOptions options)
        where TComponent : Component, new()
    {
        Run(new TComponent(), options);
    }

    public void Run(Component root, WindowOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_window is not null)
        {
            throw new InvalidOperationException(
                "This application already has a root window.");
        }

        _window = _windows.CreateWindow(options ?? new WindowOptions
        {
            Title = root.GetType().Name,
            Size = new PixelSize(1280, 720),
        });
        _window.Root = new OverlayHost
        {
            Content = new ComponentHost(root, Runtime),
        };
        _host.Run();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_devTools is not null)
        {
            _host.PreviewKeyDown -= OnPreviewKeyDown;
            _devTools.Dispose();
        }

        _host.Dispose();
        _windows.Dispose();
        _textShaper.Dispose();
        _platform.Dispose();
    }

    private void OnPreviewKeyDown(KeyInputEvent input)
    {
        if (input.Key == Key.F12)
        {
            _devTools!.Toggle();
        }
    }
}
