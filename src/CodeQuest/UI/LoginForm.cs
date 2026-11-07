// =============================================================================
// CodeQuest — UI Shell & Navigation (login, name-only)
// The login screen, revised by owner directive D1 (queued 2026-11-04, folded
// into the revised plan's I1): the user enters a NAME and the system records
// who is using it — no password. This supersedes design-of-record spec 5.1's
// password flow; the spec amendment rides the I5 designer pass.
// Look at: the layout — centered fixed window (420×380) on Background; app
//          name "CodeQuest" (Title), subtitle "Programming and cybersecurity
//          practice quizzes" (Caption, TextSecondary), 24px, one card (§4.2)
//          holding the Name field (with its inline error line, §4.4) and the
//          full-width "Sign in" primary button (§4.1). Enter submits; the tab
//          order is name → button.
// States (§5.1 adapted): default → resolving (button disabled during the
//          find-or-create) → success (the shell opens — no dialog) or the
//          inline error "Enter your name to continue." (DangerText,
//          non-blocking). The FIRST sign-in with a name creates the account
//          (find-or-create on users.username); later sign-ins with the same
//          name resolve to the same account, so attempts keep attributing to
//          one user. The schema's password_hash column is left in place but
//          unused (empty string) — the D1 disposition; PasswordHasher was
//          removed with this directive. A database failure is not a login
//          failure: it surfaces through the application's §4.11 unexpected
//          dialog (wired in Program), never as an inline credential error.
// =============================================================================

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The login screen (spec 5.1, name-only per directive D1). Program shows it
/// modally; DialogResult.OK carries the resolved account to the shell.
/// </summary>
public sealed class LoginForm : Form
{
    private const string EmptyNameMessage = "Enter your name to continue.";

    private readonly UserRepository _users;
    private readonly InputField _name;
    private readonly FlatButton _signIn;
    private bool _signingIn;

    /// <summary>Creates the login screen for the user repository to resolve against.</summary>
    public LoginForm(UserRepository users)
    {
        ArgumentNullException.ThrowIfNull(users);
        _users = users;

        Text = "CodeQuest";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UITheme.Background;
        ClientSize = new Size(UITheme.Scaled(this, 420), UITheme.Scaled(this, 380));

        // Vertical block: title + 4 + subtitle + 24 + card(138), centered.
        int titleHeight = UITheme.Scaled(this, 32);
        int subtitleHeight = UITheme.Scaled(this, 18);
        int cardHeight = UITheme.Scaled(this, 138);
        int blockTop = (ClientSize.Height - (titleHeight + UITheme.Scaled(this, UITheme.Spacing4) + subtitleHeight
            + UITheme.Scaled(this, UITheme.Spacing24) + cardHeight)) / 2;

        var titleLabel = new Label
        {
            Text = "CodeQuest",
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Background,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, blockTop),
            Size = new Size(ClientSize.Width, titleHeight),
        };

        var subtitleLabel = new Label
        {
            Text = "Programming and cybersecurity practice quizzes",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, blockTop + titleHeight + UITheme.Scaled(this, UITheme.Spacing4)),
            Size = new Size(ClientSize.Width, subtitleHeight),
        };

        int cardWidth = ClientSize.Width - UITheme.Scaled(this, UITheme.Spacing24) * 2;
        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int fieldWidth = cardWidth - inset * 2;
        int fieldHeight = UITheme.Scaled(this, 58); // 32px field + reserved 8px gap + caption line (§4.4)
        int buttonHeight = UITheme.Scaled(this, 32);

        var card = new CardPanel
        {
            Location = new Point(
                UITheme.Scaled(this, UITheme.Spacing24),
                blockTop + titleHeight + UITheme.Scaled(this, UITheme.Spacing4) + subtitleHeight
                    + UITheme.Scaled(this, UITheme.Spacing24)),
            Size = new Size(cardWidth, cardHeight),
        };

        _name = new InputField(reserveErrorLine: true, accessibleName: "Name")
        {
            Name = "NamePanel",
            PlaceholderText = "Your name",
            Location = new Point(inset, inset),
            Size = new Size(fieldWidth, fieldHeight),
        };
        NameInnerBox(_name, "NameField");

        _signIn = new FlatButton
        {
            Name = "SignInButton",
            Text = "Sign in",
            Location = new Point(inset, inset + fieldHeight + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(fieldWidth, buttonHeight),
        };
        _signIn.Click += HandleSignInClick;

        card.Controls.Add(_name);
        card.Controls.Add(_signIn);
        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(card);

        AcceptButton = _signIn;

        // Tab order: name → button.
        _name.TabIndex = 0;
        _signIn.TabIndex = 1;

        Shown += static (sender, e) =>
        {
            if (sender is LoginForm form)
            {
                form._name.FocusInput();
            }
        };
    }

    /// <summary>The account resolved for this session; set together with DialogResult.OK.</summary>
    public UserAccount? AuthenticatedUser { get; private set; }

    /// <summary>
    /// The sign-in flow (D1): read the name, find or create the account, open
    /// the shell on success or show the inline error for an empty field —
    /// never a dialog for the inline error (§4.11's dialog is for unexpected
    /// failures only).
    /// </summary>
    private void HandleSignInClick(object? sender, EventArgs e)
    {
        if (_signingIn)
        {
            return;
        }

        _signingIn = true;
        _signIn.Enabled = false;
        Refresh(); // paint the disabled state before the (fast, blocking) database calls

        UserAccount? user;
        try
        {
            user = ResolveUser(_name.Text);
        }
        finally
        {
            // A database failure is not a sign-in decision (approved D1
            // disposition): it propagates to the §4.11 unexpected dialog in
            // Program — and when the user returns via "Try again", the form
            // must be usable again, so the guard and button reset always run.
            _signingIn = false;
            _signIn.Enabled = true;
        }

        if (user is not null)
        {
            AuthenticatedUser = user;
            DialogResult = DialogResult.OK; // success opens the shell (Program runs it next)
            return;
        }

        _name.ShowError(EmptyNameMessage);
        _name.FocusInput();
    }

    /// <summary>
    /// Finds the account for the entered name or creates it on first use.
    /// Returns null only for an empty/whitespace name (the caller shows the
    /// inline error). A database failure is deliberately not caught here —
    /// it is not a sign-in decision and travels to the application's §4.11
    /// unexpected dialog. The stored password_hash is left empty: the column
    /// is unused since directive D1 — its security disposition is recorded in
    /// the revised plan and in docs/security-notes.md when I5 creates it.
    /// </summary>
    private UserAccount? ResolveUser(string rawName)
    {
        string name = rawName.Trim();
        if (name.Length == 0)
        {
            return null;
        }

        UserAccount? user = _users.GetByUsername(name);
        if (user is not null)
        {
            return user;
        }

        var created = new UserAccount
        {
            Username = name,
            DisplayName = name,
            PasswordHash = string.Empty, // unused column since directive D1
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _users.Add(created);
        return created;
    }

    /// <summary>Names the field's inner text box so tests can address it.</summary>
    private static void NameInnerBox(InputField field, string name)
    {
        foreach (Control child in field.Controls)
        {
            if (child is TextBox)
            {
                child.Name = name;
            }
        }
    }
}
