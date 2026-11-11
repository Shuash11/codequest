// All colors, fonts and sizes. (Encapsulation)

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CodeQuest.UI;

public static class UITheme
{

    public static readonly Color Background = ColorTranslator.FromHtml("#101216");

    public static readonly Color Surface = ColorTranslator.FromHtml("#171A20");

    public static readonly Color SurfaceRaised = ColorTranslator.FromHtml("#1E222B");

    public static readonly Color Fill = ColorTranslator.FromHtml("#232833");

    public static readonly Color FillPressed = ColorTranslator.FromHtml("#2A303D");

    public static readonly Color SegmentHover = ColorTranslator.FromHtml("#2C3140");

    public static readonly Color BorderControl = ColorTranslator.FromHtml("#646E82");

    public static readonly Color BorderHairline = ColorTranslator.FromHtml("#323845");

    public static readonly Color SeparatorHairline = ColorTranslator.FromHtml("#2A2F3A");

    public static readonly Color TextPrimary = ColorTranslator.FromHtml("#E8EAF0");

    public static readonly Color TextSecondary = ColorTranslator.FromHtml("#9BA3B4");

    public static readonly Color TextTertiary = ColorTranslator.FromHtml("#6B7385");

    public static readonly Color TextOnAccent = ColorTranslator.FromHtml("#FFFFFF");

    public static readonly Color Accent = ColorTranslator.FromHtml("#6C5CE7");

    public static readonly Color AccentHover = ColorTranslator.FromHtml("#5A4BD1");

    public static readonly Color AccentPressed = ColorTranslator.FromHtml("#493BB8");

    public static readonly Color AccentDisabled = ColorTranslator.FromHtml("#262938");

    public static readonly Color AccentText = ColorTranslator.FromHtml("#A79BF5");

    public static readonly Color AccentTint = ColorTranslator.FromHtml("#2A2749");

    public static readonly Color NavItemPressed = ColorTranslator.FromHtml("#262244");

    public static readonly Color Success = ColorTranslator.FromHtml("#2FA857");

    public static readonly Color SuccessText = ColorTranslator.FromHtml("#4CC27A");

    public static readonly Color SuccessTint = ColorTranslator.FromHtml("#16301F");

    public static readonly Color Danger = ColorTranslator.FromHtml("#CF3530");

    public static readonly Color DangerText = ColorTranslator.FromHtml("#F07A72");

    public static readonly Color DangerTint = ColorTranslator.FromHtml("#3A1A18");

    public static readonly Color DangerHover = ColorTranslator.FromHtml("#B32A24");

    public static readonly Color DangerPressed = ColorTranslator.FromHtml("#97201B");

    public static readonly Color DangerDisabled = ColorTranslator.FromHtml("#2E2325");

    public static readonly Color Warning = ColorTranslator.FromHtml("#B07C10");

    public static readonly Color WarningText = ColorTranslator.FromHtml("#E5A83B");

    public static readonly Color WarningTint = ColorTranslator.FromHtml("#38290F");

    public static readonly Color FunPrimary = Accent;

    public static readonly Color FunPrimaryHover = AccentHover;

    public static readonly Color TileRed = ColorTranslator.FromHtml("#D63040");

    public static readonly Color TileBlue = ColorTranslator.FromHtml("#2D6BE4");

    public static readonly Color TileAmber = ColorTranslator.FromHtml("#A65F00");

    public static readonly Color TileGreen = ColorTranslator.FromHtml("#178146");

    private static readonly IReadOnlyDictionary<Color, Color> TileHoverMap = new Dictionary<Color, Color>
    {
        [TileRed] = ColorTranslator.FromHtml("#B62936"),
        [TileBlue] = ColorTranslator.FromHtml("#265BC2"),
        [TileAmber] = ColorTranslator.FromHtml("#8D5100"),
        [TileGreen] = ColorTranslator.FromHtml("#146E3C"),
    };

    public static Color TileHover(Color tile)
        => TileHoverMap.TryGetValue(tile, out Color hover)
            ? hover
            : Color.FromArgb(
                (int)Math.Round(tile.R * 0.85),
                (int)Math.Round(tile.G * 0.85),
                (int)Math.Round(tile.B * 0.85));

    public static readonly Color CorrectWash = ColorTranslator.FromHtml("#178146");

    public static readonly Color WrongWash = ColorTranslator.FromHtml("#B3241F");

    public static readonly Color LevelEasy = ColorTranslator.FromHtml("#4CC27A");

    public static readonly Color LevelMedium = ColorTranslator.FromHtml("#E5A83B");

    public static readonly Color LevelHard = ColorTranslator.FromHtml("#F07A72");

    public static readonly Color StreakFlame = ColorTranslator.FromHtml("#F97316");

    public static readonly Color PerfectGold = ColorTranslator.FromHtml("#F2C14E");

    public static readonly Font Title = new("Segoe UI Semibold", 16f);

    public static readonly Font Body = new("Segoe UI", 10f);

    public static readonly Font Caption = new("Segoe UI", 9f);

    public static readonly Font BodySemibold = new("Segoe UI Semibold", 10f);

    public static readonly Font CaptionSemibold = new("Segoe UI Semibold", 9f);

    public static readonly Font NavGlyph = new("Segoe MDL2 Assets", 12f);

