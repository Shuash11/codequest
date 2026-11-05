// =============================================================================
// CodeQuest — Phase P7: UI Shell & Navigation
// The login screen of the design of record (spec 5.1) — the first runnable
// user path. Resolves plan backlog #5: the users table gets its purpose.
// Look at: the layout — centered fixed window (420×460) on Background; app
//          name "CodeQuest" (Title), subtitle "Programming and cybersecurity
//          practice quizzes" (Caption, TextSecondary), 24px, one card (§4.2)
//          holding the Username field, the masked Password field, 16px, the
//          full-width "Sign in" primary button (§4.1); the inline error line
//          slots under the password field (§4.4). Enter submits; the tab
//          order is username → password → button.
// States (§5.1): default → validating ("Signing in…" note, button disabled
//          while the stored hash is verified) → success (the shell opens —
//          no dialog) or error (the inline line "Incorrect username or
//          password." in DangerText, non-blocking, the password field clears,
//          the user stays on login). Unknown users get the SAME generic error
//          — no account enumeration. A database read failure is not a login
//          failure: it surfaces through the application's §4.11 unexpected
//          dialog (wired in Program), never as the credential error.
// =============================================================================

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

/// <summary>
/// The login screen (spec 5.1). Program shows it modally; DialogResult.OK
/// carries the signed-in account to the shell.
/// </summary>
public sealed class LoginForm : Form
{
    private const string IncorrectCredentialsMessage = "Incorrect username or password.";
    private const string SigningInMessage = "Signing in…";

    private readonly UserRepository _users;
    private readonly InputField _username;
    private readonly InputField _password;
    private readonly FlatButton _signIn;
    private bool _signingIn;

    /// <summary>Creates the login screen for the user repository to validate against.</summary>
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
        ClientSize = new Size(UITheme.Scaled(this, 420), UITheme.Scaled(this, 460));

        // Vertical block: title + 4 + subtitle + 24 + card(186), centered.
        int titleHeight = UITheme.Scaled(this, 32);
        int subtitleHeight = UITheme.Scaled(this, 18);
        int cardHeight = UITheme.Scaled(this, 186);
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
        int fieldHeight = UITheme.Scaled(this, 32);
        int passwordHeight = UITheme.Scaled(this, 58); // 32px field + reserved 8px gap + caption line (§5.1)

        var card = new CardPanel
        {
            Location = new Point(
                UITheme.Scaled(this, UITheme.Spacing24),
                blockTop + titleHeight + UITheme.Scaled(this, UITheme.Spacing4) + subtitleHeight
                    + UITheme.Scaled(this, UITheme.Spacing24)),
            Size = new Size(cardWidth, cardHeight),
        };

        _username = new InputField(accessibleName: "Username")
        {
            Name = "UsernamePanel",
            PlaceholderText = "Username",
            Location = new Point(inset, inset),
            Size = new Size(fieldWidth, fieldHeight),
        };
        NameInnerBox(_username, "UsernameField");

        _password = new InputField(reserveErrorLine: true, accessibleName: "Password")
        {
            Name = "PasswordPanel",
            PlaceholderText = "Password",
            Location = new Point(inset, inset + fieldHeight + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(fieldWidth, passwordHeight),
        };
        NameInnerBox(_password, "PasswordField");
        _password.SetPasswordMask(true);

        _signIn = new FlatButton
        {
            Name = "SignInButton",
            Text = "Sign in",
            Location = new Point(
                inset,
                inset + fieldHeight + UITheme.Scaled(this, UITheme.Spacing16) + passwordHeight
                    + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(fieldWidth, fieldHeight),
        };
        _signIn.Click += HandleSignInClick;

        card.Controls.Add(_username);
        card.Controls.Add(_password);
        card.Controls.Add(_signIn);
        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(card);

        AcceptButton = _signIn;

        // Tab order: username → password → button (spec 5.1).
        _username.TabIndex = 0;
        _password.TabIndex = 1;
        _signIn.TabIndex = 2;

        Shown += static (sender, e) =>
        {
            if (sender is LoginForm form)
            {
                form._username.FocusInput();
            }
        };
    }

    /// <summary>The account that passed verification; set together with DialogResult.OK.</summary>
    public UserAccount? AuthenticatedUser { get; private set; }

    /// <summary>
    /// The sign-in flow of spec 5.1: read the fields, look the account up,
    /// verify the stored hash, open the shell on success or show the inline
    /// error on failure — never a dialog for the credential error (§4.11).
    /// </summary>
    private void HandleSignInClick(object? sender, EventArgs e)
    {
        if (_signingIn)
        {
            return;
        }

        _signingIn = true;
        _signIn.Enabled = false;
        _password.ShowNote(SigningInMessage);
        Refresh(); // paint the validating state before the (blocking) verification starts

        UserAccount? user = TryAuthenticate(_username.Text, _password.Text);

        _signingIn = false;
        if (user is not null)
        {
            AuthenticatedUser = user;
            DialogResult = DialogResult.OK; // success opens the shell (Program runs it next)
            return;
        }

        _signIn.Enabled = true;
        _password.ShowError(IncorrectCredentialsMessage);
        _password.ClearText();
        _password.FocusInput();
    }

    /// <summary>
    /// Validates one credential pair against the users table. Returns null for
    /// an unknown user or a wrong password (both become the same generic
    /// inline error); a database failure is deliberately not caught here —
    /// it is not a login failure and travels to the application's §4.11
    /// unexpected dialog.
    /// </summary>
    private UserAccount? TryAuthenticate(string username, string password)
    {
        UserAccount? user = _users.GetByUsername(username);

        if (user is null || !PasswordHasher.Verify(user.PasswordHash, password))
        {
            return null;
        }

        return user;
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
