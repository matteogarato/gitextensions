using GitCommands.Git;
using GitCommands.Settings;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using ResourceManager;

namespace GitUI.CommandsDialogs;

public partial class FormInit : GitExtensionsDialog
{
    private readonly TranslationString _chooseDirectory =
        new("Please choose a directory.");

    private readonly TranslationString _chooseDirectoryCaption =
        new("Choose directory");

    private readonly TranslationString _chooseDirectoryNotFile =
        new("Cannot initialize a new repository on a file.\nPlease choose a directory.");

    private readonly EventHandler<GitModuleEventArgs>? _gitModuleChanged;

    private readonly TranslationString _initMsgBoxCaption =
        new("Create new repository");

    /// <summary>
    ///     Initializes a new instance of the <see cref="FormInit" /> class.
    /// </summary>
    /// <param name="commands">
    ///     The <see cref="IGitUiCommands" /> instance, mainly in its role as
    ///     <see cref="IServiceProvider" />.
    /// </param>
    /// <param name="dir">The initial directory path.</param>
    /// <param name="gitModuleChanged">The event handler for Git module changes.</param>
    public FormInit(IGitUiCommands commands, string dir, EventHandler<GitModuleEventArgs>? gitModuleChanged)
        : base(commands, true)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        _gitModuleChanged = gitModuleChanged;
        InitializeComponent();

        InitializeComplete();

        var repositoryHistory =
            ThreadHelper.JoinableTaskFactory.Run(RepositoryHistoryManager.Locals.LoadRecentHistoryAsync);
        _NO_TRANSLATE_Directory.DataSource = repositoryHistory;
        _NO_TRANSLATE_Directory.DisplayMember = nameof(Repository.Path);
        _NO_TRANSLATE_Directory.SelectedIndex = -1;
        _NO_TRANSLATE_Directory.Text = string.IsNullOrEmpty(dir) ? AppSettings.DefaultCloneDestinationPath : dir;
        _NO_TRANSLATE_Directory.ResizeDropDownWidth();
    }

    private void InitClick(object sender, EventArgs e)
    {
        var directoryPath = _NO_TRANSLATE_Directory.Text;

        if (!IsRootedDirectoryPath(directoryPath))
        {
            MessageBoxes.Show(this, _chooseDirectory.Text, _chooseDirectoryCaption.Text, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        if (File.Exists(directoryPath))
        {
            MessageBoxes.Show(this, _chooseDirectoryNotFile.Text, TranslatedStrings.Error, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        GitModule module = new(UiCommands.GetRequiredService<IGitExecutorProvider>(), directoryPath);

        if (!Directory.Exists(module.WorkingDir))
        {
            Directory.CreateDirectory(module.WorkingDir);
        }

        MessageBoxes.Show(this, module.Init(Central.Checked, Central.Checked), _initMsgBoxCaption.Text,
            MessageBoxButtons.OK, MessageBoxIcon.Information);

        _gitModuleChanged?.Invoke(this, new GitModuleEventArgs(module));

        ThreadHelper.JoinableTaskFactory.Run(() => RepositoryHistoryManager.Locals.AddAsMostRecentAsync(directoryPath));
        Close();
    }

    private static bool IsRootedDirectoryPath(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // this is going to throw if it's an invalid path (e.g. contains special chars)
            DirectoryInfo info = new(path);

            return Path.IsPathRooted(path.Trim());
        }
        catch (Exception)
        {
            // The code in the try block is expected to throw when the input is not a valid directory path
            // OR when the user does not have the required permission.
            // In both cases we return "false" since the path is not representing a valid "usable" directory.
            // This is also the reason why we are catching all kind of exception here and not IO-related ones.
            return false;
        }
    }

    private void BrowseClick(object sender, EventArgs e)
    {
        var userSelectedPath = OsShellUtil.PickFolder(this);

        if (userSelectedPath is not null)
        {
            _NO_TRANSLATE_Directory.Text = userSelectedPath;
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(FormInit form)
    {
        public ComboBox DirectoryCombo
        {
            get => form._NO_TRANSLATE_Directory;
        }

        public bool IsRootedDirectoryPath(string path)
        {
            return FormInit.IsRootedDirectoryPath(path);
        }
    }
}