    public static readonly Font AchievementGlyph = new("Segoe MDL2 Assets", 16f);

    public static readonly Font Display = new("Segoe UI", 28f, FontStyle.Bold);

    public static readonly Font QuestionHeadline = new("Segoe UI Semibold", 14f);

    public static readonly Font GameGlyph = CreateGameGlyph(12f, NavGlyph);

    public static readonly Font GameGlyphLarge = CreateGameGlyph(18f, AchievementGlyph);

    public const bool MotionEnabled = true;

    public static readonly string GlyphCheckCircle = GameIcon("\uF299", "\u2713");

    public static readonly string GlyphDismissCircle = GameIcon("\uF36E", "\u2717");

    public static readonly string GlyphCheck = GameIcon("\uF295", "\u2713");

    public static readonly string GlyphDismiss = GameIcon("\uF36A", "\u2717");

    public static readonly string GlyphShuffle = GameIcon("\uEF37", string.Empty);

    public static readonly string GlyphPlay = GameIcon("\uF606", string.Empty);

    public static readonly string GlyphFlame = GameIcon("\uEF6A", string.Empty);

    public static readonly string GlyphTrophy = GameIcon("\uF837", string.Empty);

    public static readonly string GlyphRocket = GameIcon("\uF678", string.Empty);

    public static readonly string GlyphFlash = GameIcon("\uE619", string.Empty);

    public static bool UsesGameIconFont { get; } = GameFontBytes is not null;

    // The Fluent System Icons font, embedded once and pinned for the whole
    // process (GDI+ reads the memory while any Font from it is alive).
    private static readonly byte[]? GameFontBytes = LoadGameFontBytes();

    private static readonly PrivateFontCollection GameFontCollection = LoadGameFontCollection();

    private static Font CreateGameGlyph(float size, Font fallback)
    {
        if (GameFontBytes is null || GameFontCollection.Families.Length == 0)
        {
            return fallback;
        }

        return new Font(GameFontCollection.Families[0], size);
    }

    private static string GameIcon(string fluentCodepoint, string fallback)
    {
        return UsesGameIconFont ? fluentCodepoint : fallback;
    }

    private static byte[]? LoadGameFontBytes()
    {
        try
        {
            using Stream? stream = typeof(UITheme).Assembly.GetManifestResourceStream(
                "CodeQuest.Assets.FluentSystemIcons-Regular.ttf");
            if (stream is null)
            {
                return null;
            }

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
        catch
        {
            return null; // a missing icon font degrades to the Segoe glyphs
        }
    }

    private static PrivateFontCollection LoadGameFontCollection()
    {
        var collection = new PrivateFontCollection();
        byte[]? bytes = GameFontBytes;
        if (bytes is not null)
        {
            IntPtr buffer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            collection.AddMemoryFont(buffer, bytes.Length); // the buffer stays allocated on purpose
        }

        return collection;
    }

    public const int Spacing4 = 4;

    public const int Spacing8 = 8;

    public const int Spacing12 = 12;

    public const int Spacing16 = 16;

    public const int Spacing24 = 24;

    public const int Spacing32 = 32;

    public const int RadiusCard = 12;

    public const int RadiusControl = 8;

    public const int RadiusChip = 4;

    public static void Apply()
    {
        Application.SetDefaultFont(Body);
    }

    public static int Scaled(Control control, int pixels)
    {
        return (int)Math.Round(pixels * control.DeviceDpi / 96f);
    }

    public static Color ContainerSurface(Control control)
        => control.Parent is { } parent ? parent.BackColor : control.BackColor;

    public static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
        int diameter = radius * 2;

        if (radius == 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public enum FlatButtonVariant
    {
        Primary,

        Secondary,

        Destructive,
    }

    public enum FlatButtonState
    {
        Default,
        Hover,
        Pressed,
        Focus,
        Disabled,
    }

    public static Color FlatButtonFill(FlatButtonVariant variant, FlatButtonState state)
    {
        switch (variant)
        {
            case FlatButtonVariant.Secondary:
                return state switch
                {
                    FlatButtonState.Hover => Fill,
                    FlatButtonState.Pressed => FillPressed,
                    _ => Color.Empty,
                };

            case FlatButtonVariant.Destructive:
                return state switch
                {
                    FlatButtonState.Hover => DangerHover,
                    FlatButtonState.Pressed => DangerPressed,
                    FlatButtonState.Disabled => DangerDisabled,
                    _ => Danger,
                };

            default:
                return state switch
                {
                    FlatButtonState.Hover => AccentHover,
                    FlatButtonState.Pressed => AccentPressed,
                    FlatButtonState.Disabled => AccentDisabled,
                    _ => Accent,
                };
        }
    }

    public static Color FlatButtonLabel(FlatButtonVariant variant, FlatButtonState state)
    {
        switch (variant)
        {
            case FlatButtonVariant.Secondary:
                return state == FlatButtonState.Disabled ? TextTertiary : AccentText;

            default:
                return state == FlatButtonState.Disabled ? TextTertiary : TextOnAccent;
        }
    }

    public static Color FlatButtonFocusRing(FlatButtonVariant variant)
        => variant == FlatButtonVariant.Secondary ? AccentText : TextPrimary;
}
