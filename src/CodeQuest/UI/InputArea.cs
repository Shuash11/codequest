// The multi-line text editors.

using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class InputArea : Panel
{
    private readonly TextBox _box = new();
    private readonly Label _errorLine = new();
    private readonly string? _fallbackAccessibleName;
    private Control? _labeledBy;
    private bool _borderError;

    public InputArea(string? accessibleName = null)
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);

        BackColor = UITheme.Surface;
        _fallbackAccessibleName = accessibleName;

        _box.Multiline = true;
        _box.AcceptsReturn = true;
        _box.ScrollBars = ScrollBars.Vertical;
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

        LayoutArea();
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

    public bool HasError => _borderError;

    public void ShowError(string message)
    {
        _borderError = true;
        _errorLine.ForeColor = UITheme.DangerText;
        _errorLine.Text = message;
        LayoutArea();
        Invalidate();
    }

    public void ClearError()
    {
        _borderError = false;
        _errorLine.Text = string.Empty;
        LayoutArea();
        Invalidate();
    }

    public void FocusInput() => _box.Focus();

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        LayoutArea();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        LayoutArea();
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

        var bounds = new System.Drawing.Rectangle(0, 0, Width - 1, FieldHeight - 1);
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

    private int FieldHeight => Height - UITheme.Scaled(this, UITheme.Spacing8) - LineHeight;

    private int LineHeight => _errorLine.GetPreferredSize(System.Drawing.Size.Empty).Height;

    private void LayoutArea()
    {
        int horizontalPadding = UITheme.Scaled(this, UITheme.Spacing12);
        int verticalPadding = UITheme.Scaled(this, UITheme.Spacing8);
        _box.SetBounds(
            horizontalPadding,
            verticalPadding,
            Width - horizontalPadding * 2,
            FieldHeight - verticalPadding * 2);

        _errorLine.SetBounds(horizontalPadding, Height - LineHeight, Width, LineHeight);
    }

    private static void RepaintOwner(object? sender)
    {
        if (sender is Control box && box.Parent is InputArea owner)
        {
            owner.Invalidate();
        }
    }

    private static void ForwardTextChanged(object? sender)
    {
        if (sender is Control box && box.Parent is InputArea owner)
        {
            owner.Invalidate();
            owner.OnTextChanged(EventArgs.Empty);
        }
    }
}
