// =============================================================================
// CodeQuest — Phase P8: Topic & Quiz Management
// Recipe 4.4 of the design of record, multi-line editors: "Multi-line editors
// (question text, explanation): same recipe, minimum 64px height, top-aligned."
// Look at: the same Surface field, 8px radius, 1px border with the
//          default/focus/error swaps as <see cref="InputField"/>, but the
//          TextBox inside is multiline, top-aligned, and the field is as tall
//          as the form asks (≥64px). The inline error line below (DangerText
//          Caption) is owned here like InputField owns it — errors are inline,
//          never dialogs (§4.4).
// =============================================================================

using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The styled multi-line text input of recipe 4.4: the question text and
/// explanation editors of the question form (spec 5.5). The error-line slot
/// below the field is always reserved (login-card convention, spec 5.1), so
/// showing an error never shifts the surrounding layout.
/// </summary>
public sealed class InputArea : Panel
{
    private readonly TextBox _box = new();
    private readonly Label _errorLine = new();
    private bool _borderError;

    /// <summary>
    /// Creates the multi-line field. <paramref name="accessibleName"/> names
    /// the inner text box for screen readers (WCAG 4.1.2).
    /// </summary>
    public InputArea(string? accessibleName = null)
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);

        BackColor = UITheme.Surface;

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
        _box.TextChanged += static (sender, e) => RepaintOwner(sender);
        Controls.Add(_box);

        _errorLine.AutoSize = true;
        _errorLine.Font = UITheme.Caption;
        _errorLine.BackColor = UITheme.Surface;
        Controls.Add(_errorLine);

        LayoutArea();
    }

    /// <summary>The entered text.</summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _box.Text;
        set => _box.Text = value;
    }

    /// <summary>The quiet hint shown when the field is empty (recipe 4.4).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string PlaceholderText
    {
        get => _box.PlaceholderText;
        set => _box.PlaceholderText = value;
    }

    /// <summary>True when the field carries the recipe's error border.</summary>
    public bool HasError => _borderError;

    /// <summary>Recipe 4.4 error state: border DangerText + the inline line below.</summary>
    public void ShowError(string message)
    {
        _borderError = true;
        _errorLine.ForeColor = UITheme.DangerText;
        _errorLine.Text = message;
        LayoutArea();
        Invalidate();
    }

    /// <summary>Leaves the error state: border restored, line cleared.</summary>
    public void ClearError()
    {
        _borderError = false;
        _errorLine.Text = string.Empty;
        LayoutArea();
        Invalidate();
    }

    /// <summary>Moves the keyboard focus into the field's text box.</summary>
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

        // Recipe 4.4 disabled state: Fill background, TextTertiary text.
        _box.BackColor = Enabled ? UITheme.Surface : UITheme.Fill;
        _box.ForeColor = Enabled ? UITheme.TextPrimary : UITheme.TextTertiary;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        if (!Enabled)
        {
            return; // the disabled state has no border at all (recipe 4.4)
        }

        Color borderColor = _borderError ? UITheme.DangerText
            : _box.Focused ? UITheme.Accent
            : UITheme.BorderControl;

        var bounds = new System.Drawing.Rectangle(0, 0, Width - 1, FieldHeight - 1);
        using var path = UITheme.RoundedPath(bounds, UITheme.Scaled(this, UITheme.RadiusControl));
        e.Graphics.DrawPath(new System.Drawing.Pen(borderColor), path);
    }

    /// <summary>The editable area height: the control's height minus the reserved error slot.</summary>
    private int FieldHeight => Height - UITheme.Scaled(this, UITheme.Spacing8) - LineHeight;

    private int LineHeight => _errorLine.GetPreferredSize(System.Drawing.Size.Empty).Height;

    /// <summary>Places the text box (12px horizontal padding, 8px vertical) and the error line.</summary>
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

    /// <summary>The text box repaints its border owner on focus/text changes.</summary>
    private static void RepaintOwner(object? sender)
    {
        if (sender is Control box && box.Parent is InputArea owner)
        {
            owner.Invalidate();
        }
    }
}
