using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// Placeholder window used only so the Phase P0 scaffold builds and launches.
/// The real navigation shell is implemented in Phase P7.
/// </summary>
public sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "CodeQuest";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 240);

        var placeholder = new Label
        {
            Dock = DockStyle.Fill,
            Text = "CodeQuest 0.1.0 - scaffold placeholder.",
            TextAlign = ContentAlignment.MiddleCenter,
        };

        Controls.Add(placeholder);
    }
}
