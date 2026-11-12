using System.Text;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using ResourceManager;

namespace GitUI.CommandsDialogs;

public sealed partial class FormAddToGitIgnore : GitModuleForm
{
    private readonly TranslationString _addToLocalExcludeTitle = new("Add file(s) to .git/info/exclude");
    private readonly IFullPathResolver _fullPathResolver;

    private readonly AsyncLoader _ignoredFilesLoader = new();
    private readonly bool _localExclude;
    private readonly TranslationString _matchingFilesString = new("{0} file(s) matched");
    private readonly TranslationString _updateStatusString = new("Updating ...");

    public FormAddToGitIgnore(IGitUiCommands commands, bool localExclude, params string[] filePatterns)
        : base(commands)
    {
        _localExclude = localExclude;

        InitializeComponent();
        InitializeComplete();

        if (localExclude)
        {
            Text = _addToLocalExcludeTitle.Text;
        }

        if (filePatterns is not null)
        {
            FilePattern.Text = string.Join(Environment.NewLine, filePatterns);
        }

        _fullPathResolver = new FullPathResolver(() => Module.WorkingDir);
    }

    private string? ExcludeFile
    {
        get
        {
            if (!_localExclude)
            {
                return _fullPathResolver.Resolve(".gitignore");
            }

            return Path.Join(Module.ResolveGitInternalPath("info"), "exclude");
        }
    }

    private void AddToIgnoreClick(object sender, EventArgs e)
    {
        string[] patterns = [.. GetCurrentPatterns()];
        if (patterns.Length == 0)
        {
            Close();
            return;
        }

        try
        {
            var fileName = ExcludeFile;
            Validates.NotNull(fileName);
            FileInfoExtensions.MakeFileTemporaryWritable(fileName, x =>
            {
                StringBuilder gitIgnoreFileAddition = new();

                if (File.Exists(fileName) &&
                    !File.ReadAllText(fileName, GitModule.SystemEncoding).EndsWith(Environment.NewLine))
                {
                    gitIgnoreFileAddition.Append(Environment.NewLine);
                }

                foreach (var pattern in patterns)
                {
                    gitIgnoreFileAddition.Append(pattern);
                    gitIgnoreFileAddition.Append(Environment.NewLine);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(x)!);
                using StreamWriter writer = new(x, true, GitModule.SystemEncoding);
                writer.Write(gitIgnoreFileAddition);
            });
        }
        catch (Exception ex)
        {
            MessageBoxes.Show(this, ex.ToString(), TranslatedStrings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        Close();
    }

    private void UpdatePreviewPanel(IReadOnlyList<string> ignoredFiles)
    {
        _NO_TRANSLATE_Preview.DataSource = ignoredFiles;
        _NO_TRANSLATE_filesWillBeIgnored.Text =
            string.Format(_matchingFilesString.Text, _NO_TRANSLATE_Preview.Items.Count);
        _NO_TRANSLATE_Preview.Enabled = true;
        noMatchPanel.Visible = _NO_TRANSLATE_Preview.Items.Count == 0;
    }

    private IEnumerable<string> GetCurrentPatterns()
    {
        return FilePattern.Lines.Where(line => !string.IsNullOrEmpty(line));
    }

    private void FilePattern_TextChanged(object sender, EventArgs e)
    {
        _ignoredFilesLoader.Cancel();
        if (_NO_TRANSLATE_Preview.Enabled)
        {
            _ignoredFilesLoader.Delay = TimeSpan.FromMilliseconds(300);
            _NO_TRANSLATE_filesWillBeIgnored.Text = _updateStatusString.Text;
            _NO_TRANSLATE_Preview.DataSource = new List<string> { _updateStatusString.Text };
            _NO_TRANSLATE_Preview.Enabled = false;
        }

        _ignoredFilesLoader.LoadAsync(() => Module.GetIgnoredFiles(GetCurrentPatterns()), UpdatePreviewPanel);
    }
}
