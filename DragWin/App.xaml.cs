using Microsoft.Toolkit.Uwp.Notifications;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;

namespace DragWin
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ToastNotificationManagerCompat.OnActivated += ToastActivatedHandler;
        }
        private void ToastActivatedHandler(ToastNotificationActivatedEventArgsCompat toastArgs)
        {
            var args = ToastArguments.Parse(toastArgs.Argument);
            Current.Dispatcher.Invoke(() =>
            {
                if (args.Contains("action") && args["action"] == "install_update")
                {
                     Updater.InstallUpdate(); 
                }

            });
        }
    }
}