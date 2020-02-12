using Heleus.Base;
using Xamarin.Forms;

namespace Heleus.Apps.Shared
{
	public enum ThemeColorStyle
	{
		None,

		Primary,
		Secondary,

		Info,
		Warning,
		Error,

		Text,

		Row,
		RowHover,
		RowHighlight,
		RowDisabled,

        MessageRow,

        Submit,
        SubmitHover,
        SubmitHighlight,
        SubmitDisabled,

        Cancel,
        CancelHover,
        CancelHighlight,
        CancelDisabled
    }

    public enum ThemeFontStyle
	{
		None,

        Text,
        Small,
        Detail,
        Micro,


        RowTitle,
		RowHeader,
		RowFooter,
		Row,
		RowIcon
	}

    class MessageRowButtonStyle : IButtonStyle
    {
        public ColorStyle Color => Theme.MessageRowColor;
        public ColorStyle HoverColor => Theme.RowHoverColor;
        public ColorStyle HighlightColor => Theme.RowHighlightColor;
        public ColorStyle DisabledColor => Theme.RowDisabledColor;
    }

    static partial class Theme
	{
		static void Write(ChunkWriter writer)
		{

		}

		static void Read(ChunkReader reader)
		{

		}

		static void SetDefaults()
		{
			_windowTheme = DefaultWindowTheme = WindowTheme.Dark;
		}

        public static readonly IButtonStyle MessageRowButton = new MessageRowButtonStyle();

        static readonly Color Primary = Color.FromRgb(60,160,240); // #3CA0F0
		static readonly Color Secondary = Color.FromRgb(0,120,250); // #0078FA

        public static readonly ColorStyle PrimaryColor = new ThemedColorStyle(ThemeColorStyle.Primary, Primary);
        public static readonly ColorStyle SecondaryColor = new ThemedColorStyle(ThemeColorStyle.Secondary, Secondary);

        public static readonly ColorStyle InfoColor = new ColorStyle(ThemeColorStyle.Info, Color.FromRgb(0, 250, 0));
        public static readonly ColorStyle WarningColor = new ColorStyle(ThemeColorStyle.Warning, Color.FromRgb(250, 250, 0));
        public static readonly ColorStyle ErrorColor = new ColorStyle(ThemeColorStyle.Error, Color.FromRgb(250, 0, 0));

        public static readonly ColorStyle TextColor = new ThemedColorStyle(ThemeColorStyle.Text, Color.White, Color.White);

        static readonly Color _submitColor = Color.FromRgb(0, 255, 0);
        static readonly Color _cancelColor = Color.FromRgb(255, 0, 0);

        public const double RowAlpha = 0.5;
        public const double RowHoverAlpha = 0.6;
        public const double RowHighlightAlpha = 0.4;
        public const double RowDisabledAlpha = 0.3;

#if !GTK
        static readonly Color _rowColor = Color.FromRgb(0, 30, 60);

        public static readonly ColorStyle MessageRowColor = new ThemedColorStyle(ThemeColorStyle.MessageRow, _rowColor.MultiplyAlpha(0.9), true);

        public static readonly ColorStyle RowColor = new ThemedColorStyle(ThemeColorStyle.Row, _rowColor.MultiplyAlpha(RowAlpha), true);
        public static readonly ColorStyle RowHoverColor = new ThemedColorStyle(ThemeColorStyle.RowHover, _rowColor.MultiplyAlpha(RowHoverAlpha), true);
        public static readonly ColorStyle RowHighlightColor = new ThemedColorStyle(ThemeColorStyle.RowHighlight, _rowColor.MultiplyAlpha(RowHighlightAlpha), true);
        public static readonly ColorStyle RowDisabledColor = new ThemedColorStyle(ThemeColorStyle.RowDisabled, _rowColor.MultiplyAlpha(RowDisabledAlpha), true);

        public static readonly ColorStyle SubmitColor = new ThemedColorStyle(ThemeColorStyle.Submit, _submitColor.MultiplyAlpha(0.65), true);
        public static readonly ColorStyle SubmitHoverColor = new ThemedColorStyle(ThemeColorStyle.SubmitHover, _submitColor.MultiplyAlpha(0.8), true);
        public static readonly ColorStyle SubmitHighlightColor = new ThemedColorStyle(ThemeColorStyle.SubmitHighlight, _submitColor.MultiplyAlpha(0.5), true);
        public static readonly ColorStyle SubmitDisabledColor = new ThemedColorStyle(ThemeColorStyle.SubmitDisabled, _submitColor.MultiplyAlpha(0.4), true);

