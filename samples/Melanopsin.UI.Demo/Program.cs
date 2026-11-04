using Melanopsin.Graphics.Backend.Skia;
using Melanopsin.Graphics.Backend.RHI;
using Melanopsin.UI.Demo;
using Melanopsin.UI.Demo.Views;
using Melanopsin.UI.Desktop;

using var application = Application.Default(new ApplicationOptions {
    BackendFactory = static _ => new SkiaRenderBackend(),
    ConfigureStyles = AppStyles.AddTo,
}).EnableDevTools();
application.Run(new MainView(application.Clipboard, application.Notifications));
