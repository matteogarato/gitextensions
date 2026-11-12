using System.ComponentModel.Composition;
using Git.hub;
using GitCommands.Config;
using GitCommands.Git;
using GitCommands.Remotes;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Plugins.GitHub3.Properties;
using GitExtUtils.GitUI;
using GitUIPluginInterfaces;
using GitUIPluginInterfaces.RepositoryHosts;
using ResourceManager;

namespace GitExtensions.Plugins.GitHub3;

internal static class GitHubLoginInfo
{
    private static string? _username;

    public static string? Username
    {
        get
        {
            if (_username == "")
            {
                return null;
            }

            if (_username is not null)
            {
                return _username;
            }

            try
            {
                var user = GitHub3Plugin.GitHub.GetCurrentUser();
                if (user is not null)
                {
                    _username = user.Login;
                    return _username;
                }

                _username = "";

                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    public static string OAuthToken
    {
        get => GitHub3Plugin.Instance.PersonalAccessToken.ValueOrDefault(GitHub3Plugin.Instance.Settings);
        set
        {
            _username = null;
            GitHub3Plugin.Instance.PersonalAccessToken[GitHub3Plugin.Instance.Settings] = value;
            GitHub3Plugin.GitHub.SetOAuth2Token(value);
        }
    }
}

[Export(typeof(IGitPlugin))]
[Export(typeof(IRepositoryHostPlugin))]
[Export(typeof(IGitPluginForCommit))]
public class GitHub3Plugin : GitPluginBase, IRepositoryHostPlugin, IGitPluginForCommit
{
    public static string GitHubAuthorizationRelativeUrl = "authorizations";
    public static string UpstreamConventionName = "upstream";

    internal static GitHub3Plugin Instance = null!;
    internal static Client? GitHub;

    internal static GitHubRemoteParser GitHubRemoteParser = new();
    public readonly StringSetting GitHubHost = new("GitHub (Enterprise) hostname", "github.com");
    public readonly StringSetting PersonalAccessToken = new("OAuth Token", "Personal Access Token", "");
    private readonly List<string> _currentMessages = [];
    private readonly TranslationString _error = new("Error");
    private readonly TranslationString _generateToken = new("Generate a GitHub personal access token");

    private readonly BoolSetting _issueCommitMessageHelperEnabled =
        new("IssueCommitMessageHelperEnabled", "Enable commit message issue helper", true);

    private readonly NumberSetting<int> _issueCommitMessageHelperMaxCount =
        new("IssueCommitMessageHelperMaxCount", "Maximum number of issues retrieved", 10);

    private readonly TranslationString _manageToken = new("Manage GitHub personal access token");
    private readonly TranslationString _noAssignedIssues = new("No assigned GitHub issues found");
    private readonly TranslationString _noTokenError = new("No GitHub personal access token (PAT) defined");

    private readonly TranslationString _noteRestartNeeded =
        new("Note: Git Extensions need to be restarted so that the token is taken into account.");

    private readonly TranslationString _openLinkFailed = new("Fail to open the link. Reason: ");

    private readonly TranslationString _tokenAlreadyExist =
        new(
            "You already have an personal access token. To get a new one, delete your old one in Plugins > Plugin Settings first.");

    private readonly TranslationString _viewInWebSite = new("View in {0}");

    private IGitUiCommands? _currentGitUiCommands;
    private IReadOnlyList<IHostedRemote>? _hostedRemotesForModule;

    public GitHub3Plugin() : base(true)
    {
        Id = new Guid("2EC3E1F0-EF37-413F-BEA5-B8FE1F9C505C");
        Name = "GitHub";
        Translate(AppSettings.CurrentTranslation);

        Instance ??= this;

        Icon = Resources.IconGitHub;
    }

    public string GitHubApiEndpoint
    {
        get => $"https://api.{GitHubHost.ValueOrDefault(Settings)}";
    }

    public string GitHubEndpoint
    {
        get => $"https://{GitHubHost.ValueOrDefault(Settings)}";
    }

    internal static Client GitHub
    {
        get => _gitHub ??= new(Instance.GitHubApiEndpoint);
    }

    public override IEnumerable<ISetting> GetSettings()
    {
        yield return PersonalAccessToken;

        LinkLabel generateTokenLink = new() { Text = _generateToken.Text };
        generateTokenLink.Click += GenerateTokenLink_Click;
        yield return new PseudoSetting(generateTokenLink);

        LinkLabel manageTokenLink = new() { Text = _manageToken.Text };
        manageTokenLink.Click += ManageTokenLink_Click;
        yield return new PseudoSetting(manageTokenLink);

        yield return new PseudoSetting(_noteRestartNeeded.Text);

        yield return _issueCommitMessageHelperEnabled;

        yield return _issueCommitMessageHelperMaxCount;
    }

    public override void Register(IGitUiCommands gitUiCommands)
    {
        _currentGitUiCommands = gitUiCommands;
        if (!string.IsNullOrEmpty(GitHubLoginInfo.OAuthToken))
        {
            GitHub.SetOAuth2Token(GitHubLoginInfo.OAuthToken);
        }

        gitUiCommands.PreCommit += GitUiCommands_PreCommit;
        gitUiCommands.PostCommit += GitUiCommands_PostCommit;
    }

    public override bool Execute(GitUiEventArgs args)
    {
        if (string.IsNullOrEmpty(GitHubLoginInfo.OAuthToken))
        {
            args.GitUiCommands.StartSettingsDialog(this);
        }
        else
        {
            MessageBoxes.ShowError(args.OwnerForm, _tokenAlreadyExist.Text, _error.Text);
        }

        return false;
    }

    // --

    IReadOnlyList<IHostedRepository> IRepositoryHostPlugin.SearchForRepository(string search)
    {
        return GitHub.SearchRepositories(search).Select(repo => (IHostedRepository)new GitHubRepo(repo)).ToList();
    }

    IReadOnlyList<IHostedRepository> IRepositoryHostPlugin.GetRepositoriesOfUser(string user)
    {
        return GitHub.GetRepositories(user).Select(repo => (IHostedRepository)new GitHubRepo(repo)).ToList();
    }

    IHostedRepository IRepositoryHostPlugin.GetRepository(string user, string repositoryName)
    {
        return new GitHubRepo(GitHub.GetRepository(user, repositoryName));
    }

    IReadOnlyList<IHostedRepository> IRepositoryHostPlugin.GetMyRepos()
    {
        return GitHub.GetRepositories().Select(repo => (IHostedRepository)new GitHubRepo(repo)).ToList();
    }

    bool IRepositoryHostPlugin.ConfigurationOk
    {
        get => !string.IsNullOrEmpty(GitHubLoginInfo.OAuthToken);
    }

    string? IRepositoryHostPlugin.OwnerLogin
    {
        get => GitHub.GetCurrentUser()?.Login;
    }

    async Task<string?> IRepositoryHostPlugin.AddUpstreamRemoteAsync()
    {
        Validates.NotNull(_currentGitUiCommands);

        var gitModule = _currentGitUiCommands.Module;
        var hostedRemote = GetHostedRemotesForModule().FirstOrDefault(r => r.IsOwnedByMe);
        if (hostedRemote is null)
        {
            return null;
        }

        var hostedRepository = hostedRemote.GetHostedRepository();
        if (!hostedRepository.IsAFork)
        {
            return null;
        }

        if ((await gitModule.GetRemotesAsync()).Any(r =>
                r.Name == UpstreamConventionName || r.FetchUrl == hostedRepository.ParentUrl))
        {
            return null;
        }

        gitModule.AddRemote(UpstreamConventionName, hostedRepository.ParentUrl);
        return UpstreamConventionName;
    }

    bool IRepositoryHostPlugin.GitModuleIsRelevantToMe()
    {
        return _currentGitUiCommands?.Module is not null && GetHostedRemotes().Any();
    }

    /// <summary>
    ///     Returns all relevant github-remotes for the current working directory
    /// </summary>
    IReadOnlyList<IHostedRemote> IRepositoryHostPlugin.GetHostedRemotesForModule()
    {
        return _currentGitUiCommands?.Module is null ? [] : GetHostedRemotes().ToArray();
    }

    void IRepositoryHostPlugin.ConfigureContextMenu(ContextMenuStrip contextMenu)
    {
        const string hostedRemoteMenuItem = "HostedRemoteMenuItem";

        for (var i = contextMenu.Items.Count - 1; i >= 0; i--)
        {
            var item = contextMenu.Items[i];
            if (item is ToolStripMenuItem tsmi && tsmi.Tag as string == hostedRemoteMenuItem)
            {
                contextMenu.Items.RemoveAt(i);
            }
        }

        _hostedRemotesForModule = GetHostedRemotesForModule();
        if (_hostedRemotesForModule.Count == 0)
        {
            return;
        }

        ToolStripMenuItem toolStripMenuItem =
            new(string.Format(_viewInWebSite.Text, Name), Icon) { Tag = hostedRemoteMenuItem };
        contextMenu.Items.Add(toolStripMenuItem);

        foreach (var hostedRemote in _hostedRemotesForModule.OrderBy(r => r.Data))
        {
            var toolStripItem = toolStripMenuItem.DropDownItems.Add(hostedRemote.DisplayData);
            toolStripItem.Click += (s, e) =>
            {
                if (contextMenu.Tag is GitBlameContext blameContext)
                {
                    OsShellUtil.OpenUrlInDefaultBrowser(hostedRemote.GetBlameUrl(
                        blameContext.BlameId.ToString(),
                        blameContext.FileName,
                        blameContext.LineIndex + 1));
                }
            };
        }
    }

    private void GenerateTokenLink_Click(object? sender, EventArgs e)
    {
        OpenLink(
            $"https://{GitHubHost.ValueOrDefault(Instance.Settings)}/settings/tokens/new?description=Token%20for%20GitExtensions&scopes=repo,public_repo");
    }

    private void ManageTokenLink_Click(object? sender, EventArgs e)
    {
        OpenLink($"https://{GitHubHost.ValueOrDefault(Instance.Settings)}/settings/tokens");
    }

    private void OpenLink(string url)
    {
        try
        {
            OsShellUtil.OpenUrlInDefaultBrowser(url);
        }
        catch (Exception ex)
        {
            MessageBoxes.Show(null, _openLinkFailed.Text + ex.Message, string.Empty, MessageBoxButtons.OK,
                MessageBoxIcon.None);
        }
    }

    private void GitUiCommands_PreCommit(object? sender, GitUiEventArgs e)
    {
        if (!_issueCommitMessageHelperEnabled.ValueOrDefault(Settings))
        {
            return;
        }

        if (string.IsNullOrEmpty(GitHubLoginInfo.OAuthToken))
        {
            e.GitUiCommands.AddCommitTemplate(_noTokenError.Text, () => string.Empty, Icon);
            return;
        }

        ThreadHelper.FileAndForget(async () =>
        {
            IHostedRemote[] hostedRemotes = [.. GetHostedRemotes()];
            if (hostedRemotes.Length == 0)
            {
                return;
            }

            var issues = GitHub.GetAssignedIssues();

            if (issues?.All(i => i.Number == 0) ?? true)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                e.GitUiCommands.AddCommitTemplate(_noAssignedIssues.Text, () => string.Empty, Icon);
                return;
            }

            Issue[] recentUserIssues =
            [
                .. Enumerable.Where<Issue>(issues,
                        i => i.Number != 0 && hostedRemotes.Any(r =>
                            r.Owner == i.Repository.Owner.Login && r.RemoteRepositoryName == i.Repository.Name))
                    .OrderByDescending(i => i.UpdatedAt)
                    .Take(_issueCommitMessageHelperMaxCount.ValueOrDefault(Settings))
            ];

            var multipleRemotes = hostedRemotes.Length > 1;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            foreach (var issue in recentUserIssues)
            {
                var remoteData = multipleRemotes
                    ? $" ({issue.Repository.Owner.Login}/{issue.Repository.Name})"
                    : string.Empty;
                var key = $"{issue.Number}: {issue.Title}{remoteData}";
                _currentMessages.Add(key);
                e.GitUiCommands.AddCommitTemplate(key, () => GetIssueDescription(issue), Icon);
            }

            static string GetIssueDescription(Issue issue)
            {
                return $"""

                        Fixes #{issue.Number} : {issue.Title}

                        {issue.Body}

                        """;
            }
        });
    }

    private void GitUiCommands_PostCommit(object? sender, GitUiEventArgs e)
    {
        if (_currentMessages.Count == 0)
        {
            return;
        }

        foreach (var key in _currentMessages)
        {
            e.GitUiCommands.RemoveCommitTemplate(key);
        }

        _currentMessages.Clear();
    }

    private IEnumerable<IHostedRemote> GetHostedRemotes()
    {
        HashSet<IHostedRemote> set = [];

        var gitModule = _currentGitUiCommands?.Module;
        if (gitModule is null)
        {
            yield break;
        }

        foreach (var remote in gitModule.GetRemoteNames())
        {
            var url = gitModule.GetSetting(string.Format(SettingKeyString.RemoteUrl, remote));

            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            if (GitHubRemoteParser.TryExtractGitHubDataFromRemoteUrl(url, out var owner, out var repository))
            {
                GitHubHostedRemote hostedRemote = new(remote, owner, repository, url);

                if (set.Add(hostedRemote))
                {
                    yield return hostedRemote;
                }
            }
        }
    }
}
