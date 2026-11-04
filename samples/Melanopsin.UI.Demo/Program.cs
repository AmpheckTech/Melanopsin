using Melanopsin.UI.Demo;
using Melanopsin.UI.Demo.Views;
using Melanopsin.UI.Desktop;

using var application = Application.Default(AppStyles.AddTo).EnableDevTools();
application.Run(new MainView(application.Clipboard, application.Notifications));
