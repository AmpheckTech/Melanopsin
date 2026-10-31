using Melanopsin.Graphics;
using Melanopsin.Graphics.Backend.RHI;
using Melanopsin.Graphics.Backend.Skia;
using Melanopsin.Graphics.Text;
using Melanopsin.Graphics.Text.Fonts;
using Melanopsin.UI.App;
using Melanopsin.UI.Demo.Views;
using Melanopsin.UI.Hosting;
using Melanopsin.UI.Input;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Platform.Wayland;
using Melanopsin.UI.Platform.Win32;
using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Demo;

internal static class Program {
    public static void Main() {
        using IPlatformWindowHost platform = OperatingSystem.IsWindows()
            ? new Win32PlatformHost()
            : new WaylandPlatformHost();

        var font = FontFace.Load(FindFont());
        using var textShaper = new HarfBuzzTextShaper(
            new FontCollection(font));
        var metrics = new UiMetrics(
            platform.DisplayScale,
            viewportWidth: 820,
            viewportHeight: 720);
        var styleSheet = MaterialStyleSheet.Create();
        styleSheet.AddRange(StyleSheetParser.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "Styles",
                "App.css"))).Rules);

        using var windows = new WindowManager(
            new RHIRenderBackend(),
            platform,
            new StyleEngine(styleSheet, metrics),
            platformHost: platform,
            displayScale: platform.DisplayScale,
            layoutEngineFactory: styles =>
                new FlowLayoutEngine(styles, textShaper, metrics),
            sceneComposerFactory: styles =>
                new DefaultRenderSceneComposer(styles, textShaper));
        using var runtime = new UiRuntime();
        var window = windows.CreateWindow(new WindowOptions {
            Title = "Melanopsin UI Demo",
            Size = new PixelSize(820, 720),
        });
        window.Root = new OverlayHost {
            Content = new ComponentHost(new MainView(), runtime),
        };

        var renderOptions = new RenderOptions {
            ClearColor = new Color(244, 247, 245),
            ScaleFactor = platform.DisplayScale,
        };
        using var application = new Application(
            windows,
            platform,
            renderOptions,
            new InputManager(windows),
            new FocusManager(windows),
            runtime: runtime);
        application.Run();
    }

    private static string FindFont() {
        var configured = Environment.GetEnvironmentVariable("MELANOPSIN_FONT");
        if (!string.IsNullOrWhiteSpace(configured)) {
            return configured;
        }

        string[] candidates = OperatingSystem.IsWindows()
            ? [
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Fonts",
                    "segoeui.ttf"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Fonts",
                    "arial.ttf"),
            ]
            : [
                "/usr/share/fonts/TTF/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            ];
        return candidates.FirstOrDefault(File.Exists) ??
            throw new FileNotFoundException(
                "Set MELANOPSIN_FONT to a TrueType font file.");
    }
}
