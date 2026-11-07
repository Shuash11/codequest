// =============================================================================
// CodeQuest — Validation & Error-Handling Sweep (I5): the status strip label
// with the live-region contract the P8 accessibility review asked for — the
// strip was invisible to assistive technology, so success and blocked
// verdicts never reached AT users.
// Look at: OnTextChanged raises a UIA automation notification with the new
//          verdict (ImportantMostRecent — announced without stealing focus),
//          which is the live-region mechanism WinForms actually exposes. The
//          accessible name equals the visible purpose ("Status").
// =============================================================================

using System.Windows.Forms;
using System.Windows.Forms.Automation;

namespace CodeQuest.UI;

/// <summary>
/// The shell's status strip label: a verdict line announced to assistive
/// technology as an automation notification (the I5 accessibility fix).
/// </summary>
public sealed class LiveStatusLabel : Label
{
    /// <summary>Creates the live status label.</summary>
    public LiveStatusLabel()
    {
        AccessibleName = "Status"; // the label's purpose, announced with the verdict
    }

    /// <summary>Announces every new verdict to assistive technology.</summary>
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);

        if (Text.Length == 0)
        {
            return; // a cleared strip is not a verdict worth announcing
        }

        // The live-region mechanism WinForms exposes: ImportantMostRecent
        // announces without stealing focus, replacing a pending announcement.
        _ = AccessibilityObject.RaiseAutomationNotification(
            AutomationNotificationKind.Other,
            AutomationNotificationProcessing.ImportantMostRecent,
            Text);
    }
}
