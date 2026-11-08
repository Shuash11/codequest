// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation
// Recipe 4.4 of the design of record: the text input.
// Look at: OnPaint — a Surface field with the 8px radius and a 1px border
//          that swaps BorderControl → AccentText on keyboard focus (no glow;
//          the D4 accent-as-ink rule) and → DangerText in the error state.
//          The area OUTSIDE the rounded field (corner cutouts and the error-
//          line slot) shows the CONTAINER's surface — resolved via
//          UITheme.ContainerSurface at paint time (D4 audit rule #1: the same
//          field sits on cards, dialogs and module backgrounds), and the
//          error line's BackColor follows it (audit rule #9). The borderless
//          TextBox inside carries the native PlaceholderText and the Body
//          font. The inline error line (DangerText Caption, 8px below the
//          field — "the error line persists until the value validates; it
//          never appears as a dialog") is owned here: forms call
//          ShowError/ShowNote/ClearError. The recipe's disabled state (Fill
//          background, TextTertiary text, no border) is painted too. With
//          ReserveErrorLine the line's slot is always reserved (login card,
//          spec 5.1: "inline error line slots"), so the layout never jumps;
//          without it the line only exists while an error shows.
// =============================================================================

using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The styled single-line text input of recipe 4.4: Surface fill, 8px radius,
/// 1px border with default/focus/error states, optional inline error line.
/// </summary>
public sealed class InputField : Panel
{
    private readonly TextBox _box = new();
    private readonly Label _errorLine = new();
    private readonly string? _fallbackAccessibleName;
    private Control? _labeledBy;
    private bool _borderError;

    /// <summary>
    /// Creates the field at the recipe's 32px height (plus the reserved error
    /// slot when asked). <paramref name="accessibleName"/> names the inner text
    /// box for screen readers (WCAG 4.1.2) — the placeholder must not be the
    /// field's only accessible name.
    /// </summary>
    public InputField(bool reserveErrorLine = false, string? accessibleName = null)
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);

        BackColor = UITheme.Surface; // the field fill token (see OnPaint/OnPaintBackground)
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
        _errorLine.BackColor = UITheme.Surface; // corrected to the container surface on attach (OnParentChanged)
        Controls.Add(_errorLine);

        LayoutField();
    }

    /// <summary>
    /// The field was placed on its container: the error line sits directly on
    /// that surface (below the rounded field), so its BackColor must equal
    /// the container's — never a fixed token (D4 audit rules #1/#9).
    /// </summary>
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        _errorLine.BackColor = UITheme.ContainerSurface(this);
        Invalidate();
    }

    /// <summary>
    /// Everything OUTSIDE the rounded field — the corner cutouts and the
    /// error-line slot — shows the container's surface (D4 audit rule #1).
    /// The field fill itself paints in OnPaint.
    /// </summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(UITheme.ContainerSurface(this));
    }

    /// <summary>
    /// The visible label whose text names this field (§4.4's association rule,
    /// F2): the label is the source of the input's accessible name, not a
    /// string retyped beside it. Setting it replaces the fallback name.
    /// </summary>
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

    /// <summary>The entered text.</summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _box.Text;
        set => _box.Text = value;
    }

    /// <summary>The quiet hint shown when the field is empty (recipe 4.4, native placeholder).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string PlaceholderText
    {
        get => _box.PlaceholderText;
        set => _box.PlaceholderText = value;
    }

    /// <summary>Masks the input for passwords (spec 5.1).</summary>
    public void SetPasswordMask(bool masked)
    {
        _box.UseSystemPasswordChar = masked;
    }

    /// <summary>True when the field carries the recipe's error border.</summary>
    public bool HasError => _borderError;

    /// <summary>
    /// Whether the error-line slot is permanently reserved below the field
    /// (login form, spec 5.1) so the surrounding layout never jumps.
    /// </summary>
    public bool ReserveErrorLine { get; }

    /// <summary>
    /// Recipe 4.4 error state: border DangerText + the inline error line
    /// directly below, in DangerText Caption.
    /// </summary>
    public void ShowError(string message)
    {
        _borderError = true;
        _errorLine.ForeColor = UITheme.DangerText;
        _errorLine.Text = message;
        Invalidate();
    }

    /// <summary>
    /// The validating caption of spec 5.1 ("Signing in…"): the same reserved
    /// line slot, in the quiet secondary color, border unchanged.
    /// </summary>
    public void ShowNote(string message)
    {
        _errorLine.ForeColor = UITheme.TextSecondary;
        _errorLine.Text = message;
        Invalidate();
    }

    /// <summary>Leaves the error state: border restored, line cleared.</summary>
    public void ClearError()
    {
        _borderError = false;
        _errorLine.Text = string.Empty;
        Invalidate();
    }

    /// <summary>Clears the entered text (spec 5.1: the password field clears on a failed sign-in).</summary>
    public void ClearText()
    {
        _box.Text = string.Empty;
    }

    /// <summary>Moves the keyboard focus into the field's text box.</summary>
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

        // Recipe 4.4 disabled state: Fill background, TextTertiary text.
        _box.BackColor = Enabled ? UITheme.Surface : UITheme.Fill;
        _box.ForeColor = Enabled ? UITheme.TextPrimary : UITheme.TextTertiary;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // The field fill: Surface (the input fill token at every elevation),
        // or the recipe's disabled Fill. The background pass already filled
        // the container surface around the rounded field.
        System.Drawing.Rectangle bounds = new(0, 0, Width - 1, FieldHeight - 1);
        using (var fieldPath = UITheme.RoundedPath(bounds, UITheme.Scaled(this, UITheme.RadiusControl)))
        using (var fieldBrush = new System.Drawing.SolidBrush(Enabled ? UITheme.Surface : UITheme.Fill))
        {
            e.Graphics.FillPath(fieldBrush, fieldPath);
        }

        // Recipe 4.4 disabled state has no border at all; otherwise the 1px
        // border carries the default/focus/error colors of the state table
        // (the focus swap is AccentText on dark — the accent-as-ink rule).
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

    /// <summary>The box part of the field: 32px at standard DPI (recipe 4.4).</summary>
    private int FieldHeight => UITheme.Scaled(this, 32);

    /// <summary>
    /// The full height of the control: the 32px field plus, when the error
    /// slot is reserved, the 8px gap and one Caption line below it.
    /// </summary>
    private int TotalHeight => FieldHeight
        + (ReserveErrorLine || _errorLine.Text.Length > 0
            ? UITheme.Scaled(this, UITheme.Spacing8) + LineHeight
            : 0);

    private int LineHeight => _errorLine.GetPreferredSize(System.Drawing.Size.Empty).Height;

    /// <summary>Places the text box (12px horizontal padding, vertically centered) and the error line.</summary>
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

    /// <summary>The text box repaints its border owner on focus changes.</summary>
    private static void RepaintOwner(object? sender)
    {
        if (sender is Control box && box.Parent is InputField owner)
        {
            owner.Invalidate();
        }
    }

    /// <summary>
    /// Forwards the inner text box's text changes to the field: the border
    /// repaints and the field's own TextChanged event fires. The Text
    /// override delegates to the inner box, so without this forwarding the
    /// field's subscribers never hear anything — the live search (spec 5.3,
    /// 5.4) would filter only on a reload, and the dialog forms' inline
    /// error-clearing handlers would stay silent.
    /// </summary>
    private static void ForwardTextChanged(object? sender)
    {
        if (sender is Control box && box.Parent is InputField owner)
        {
            owner.Invalidate();
            owner.OnTextChanged(EventArgs.Empty);
        }
    }
}
