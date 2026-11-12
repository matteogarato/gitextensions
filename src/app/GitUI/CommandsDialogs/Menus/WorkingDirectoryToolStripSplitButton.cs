using GitCommands;
using GitCommands.Settings;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Translations;
using GitExtUtils.GitUI;
using GitUI.CommandsDialogs.BrowseDialog;
using GitUI.Properties;
using ResourceManager;
using ResourceManager.Hotkey;

namespace GitUI.CommandsDialogs.Menus;

/// <summary>
///     Represents a split button that contains the recent repositories.
/// </summary>
internal sealed class WorkingDirectoryToolStripSplitButton : ToolStripSplitButton, ITranslate
{
    private static readonly TranslationString _noWorkingFolderText = new("No working directory");
    private static readonly TranslationString _configureWorkingDirMenu = new("Co&nfigure this menu...");
    private static readonly TranslationString _repositorySearchPlaceholder = new("Search repositories...");

    private static readonly TranslationString _toolTip = new("""
                                                             Change working directory
                                                             Left click opens the drop-down menu.
                                                             Then hold Ctrl in order to open the selected repository in a new instance.
                                                             Right click starts the "Open repository" dialog.
                                                             """);

    private Implementation? _implementation;

    public WorkingDirectoryToolStripSplitButton()
    {
        Name = nameof(WorkingDirectoryToolStripSplitButton);

        Image = Resources.RepoOpen;
        ImageAlign = ContentAlignment.MiddleLeft;
        TextAlign = ContentAlignment.MiddleLeft;
    }

    void ITranslate.AddTranslationItems(ITranslation translation)
    {
        TranslationUtils.AddTranslationItemsFromFields("FormBrowse", this, translation);
    }

    void ITranslate.TranslateItems(ITranslation translation)
    {
        TranslationUtils.TranslateItemsFromFields("FormBrowse", this, translation);
    }

    /// <summary>
    ///     Initializes the menu item.
    /// </summary>
    /// <param name="getUiCommands">The method that returns the current instance of UI commands.</param>
    public void Initialize(Func<IGitUiCommands> getUiCommands, IRepositoryHistoryUiService repositoryHistoryUiService,
        StartToolStripMenuItem startToolStripMenuItem, ToolStripMenuItem closeToolStripMenuItem)
    {
        Translator.Translate(this, AppSettings.CurrentTranslation);

        _implementation = new Implementation(this, getUiCommands, repositoryHistoryUiService, startToolStripMenuItem,
            closeToolStripMenuItem);
    }

    /// <summary>Updates the text shown on the combo button itself.</summary>
    public void RefreshContent()
    {
        // If the component is not initialized, no point doing anything.
        _implementation?.RefreshContent(this);
    }

    public void RefreshShortcutKeys(IEnumerable<HotkeyCommand>? hotkeys)
    {
        _implementation?.RefreshShortcutKeys(hotkeys);
    }

    private sealed class Implementation
    {
        // This is used as Tag in order to mark controls which are to be excluded from the filtering considerations.
        private static readonly object _excludeFromFilterMarker = new();
        private readonly ToolStripMenuItem _closeToolStripMenuItem;

        private readonly Func<IGitUiCommands> _getUiCommands;

        /// <summary>
        ///     The current instance of the <see cref="RepositoryHistoryUiService" />.
        /// </summary>
        private readonly IRepositoryHistoryUiService _repositoryHistoryUiService;

        private readonly StartToolStripMenuItem _startToolStripMenuItem;

        private readonly ToolStripMenuItem _tsmiCategorisedRepos;
        private readonly ToolStripMenuItem _tsmiCloseRepo;
        private readonly ToolStripMenuItem _tsmiOpenLocalRepository;
        private readonly ToolStripMenuItem _tsmiRecentReposSettings;
        private readonly ToolStripTextBox _txtFilter = new();

