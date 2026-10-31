using Melanopsin.Graphics;
using Melanopsin.Graphics.Backend.RHI;
using Melanopsin.Graphics.Backend.Skia;
using Melanopsin.Graphics.Text;
using Melanopsin.Graphics.Text.Fonts;
using Melanopsin.UI.App;
using Melanopsin.UI.Demo;
using Melanopsin.UI.Demo.Views;
using Melanopsin.UI.Desktop;

using var application = Application.Default(AppStyles.AddTo).EnableDevTools();
application.Run<MainView>();
