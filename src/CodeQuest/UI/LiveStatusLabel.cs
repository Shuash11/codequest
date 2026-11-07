// =============================================================================
// CodeQuest — Validation & Error-Handling Sweep (I5): the status strip label
// with the live-region contract the P8 accessibility review asked for — the
// strip was invisible to assistive technology, so success and blocked
// verdicts never reached AT users.
// Look at: the accessible object — role StatusBar-equivalent text with the
//          Polite live setting, so a verdict announced through ShowStatus is
//          spoken without stealing focus (the live-region equivalent WinForms
//          offers). The accessible name equals the visible purpose ("Status").
// =============================================================================

using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The shell's status strip label: a verdict line announced to assistive
/// technology as a polite live region (the I5 accessibility fix).
/// </summary>
public sealed class LiveStatusLabel : Label
{
    /// <summary>Creates the live status label.</summary>
    public LiveStatusLabel()
    {
        AccessibleName = "Status"; // the label's purpose, announced with the verdict
    }

    /// <summary>The accessible object carries the polite live setting.</summary>
    protected override AccessibleObject CreateAccessibilityInstance() => new LiveStatusLabelAccessibleObject(this);

    /// <summary>Polite live region: verdicts are announced without focus changes.</summary>
    private sealed class LiveStatusLabelAccessibleObject : Control.ControlAccessibleObject
    {
        public LiveStatusLabelAccessibleObject(LiveStatusLabel owner) : base(owner)
        {
        }

        /// <summary>Polite: announce when idle, never interrupt the user.</summary>
        public override System.Windows.Forms.Automation.AutomationLiveSetting LiveSetting
            => System.Windows.Forms.Automation.AutomationLiveSetting.Polite;
    }
}