        internal Implementation(
            ToolStripSplitButton button,
            Func<IGitUiCommands> getUiCommands,
            IRepositoryHistoryUiService repositoryHistoryUiService,
            StartToolStripMenuItem startToolStripMenuItem,
            ToolStripMenuItem closeToolStripMenuItem)
        {
            button.ButtonClick += (s, e) => button.ShowDropDown();
            button.DropDownOpening += (s, e) => FillDropDown(button);
            button.MouseUp += MouseUpHandler;
            button.ToolTipText = _toolTip.Text;

            _getUiCommands = getUiCommands;
            _repositoryHistoryUiService = repositoryHistoryUiService;
            _startToolStripMenuItem = startToolStripMenuItem;
            _closeToolStripMenuItem = closeToolStripMenuItem;

            _txtFilter.Tag = _excludeFromFilterMarker;

            var filterTextbox = _txtFilter.TextBox;
            filterTextbox.PlaceholderText = _repositorySearchPlaceholder.Text;
            filterTextbox.TextChanged += (s, e) =>
            {
                if (_txtFilter.GetCurrentParent() is null)
                {
                    // We are clearing the textbox while opening the dropdown
                    return;
                }

                // Default items include:
                //  1. filter
                //  2. separator
                //  3. favourite items
                //      ... recent items
                //  4. "Open repo..."
                //  5. "Close repo..."
                //  6. separator
                //  7. "Configure menu"
                const int defaultItemCount = 7;
                if (button.DropDown.Items.Count <= defaultItemCount)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(filterTextbox.Text))
                {
                    foreach (ToolStripItem item in button.DropDown.Items)
                    {
                        item.Visible = true;
                    }

                    return;
                }

                foreach (ToolStripItem item in button.DropDown.Items)
                {
                    if (item is ToolStripSeparator || item.Tag == _excludeFromFilterMarker)
                    {
                        continue;
                    }

                    item.Visible =
                        item.Text?.Contains(filterTextbox.Text, StringComparison.CurrentCultureIgnoreCase) is true;
                }
            };

            // Initialize toolstip menu items
            // ----------------------------------------
            _tsmiCategorisedRepos =
                new ToolStripMenuItem(_startToolStripMenuItem.FavouriteRepositoriesMenuItem.Text,
                    _startToolStripMenuItem.FavouriteRepositoriesMenuItem.Image) { Tag = _excludeFromFilterMarker };

            _tsmiOpenLocalRepository =
                new ToolStripMenuItem(_startToolStripMenuItem.OpenRepositoryMenuItem.Text,
                    _startToolStripMenuItem.OpenRepositoryMenuItem.Image)
                {
                    ShortcutKeyDisplayString = _startToolStripMenuItem.OpenRepositoryMenuItem.ShortcutKeyDisplayString,
                    Tag = _excludeFromFilterMarker
                };
            _tsmiOpenLocalRepository.Click += (s, e) => _startToolStripMenuItem.OpenRepositoryMenuItem.PerformClick();

            _tsmiCloseRepo = new ToolStripMenuItem(_closeToolStripMenuItem.Text, _closeToolStripMenuItem.Image)
            {
                ShortcutKeyDisplayString = _closeToolStripMenuItem.ShortcutKeyDisplayString,
                Tag = _excludeFromFilterMarker
            };
            _tsmiCloseRepo.Click += (hs, he) => _closeToolStripMenuItem.PerformClick();

            _tsmiRecentReposSettings = new ToolStripMenuItem(_configureWorkingDirMenu.Text)
            {
                Tag = _excludeFromFilterMarker
            };
            _tsmiRecentReposSettings.Click += (hs, he) =>
            {
                using (FormRecentReposSettings frm = new())
                {
                    frm.ShowDialog(ActiveOrOpenForm);
                }

                RefreshContent(button);
            };
        }

        /// <summary>
        ///     Gets the current instance of the git module.
        /// </summary>
        private IGitModule Module
        {
            get => UiCommands.Module;
        }

