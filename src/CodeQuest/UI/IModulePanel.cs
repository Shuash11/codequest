// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// The contract between the shell's module host and a real module panel
// (spec 5.2: "Panels persist (show/hide, not recreate)"). The P7 placeholders
// are plain panels; the P8 screens extend <see cref="ModulePanel"/> so the
// shell can refresh their data from the database each time they become
// visible, and so they can report to the shell's status strip (§5 anatomy,
// §4.11 — success and failure feedback is an inline strip line, never a
// dialog).
// =============================================================================

using System;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>A module panel that reloads its data when the shell shows it.</summary>
public interface IModulePanel
{
    /// <summary>Reloads the panel's rows from the database (keeps control state).</summary>
    void RefreshData();
}

/// <summary>
/// The base of every real module screen: a persisted, shell-hosted panel
/// (§5.2) that reports feedback lines to the shell's status strip.
/// </summary>
public abstract class ModulePanel : UserControl, IModulePanel
{
    /// <summary>The feedback this panel asks the shell to show in the status strip.</summary>
    public event EventHandler<StatusEventArgs>? StatusRequested;

    /// <summary>Reloads the panel's rows from the database (keeps control state).</summary>
    public abstract void RefreshData();

    /// <summary>Reports one feedback line to the shell's status strip.</summary>
    /// <param name="message">The strip text (the design of record's copy).</param>
    /// <param name="success">True for success lines (auto-clear, §4.11), false for blocked/failure lines.</param>
    protected void ReportStatus(string message, bool success)
        => StatusRequested?.Invoke(this, new StatusEventArgs(message, success));
}

/// <summary>The feedback a module panel asks the shell to put in the status strip.</summary>
public sealed class StatusEventArgs : EventArgs
{
    /// <summary>Creates the feedback line.</summary>
    /// <param name="message">The strip text (the design of record's copy).</param>
    /// <param name="success">True for success lines (auto-clear after ~4s, §4.11), false for blocked/failure lines.</param>
    public StatusEventArgs(string message, bool success)
    {
        Message = message;
        Success = success;
    }

    /// <summary>The strip text.</summary>
    public string Message { get; }

    /// <summary>Whether the line is success feedback (auto-clears) or failure feedback (stays).</summary>
    public bool Success { get; }
}
