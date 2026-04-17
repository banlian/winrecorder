using System.Threading.Tasks;
using WinRecorder.Logging;

namespace WinRecorder;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            ErrorLog.Write("Application.ThreadException", e.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                ErrorLog.Write("AppDomain.UnhandledException", ex);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ErrorLog.Write("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        Application.Run(new App.AppContext());
    }    
}