        /// <summary>
        ///     Gets the active or the first open form.
        /// </summary>
        private static Form? ActiveOrOpenForm
        {
            get => Form.ActiveForm ?? (Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
        }

        /// <summary>
        ///     Gets the current instance of the UI commands.
        /// </summary>
        private IGitUiCommands UiCommands
        {
            get => _getUiCommands();
        }

        private void FillDropDown(ToolStripDropDownItem button)
        {
            // Do not rebuild while the dropdown is open — Clear() would close it.
            if (button.DropDown.Visible)
            {
                return;
            }

            button.DropDown.SuspendLayout();
            try
            {
                button.DropDown.Items.Clear();

                _txtFilter.Text = string.Empty;

                button.DropDown.Items.Add(_txtFilter);
                button.DropDown.Items.Add(new ToolStripSeparator());

                _repositoryHistoryUiService.PopulateFavouriteRepositoriesMenu(_tsmiCategorisedRepos);
                if (_tsmiCategorisedRepos.DropDownItems.Count > 0)
                {
                    button.DropDown.Items.Add(_tsmiCategorisedRepos);
                }

                _repositoryHistoryUiService.PopulateRecentRepositoriesMenu(button);

                button.DropDown.Items.Add(new ToolStripSeparator());
                button.DropDown.Items.Add(_tsmiOpenLocalRepository);
                button.DropDown.Items.Add(_tsmiCloseRepo);
                button.DropDown.Items.Add(new ToolStripSeparator());
                button.DropDown.Items.Add(_tsmiRecentReposSettings);
            }
            finally
            {
                button.DropDown.ResumeLayout();

                var maxWidth = button.DropDownItems.Cast<ToolStripItem>()
                    .Max(item => item == _txtFilter ? 0 : item.Width);
                const int paddingToAvoidGrowth = 60;
                _txtFilter.Size = new Size(maxWidth - DpiUtil.Scale(paddingToAvoidGrowth), _txtFilter.Height);
            }
        }

        private void MouseUpHandler(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                _startToolStripMenuItem.OpenRepositoryMenuItem.PerformClick();
            }
        }

        internal void RefreshContent(ToolStripSplitButton button)
        {
            if (ActiveOrOpenForm is not { } graphicsForm)
            {
                // The component is unparented, no point doing anything.
                return;
            }

            var path = Module.WorkingDir;

            // It appears at times Module.WorkingDir path is an empty string,
            // this caused issues like https://github.com/gitextensions/gitextensions/issues/4874.
            if (string.IsNullOrWhiteSpace(path))
            {
                button.Text = _noWorkingFolderText.Text;
                return;
            }

            var recentRepositoryHistory =
                ThreadHelper.JoinableTaskFactory.Run(() => RepositoryHistoryManager.Locals.AddAsMostRecentAsync(path));

            List<RecentRepoInfo> pinnedRepos = [];
            using var graphics = graphicsForm.CreateGraphics();
            RecentRepoSplitter splitter = new() { MeasureFont = button.Font };

            splitter.SplitRecentRepos(recentRepositoryHistory, pinnedRepos, pinnedRepos);

            var ri =
                pinnedRepos.Find(e => e.Repo.Path.Equals(path, StringComparison.InvariantCultureIgnoreCase));

            button.Text = PathUtil.GetDisplayPath(ri?.Caption ?? path);

            if (AppSettings.RecentReposComboMinWidth > 0)
            {
                button.AutoSize = false;
                var captionWidth = graphics.MeasureString(button.Text, button.Font).Width;
                captionWidth = captionWidth + button.DropDownButtonWidth + 5;
                button.Width = Math.Max(AppSettings.RecentReposComboMinWidth, (int)captionWidth);
            }
            else
            {
                button.AutoSize = true;
            }
        }

        internal void RefreshShortcutKeys(IEnumerable<HotkeyCommand>? hotkeys)
        {
            _tsmiOpenLocalRepository.ShortcutKeyDisplayString = hotkeys.GetShortcutDisplay(FormBrowse.Command.OpenRepo);
            _tsmiCloseRepo.ShortcutKeyDisplayString = hotkeys.GetShortcutDisplay(FormBrowse.Command.CloseRepository);
        }
    }
}
