using System.Diagnostics;
using GitCommands.Settings;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using ResourceManager;

namespace GitUI.CommandsDialogs.SettingsDialog.Pages;

public partial class ColorsSettingsPage : SettingsPageWithHeader, IColorsSettingsPage
{
    private static readonly TranslationString _formatBuiltinThemeName =
        new("{0}");

    private static readonly TranslationString _formatUserDefinedThemeName =
        new("{0}, user-defined");

    private readonly ColorsSettingsPageController _controller;

    public ColorsSettingsPage(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        InitializeComponent();
        sbOpenThemeFolder.AutoSize = false;

        _NO_TRANSLATE_cbSelectTheme.SelectedIndexChanged += ComboBoxTheme_SelectedIndexChanged;
        chkUseSystemVisualStyle.CheckedChanged += ChkUseSystemVisualStyle_CheckedChanged;
        chkColorblind.CheckedChanged += ChkColorblind_CheckedChanged;
        _controller = new ColorsSettingsPageController(this, new ThemeRepository(), new ThemePathProvider());
        InitializeComplete();
    }

    ThemeId IColorsSettingsPage.SelectedThemeId
    {
        get => ((FormattedThemeId)_NO_TRANSLATE_cbSelectTheme.SelectedItem!).ThemeId;
        set
        {
            FormattedThemeId formattedThemeId = new(value);
            var index = _NO_TRANSLATE_cbSelectTheme.Items.IndexOf(formattedThemeId);
            if (index < 0)
            {
                // Handle case when selected theme is missing gracefully.
                // It may happen in a following scenario:
                // - user creates custom theme and selects it in this settings page
                // - user saves app settings
                // - user deletes the file with custom theme
                // on first install; suppress MessageBox
                var theme = formattedThemeId.ToString();
                if (!string.IsNullOrWhiteSpace(theme))
                {
                    MessageBoxes.ShowError(FindForm(), $"Theme not found: {theme}");
                }

                index = 0;
            }

            _NO_TRANSLATE_cbSelectTheme.SelectedIndex = index;
        }
    }

    string[] IColorsSettingsPage.SelectedThemeVariations
    {
        get => chkColorblind.Checked
            ? [ThemeVariations.Colorblind]
            : ThemeVariations.None;

        set => chkColorblind.Checked = value.Contains(ThemeVariations.Colorblind);
    }

    bool IColorsSettingsPage.UseSystemVisualStyle
    {
        get => chkUseSystemVisualStyle.Checked;
        set => chkUseSystemVisualStyle.Checked = value;
    }

    bool IColorsSettingsPage.LabelRestartIsNeededVisible
    {
        get => lblRestartNeeded.Visible;
        set => lblRestartNeeded.Visible = value;
    }

    bool IColorsSettingsPage.IsChoosingVisualStyleEnabled
    {
        get => chkUseSystemVisualStyle.Enabled;
        set => chkUseSystemVisualStyle.Enabled = value;
    }

    void IColorsSettingsPage.ShowThemeLoadingErrorMessage(ThemeId themeId, string[] variations, Exception ex)
    {
        Trace.WriteLine($"Failed to load theme {themeId.Name}: {ex}");
        var variationsStr = string.Concat(variations.Select(_ => "." + _));
        var identifier = new FormattedThemeId(themeId).ToString();
        AppSettings.ThemeId = ThemeId.DefaultLight;
        MessageBoxes.ShowError(this, $"Failed to load theme {identifier}{variationsStr}: {ex.Message}"
                                     + $"{Environment.NewLine}{Environment.NewLine}See also https://github.com/gitextensions/gitextensions/wiki/Dark-Mode");
    }

    void IColorsSettingsPage.PopulateThemeMenu(IEnumerable<ThemeId> themeIds)
    {
        _NO_TRANSLATE_cbSelectTheme.Items.Clear();
        object[] formattedThemeIds =
        [
            .. themeIds
                .Select(id => new FormattedThemeId(id))
                .Cast<object>()
        ];
        _NO_TRANSLATE_cbSelectTheme.Items.AddRange(formattedThemeIds);
    }

    protected override void OnRuntimeLoad()
    {
        base.OnRuntimeLoad();

        if (!IsSettingsLoaded)
        {
            SettingsToPage();
        }
    }

    protected override void SettingsToPage()
    {
        MulticolorBranches.Checked = AppSettings.MulticolorBranches;
        chkDrawAlternateBackColor.Checked = AppSettings.RevisionGraphDrawAlternateBackColor;
        DrawNonRelativesGray.Checked = AppSettings.RevisionGraphDrawNonRelativesGray;
        DrawNonRelativesTextGray.Checked = AppSettings.RevisionGraphDrawNonRelativesTextGray;
        chkHighlightAuthored.Checked = AppSettings.HighlightAuthoredRevisions;
        chkFillRefLabels.Checked = AppSettings.FillRefLabels;
        _controller.ShowThemeSettings();

        base.SettingsToPage();
    }

    protected override void PageToSettings()
    {
        AppSettings.MulticolorBranches = MulticolorBranches.Checked;
        AppSettings.RevisionGraphDrawAlternateBackColor = chkDrawAlternateBackColor.Checked;
        AppSettings.RevisionGraphDrawNonRelativesGray = DrawNonRelativesGray.Checked;
        AppSettings.RevisionGraphDrawNonRelativesTextGray = DrawNonRelativesTextGray.Checked;
        AppSettings.HighlightAuthoredRevisions = chkHighlightAuthored.Checked;
        AppSettings.FillRefLabels = chkFillRefLabels.Checked;
        _controller.ApplyThemeSettings();

        base.PageToSettings();
    }

    private void ComboBoxTheme_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _controller.HandleSelectedThemeChanged();
    }

    private void ChkUseSystemVisualStyle_CheckedChanged(object? sender, EventArgs e)
    {
        _controller.HandleUseSystemVisualStyleChanged();
    }

    private void ChkColorblind_CheckedChanged(object? sender, EventArgs e)
    {
        _controller.HandleUseColorblindVariationChanged();
    }

    private void tsmiApplicationFolder_Click(object sender, EventArgs e)
    {
        _controller.ShowAppThemesDirectory();
    }

    private void tsmiUserFolder_Click(object sender, EventArgs e)
    {
        _controller.ShowUserThemesDirectory();
    }

    private readonly struct FormattedThemeId(ThemeId themeId)
    {
        public ThemeId ThemeId { get; } = themeId;

        public override bool Equals(object? obj)
        {
            return obj is FormattedThemeId other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return ThemeId.GetHashCode();
        }

        public static bool operator ==(FormattedThemeId left, FormattedThemeId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FormattedThemeId left, FormattedThemeId right)
        {
            return !left.Equals(right);
        }

        public override readonly string ToString()
        {
            if (ThemeId.IsBuiltin)
            {
                return string.Format(_formatBuiltinThemeName.Text, ThemeId.Name);
            }

            return string.Format(_formatUserDefinedThemeName.Text, ThemeId.Name);
        }

        private readonly bool Equals(FormattedThemeId other)
        {
            return ThemeId.Equals(other.ThemeId);
        }
    }
}
