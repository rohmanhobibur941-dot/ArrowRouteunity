using UnityEngine;

namespace ArrowRoute
{
    /// <summary>Colours of the three board themes (same numbers as the Android version).</summary>
    public static class Themes
    {
        public const int Classic = 0;
        public const int Neon = 1;
        public const int Candy = 2;

        static Color Hex(int rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        }

        static readonly Color[] NeonColors =
        {
            Hex(0xFFD54F), Hex(0x64B5F6), Hex(0xBA68C8), Hex(0xF06292), Hex(0x4DD0E1),
            Hex(0xAED581), Hex(0xFF8A65), Hex(0x7986CB), Hex(0x81C784), Hex(0xE57373)
        };

        static readonly Color[] CandyColors =
        {
            Hex(0x1E1E24), Hex(0x22B14C), Hex(0xFFC70F), Hex(0x2D8CFF), Hex(0xF0453A),
            Hex(0x8E5BE8), Hex(0xFF8A2B), Hex(0xFF5FA2), Hex(0x14C4B4)
        };

        public static readonly Color Red = Hex(0xE53935);
        public static readonly Color Hint = Hex(0x2ECC71);
        public static readonly Color HintLight = Hex(0xC8FFDC);

        /// <summary>Classic = black arrows, Neon = many colours, Candy = candy colours.</summary>
        public static Color ArrowColor(int theme, int id)
        {
            if (theme == Classic) return Hex(0x1B1B1B);
            if (theme == Neon) return NeonColors[id % NeonColors.Length];
            return CandyColors[id % CandyColors.Length];
        }

        public static Color BoardBg(int theme)
        {
            if (theme == Neon) return Hex(0x141B2B);
            if (theme == Candy) return Hex(0xF4E2F3);
            return Hex(0xE9DBBE);
        }

        public static Color DotColor(int theme)
        {
            if (theme == Neon) return Hex(0xFFFFFF, 0.53f);
            if (theme == Candy) return Hex(0x8E4A9E, 0.55f);
            return Hex(0x6B5232, 0.55f);
        }

        public static Color PatternColor(int theme)
        {
            if (theme == Neon) return Hex(0x00E5FF, 0.12f);
            if (theme == Candy) return Hex(0xFF5FA2, 0.15f);
            return Hex(0x8A5A2B, 0.12f);
        }

        public static Color Shade(Color c, float f)
        {
            return new Color(c.r * f, c.g * f, c.b * f, 1f);
        }

        // ------------------------------------------------------------ game screen look per theme

        public sealed class UiPalette
        {
            public string background;   // texture name in Resources/UI, or null for a plain colour
            public Color headTop, headBottom, bar, edge, chip, zoomBg;
        }

        public static UiPalette Ui(int theme)
        {
            UiPalette p = new UiPalette();
            if (theme == Neon)
            {
                p.background = null;
                p.headTop = Hex(0x0A1020); p.headBottom = Hex(0x101A34); p.bar = Hex(0x0A1020);
                p.edge = Hex(0x00E5FF); p.chip = Hex(0x00E5FF, 0.4f); p.zoomBg = Hex(0x16213C);
            }
            else if (theme == Candy)
            {
                p.background = "bg_home";
                p.headTop = Hex(0x3A1AA8, 0.94f); p.headBottom = Hex(0x6A2FD8, 0.70f); p.bar = Hex(0x3A1AA8, 0.94f);
                p.edge = Hex(0xFF7BD5); p.chip = Hex(0xFFFFFF, 0.4f); p.zoomBg = Hex(0x4A24B8);
            }
            else
            {
                p.background = "bg_wood";
                p.headTop = Hex(0x1E1004, 0.94f); p.headBottom = Hex(0x2B1808, 0.70f); p.bar = Hex(0x1E1004, 0.90f);
                p.edge = Hex(0x8A5A2B); p.chip = Hex(0x000000, 0.4f); p.zoomBg = Hex(0x3A230F);
            }
            return p;
        }

        public static Color DifficultyColor(int d)
        {
            if (d == 0) return Hex(0x3FBF2C);
            if (d == 1) return Hex(0xF2B01E);
            if (d == 2) return Hex(0xFF7A1A);
            return Hex(0xE9453A);
        }

        public static string DifficultyLabel(int d)
        {
            if (d == 0) return "lbl_easy";
            if (d == 1) return "lbl_medium";
            if (d == 2) return "lbl_hard";
            return "lbl_extreme";
        }
    }
}
