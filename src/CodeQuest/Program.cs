// Starts the app: sign-in, then the main window.

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using CodeQuest.UI;

namespace CodeQuest;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        AppConfig config = AppConfig.Load();

        UITheme.Apply();

        Application.ThreadException += OnThreadException;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        Db db = new();
        if (!File.Exists(db.DatabasePath))
        {
            DialogForm.ShowStartupBlocker();
            return;
        }

        DatabaseMigrator.EnsureSchema(db);

        var users = new UserRepository(db);

        var sessions = new SessionStore();
        string? prefillName = null;

        while (true)
        {
            UserAccount? signedIn = null;
            string? remembered = sessions.LoadUsername();

            if (remembered is not null)
            {
                // A database problem at startup falls back to the name
                // screen: no message loop exists yet, so an exception here
                // would end the process instead of showing the §4.11 dialog
                // (security review F7).
                try
                {
                    signedIn = users.GetByUsername(remembered);
                }
                catch (DataAccessException)
                {
                    signedIn = null;
                }
            }

            if (signedIn is null)
            {
                using (LoginForm login = new(users, remembered ?? prefillName))
                {
                    if (login.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }

                    signedIn = login.AuthenticatedUser;
                }

                if (signedIn is null)
                {
                    return;
                }

                sessions.SaveUsername(signedIn.Username);
            }

            bool backToLogin;
            using (MainForm shell = new(signedIn, db, config))
            {
                Application.Run(shell);
                backToLogin = shell.ReturnedToLogin;
            }

            if (!backToLogin)
            {
                return;
            }

            prefillName = signedIn.Username;
            sessions.Clear();
        }
    }

    private static void OnThreadException(object? sender, ThreadExceptionEventArgs e)
    {
        AppendErrorLog(e.Exception);
        if (!DialogForm.ShowUnexpectedError())
        {
            Application.Exit();
        }
    }

    private static void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            AppendErrorLog(exception);
        }

        try
        {
            _ = DialogForm.ShowUnexpectedError();
        }
        catch (Exception dialogFailure)
        {
            _ = dialogFailure;
        }

        Environment.Exit(1);
    }

    private static void AppendErrorLog(Exception exception)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodeQuest");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "error.log"),
                $"===== {DateTimeOffset.UtcNow:yyyy-MM-dd'T'HH:mm:ss'Z'} ====={Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
