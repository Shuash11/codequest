// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation
// The application entry point with the plan's bootstrap order:
// config (AppConfig, P5) → theme (UITheme) → login (LoginForm) → shell
// (MainForm on success). The login↔shell pair runs in a loop, so "Log out"
// (spec 5.2) returns to the login screen and closing the login ends the app.
// Look at: ShowStartupBlockerBeforeLogin — the ONE startup dialog of spec
//          4.11: a missing database file gets the friendly card, never a
//          stack trace. OnThreadException / OnDomainUnhandledException — the
//          §4.11 unexpected-error dialog (plain language + retry hint), so an
//          unhandled exception never reaches the user as a raw crash.
// =============================================================================

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using CodeQuest.UI;

namespace CodeQuest;

/// <summary>The application entry point.</summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Bootstrap order per plan P7: config → theme → login → shell. The
        // config is loaded once at startup (a missing or malformed file degrades
        // to "no providers configured" — it never throws); the generation chain
        // (I4) consumes exactly this instance (contract 8).
        AppConfig config = AppConfig.Load();

        UITheme.Apply();

        // §4.11: unexpected exceptions surface as one friendly dialog — never
        // a raw crash, never a stack trace.
        Application.ThreadException += OnThreadException;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        Db db = new();
        if (!File.Exists(db.DatabasePath))
        {
            DialogForm.ShowStartupBlocker();
            return;
        }

        // Gamification (plan WP1/A7): the achievements table is added to
        // existing databases by an idempotent startup migration — it never
        // touches the existing rows, so it is safe on every launch.
        DatabaseMigrator.EnsureSchema(db);

        var users = new UserRepository(db);

        // Login → shell → (logout →) login, until the user leaves the login.
        while (true)
        {
            UserAccount? signedIn;
            using (LoginForm login = new(users))
            {
                if (login.ShowDialog() != DialogResult.OK)
                {
                    return; // the login window was closed — the app ends
                }

                signedIn = login.AuthenticatedUser;
            }

            if (signedIn is null)
            {
                return; // unreachable: OK always carries an account (defensive)
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
        }
    }

    /// <summary>
    /// A UI-thread exception: the §4.11 unexpected dialog. "Try again" returns
    /// to the running app; "Close" ends it. The exception is appended to the
    /// local error log first (type + message + time — never key material, per
    /// the I5 logging obligation), so a crash after a demo still leaves a clue.
    /// </summary>
    private static void OnThreadException(object? sender, ThreadExceptionEventArgs e)
    {
        AppendErrorLog(e.Exception);
        if (!DialogForm.ShowUnexpectedError())
        {
            Application.Exit();
        }
    }

    /// <summary>
    /// A non-UI-thread exception: the process is ending regardless, so the
    /// dialog's job is the friendly message (no stack trace) before the exit.
    /// The exception lands in the local error log the same way.
    /// </summary>
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
            // The dialog itself failed while the process was already dying —
            // nothing further can be shown; exit without a raw crash dialog.
            _ = dialogFailure;
        }

        Environment.Exit(1);
    }

    /// <summary>
    /// Appends one exception report to %LOCALAPPDATA%\CodeQuest\error.log —
    /// the I5 logging hook. The full exception text (type, message, stack,
    /// inner exceptions) is what turns a one-line mystery into a fixable
    /// bug report; the key never appears in any of it (the key travels only
    /// in request headers). A logging failure is swallowed: logging must
    /// never turn a handled failure into a crash.
    /// </summary>
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
            // Logging is best-effort by design (I5): a full disk or a locked
            // file must not replace the original failure with a new one.
        }
    }
}
