using System.Diagnostics;
using System.Windows;

namespace SMTAlert
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (s, args) =>
            {
                Debug.WriteLine($"Unhandled exception: {args.Exception}");
                args.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                Debug.WriteLine($"Unobserved task exception: {args.Exception}");
                args.SetObserved();
            };
        }
    }
}