        public static readonly ColorStyle CancelColor = new ThemedColorStyle(ThemeColorStyle.Cancel, _cancelColor.MultiplyAlpha(0.65), true);
        public static readonly ColorStyle CancelHoverColor = new ThemedColorStyle(ThemeColorStyle.CancelHover, _cancelColor.MultiplyAlpha(0.8), true);
        public static readonly ColorStyle CancelHighlightColor = new ThemedColorStyle(ThemeColorStyle.CancelHighlight, _cancelColor.MultiplyAlpha(0.5), true);
        public static readonly ColorStyle CancelDisabledColor = new ThemedColorStyle(ThemeColorStyle.CancelDisabled, _cancelColor.MultiplyAlpha(0.4), true);

#else
        public static readonly ColorStyle MessageRowColor = new ThemedColorStyle(ThemeColorStyle.MessageRow, Secondary.WithSaturation(RowAlpha));

        public static readonly ColorStyle RowColor = new ThemedColorStyle(ThemeColorStyle.Row, Secondary);
        public static readonly ColorStyle RowHoverColor = new ThemedColorStyle(ThemeColorStyle.RowHover, Secondary.WithSaturation(RowAlpha));
        public static readonly ColorStyle RowHighlightColor = new ThemedColorStyle(ThemeColorStyle.RowHighlight, Secondary.WithLuminosity(RowHoverAlpha));
        public static readonly ColorStyle RowDisabledColor = new ThemedColorStyle(ThemeColorStyle.RowDisabled, Secondary.WithSaturation(RowDisabledAlpha));

        public static readonly ColorStyle SubmitColor = new ThemedColorStyle(ThemeColorStyle.Submit, _submitColor);
        public static readonly ColorStyle SubmitHoverColor = new ThemedColorStyle(ThemeColorStyle.SubmitHover, _submitColor.WithSaturation(RowAlpha));
        public static readonly ColorStyle SubmitHighlightColor = new ThemedColorStyle(ThemeColorStyle.SubmitHighlight, _submitColor.WithLuminosity(RowHoverAlpha));
        public static readonly ColorStyle SubmitDisabledColor = new ThemedColorStyle(ThemeColorStyle.SubmitDisabled, _submitColor.WithSaturation(RowDisabledAlpha));

        public static readonly ColorStyle CancelColor = new ThemedColorStyle(ThemeColorStyle.Cancel, _cancelColor);
        public static readonly ColorStyle CancelHoverColor = new ThemedColorStyle(ThemeColorStyle.CancelHover, _cancelColor.WithSaturation(RowAlpha));
        public static readonly ColorStyle CancelHighlightColor = new ThemedColorStyle(ThemeColorStyle.CancelHighlight, _cancelColor.WithLuminosity(RowHoverAlpha));
        public static readonly ColorStyle CancelDisabledColor = new ThemedColorStyle(ThemeColorStyle.CancelDisabled, _cancelColor.WithSaturation(RowDisabledAlpha));
#endif

        public static readonly FontStyle TextFont = new ThemedFontStyle(ThemeFontStyle.Text, FontWeight.Light, 16);
        public static readonly FontStyle SmallFont = new ThemedFontStyle(ThemeFontStyle.Small, FontWeight.Light, 14);
        public static readonly FontStyle DetailFont = new ThemedFontStyle(ThemeFontStyle.Detail, FontWeight.Regular, 12);
        public static readonly FontStyle MicroFont = new ThemedFontStyle(ThemeFontStyle.Micro, FontWeight.Regular, 10);

        public static readonly FontStyle RowTitleFont = new ThemedFontStyle(ThemeFontStyle.RowTitle, FontWeight.Medium, 26);
        public static readonly FontStyle RowHeaderFont = new ThemedFontStyle(ThemeFontStyle.RowHeader, FontWeight.Regular, 20);
        public static readonly FontStyle RowFooterFont = new ThemedFontStyle(ThemeFontStyle.RowFooter, FontWeight.Regular, 16);
        public static readonly FontStyle RowFont = new ThemedFontStyle(ThemeFontStyle.Row, FontWeight.Light, 18);
        public static readonly FontStyle RowIconFont = new ThemedFontStyle(ThemeFontStyle.RowIcon, FontWeight.Regular, 20);
    }
}
