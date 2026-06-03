using ETTerms.App;
using ETTerms.Infrastructure;

namespace ETTerms;

static class Program
{
    /// <summary>The main entry point for the application.</summary>
    [STAThread]
    static void Main()
    {
        AppLogger.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            AppLogger.LogError("Unhandled UI exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLogger.LogError("Unhandled domain exception", e.ExceptionObject as Exception);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
