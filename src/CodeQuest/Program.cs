using System.Windows.Forms;

namespace CodeQuest;

/// <summary>
/// Application entry point. Bootstrap order is finalized in Phase P7
/// (config -> theme -> login -> shell); Phase P0 only proves the
/// WinForms pipeline starts.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new UI.MainForm());
    }
}
