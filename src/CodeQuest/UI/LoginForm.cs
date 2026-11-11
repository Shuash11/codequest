// The name screen. (Inheritance)

using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using System.Drawing;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class LoginForm : Form
{
    private const string EmptyNameMessage = "Enter your name to continue.";

    private readonly UserRepository _users;
    private readonly InputField _name;
    private readonly FlatButton _signIn;
    private bool _signingIn;

    public LoginForm(UserRepository users, string? rememberedName = null)
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

        int chipSize = UITheme.Scaled(this, 44);
        int titleHeight = UITheme.Scaled(this, 32);
        int subtitleHeight = UITheme.Scaled(this, 18);
        int cardHeight = UITheme.Scaled(this, 138);
        int blockHeight = chipSize + UITheme.Scaled(this, UITheme.Spacing12) + titleHeight + UITheme.Scaled(this, UITheme.Spacing8)
            + subtitleHeight + UITheme.Scaled(this, UITheme.Spacing32) + cardHeight;
        int blockTop = (ClientSize.Height - blockHeight) / 2;
        int chipX = (ClientSize.Width - chipSize) / 2;

        var logo = new Panel
        {
            Name = "LoginLogoChip",
            Size = new Size(chipSize, chipSize),
            Location = new Point((ClientSize.Width - chipSize) / 2, blockTop),
            BackColor = UITheme.Background,
        };
        logo.Paint += static (sender, e) =>
        {
            if (sender is Panel panel)
            {
                using var path = UITheme.RoundedPath(
                    new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), UITheme.Scaled(panel, 12));
                using var brush = new SolidBrush(UITheme.Accent);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(
                    e.Graphics,
                    "CQ",
                    UITheme.BodySemibold,
                    panel.ClientRectangle,
                    UITheme.TextOnAccent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };

        var titleLabel = new Label
        {
            Text = "CodeQuest",
            Font = UITheme.Title,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Background,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, blockTop + chipSize + UITheme.Scaled(this, UITheme.Spacing12)),
            Size = new Size(ClientSize.Width, titleHeight),
        };

        var subtitleLabel = new Label
        {
            Text = "Programming and cybersecurity practice quizzes",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(
                0,
                blockTop + chipSize + UITheme.Scaled(this, UITheme.Spacing12) + titleHeight + UITheme.Scaled(this, UITheme.Spacing8)),
            Size = new Size(ClientSize.Width, subtitleHeight),
        };

        int cardWidth = ClientSize.Width - UITheme.Scaled(this, UITheme.Spacing24) * 2;
        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int fieldWidth = cardWidth - inset * 2;
        int fieldHeight = UITheme.Scaled(this, 58);
        int buttonHeight = UITheme.Scaled(this, 32);

        var card = new CardPanel
        {
            Location = new Point(
                UITheme.Scaled(this, UITheme.Spacing24),
                blockTop + chipSize + UITheme.Scaled(this, UITheme.Spacing12) + titleHeight + UITheme.Scaled(this, UITheme.Spacing8)
                    + subtitleHeight + UITheme.Scaled(this, UITheme.Spacing32)),
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

        if (!string.IsNullOrWhiteSpace(rememberedName))
        {
            _name.Text = rememberedName.Trim();
        }

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
        Controls.Add(logo);
        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(card);

        AcceptButton = _signIn;

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

    public UserAccount? AuthenticatedUser { get; private set; }

    private void HandleSignInClick(object? sender, EventArgs e)
    {
        if (_signingIn)
        {
            return;
        }

        _signingIn = true;
        _signIn.Enabled = false;
        Refresh();

        UserAccount? user;
        try
        {
            user = ResolveUser(_name.Text);
        }
        finally
        {
            _signingIn = false;
            _signIn.Enabled = true;
        }

        if (user is not null)
        {
            AuthenticatedUser = user;
            DialogResult = DialogResult.OK;
            return;
        }

        _name.ShowError(EmptyNameMessage);
        _name.FocusInput();
    }

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
            PasswordHash = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _users.Add(created);
        return created;
    }

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
