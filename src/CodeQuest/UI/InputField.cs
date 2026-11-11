// A text field with an error line.

using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class InputField : Panel
{
    private readonly TextBox _box = new();
    private readonly Label _errorLine = new();
    private readonly string? _fallbackAccessibleName;
    private Control? _labeledBy;
    private bool _borderError;

    public InputField(bool reserveErrorLine = false, string? accessibleName = null)
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);

        BackColor = UITheme.Surface;
        ReserveErrorLine = reserveErrorLine;
        _fallbackAccessibleName = accessibleName;

        _box.AutoSize = false;
        _box.BorderStyle = BorderStyle.None;
        _box.Font = UITheme.Body;
        _box.BackColor = UITheme.Surface;
        _box.ForeColor = UITheme.TextPrimary;
        _box.AccessibleName = accessibleName;
        _box.GotFocus += static (sender, e) => RepaintOwner(sender);
        _box.LostFocus += static (sender, e) => RepaintOwner(sender);
        _box.TextChanged += static (sender, e) => ForwardTextChanged(sender);
        Controls.Add(_box);

        _errorLine.AutoSize = true;
        _errorLine.Font = UITheme.Caption;
        _errorLine.BackColor = UITheme.Surface;
        Controls.Add(_errorLine);

        LayoutField();
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        _errorLine.BackColor = UITheme.ContainerSurface(this);
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UITheme.ContainerSurface(this));
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Control? LabeledBy
    {
        get => _labeledBy;
        set
        {
            _labeledBy = value;
            _box.AccessibleName = value?.Text ?? _fallbackAccessibleName;
        }
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _box.Text;
        set => _box.Text = value;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string PlaceholderText
    {
        get => _box.PlaceholderText;
        set => _box.PlaceholderText = value;
    }

    public void SetPasswordMask(bool masked)
    {
        _box.UseSystemPasswordChar = masked;
    }

    public bool HasError => _borderError;

    public bool ReserveErrorLine { get; }

    public void ShowError(string message)
    {
        _borderError = true;
        _errorLine.ForeColor = UITheme.DangerText;
        _errorLine.Text = message;
        Invalidate();
    }

    public void ShowNote(string message)
    {
        _errorLine.ForeColor = UITheme.TextSecondary;
        _errorLine.Text = message;
        Invalidate();
    }

    public void ClearError()
    {
        _borderError = false;
        _errorLine.Text = string.Empty;
        Invalidate();
    }

    public void ClearText()
    {
        _box.Text = string.Empty;
    }

    public void FocusInput()
    {
        _box.Focus();
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        LayoutField();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        LayoutField();
    }

    protected override void OnEnabledChanged(EventArgs eventargs)
    {
        base.OnEnabledChanged(eventargs);

        _box.BackColor = Enabled ? UITheme.Surface : UITheme.Fill;
        _box.ForeColor = Enabled ? UITheme.TextPrimary : UITheme.TextTertiary;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        System.Drawing.Rectangle bounds = new(0, 0, Width - 1, FieldHeight - 1);
        using (var fieldPath = UITheme.RoundedPath(bounds, UITheme.Scaled(this, UITheme.RadiusControl)))
        using (var fieldBrush = new System.Drawing.SolidBrush(Enabled ? UITheme.Surface : UITheme.Fill))
        {
            e.Graphics.FillPath(fieldBrush, fieldPath);
        }

        if (!Enabled)
        {
            return;
        }

        Color borderColor = _borderError ? UITheme.DangerText
            : _box.Focused ? UITheme.AccentText
            : UITheme.BorderControl;

        using var borderPath = UITheme.RoundedPath(bounds, UITheme.Scaled(this, UITheme.RadiusControl));
        e.Graphics.DrawPath(new System.Drawing.Pen(borderColor), borderPath);
    }

    private int FieldHeight => UITheme.Scaled(this, 32);

    private int TotalHeight => FieldHeight
        + (ReserveErrorLine || _errorLine.Text.Length > 0
            ? UITheme.Scaled(this, UITheme.Spacing8) + LineHeight
            : 0);

    private int LineHeight => _errorLine.GetPreferredSize(System.Drawing.Size.Empty).Height;

    private void LayoutField()
    {
        int horizontalPadding = UITheme.Scaled(this, UITheme.Spacing12);
        int boxHeight = FieldHeight - UITheme.Scaled(this, UITheme.Spacing8);
        _box.SetBounds(
            horizontalPadding,
            (FieldHeight - boxHeight) / 2,
            Width - horizontalPadding * 2,
            boxHeight);

        _errorLine.SetBounds(horizontalPadding, FieldHeight + UITheme.Scaled(this, UITheme.Spacing8), Width, LineHeight);

        int targetHeight = TotalHeight;
        if (Height != targetHeight)
        {
            Height = targetHeight;
        }
    }

    private static void RepaintOwner(object? sender)
    {
        if (sender is Control box && box.Parent is InputField owner)
        {
            owner.Invalidate();
        }
    }

    private static void ForwardTextChanged(object? sender)
    {
        if (sender is Control box && box.Parent is InputField owner)
        {
            owner.Invalidate();
            owner.OnTextChanged(EventArgs.Empty);
        }
    }
}
