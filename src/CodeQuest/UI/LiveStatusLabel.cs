// The status line that screen readers announce.

using System.Windows.Forms;
using System.Windows.Forms.Automation;

namespace CodeQuest.UI;

public sealed class LiveStatusLabel : Label
{
    public LiveStatusLabel()
    {
        AccessibleName = "Status";
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);

        if (Text.Length == 0)
        {
            return;
        }

        _ = AccessibilityObject.RaiseAutomationNotification(
            AutomationNotificationKind.Other,
            AutomationNotificationProcessing.ImportantMostRecent,
            Text);
    }
}
