// The contract every screen follows. (Abstraction)

using System;
using System.Windows.Forms;

namespace CodeQuest.UI;

public interface IModulePanel
{
    void RefreshData();
}

public interface ILeaveGuard
{
    bool ConfirmLeave();
}

public abstract class ModulePanel : UserControl, IModulePanel
{
    public event EventHandler<StatusEventArgs>? StatusRequested;

    public abstract void RefreshData();

    protected void ReportStatus(string message, bool success)
        => StatusRequested?.Invoke(this, new StatusEventArgs(message, success));
}

public sealed class StatusEventArgs : EventArgs
{
    public StatusEventArgs(string message, bool success)
    {
        Message = message;
        Success = success;
    }

    public string Message { get; }

    public bool Success { get; }
}
