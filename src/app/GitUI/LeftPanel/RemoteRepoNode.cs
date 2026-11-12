using System.Diagnostics;
using GitCommands.Git;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Git;
using GitUI.Properties;
using GitUIPluginInterfaces.RepositoryHosts;

namespace GitUI.LeftPanel;

[DebuggerDisplay("Remote = {_remote.Name}, FullPath = {FullPath}")]
internal sealed class RemoteRepoNode(
    Tree tree,
    string fullPath,
    IConfigFileRemoteSettingsManager remotesManager,
    Remote remote,
    bool isEnabled)
    : BaseRevisionNode(tree, fullPath, true)
{
    private readonly Remote _remote = remote;

    public bool Enabled { get; } = isEnabled;

    public bool IsRemoteUrlUsingHttp
    {
        get => _remote.FetchUrl.IsUrlUsingHttp();
    }

    public bool Fetch()
    {
        Trace.Assert(Enabled);
        return DoFetch();
    }

    public bool Prune()
    {
        Trace.Assert(Enabled);
        return DoPrune();
    }

    public void OpenRemoteUrlInBrowser()
    {
        if (!IsRemoteUrlUsingHttp)
        {
            return;
        }

        OsShellUtil.OpenUrlInDefaultBrowser(_remote.FetchUrl);
    }

    public void Enable(bool fetch)
    {
        Trace.Assert(!Enabled);
        remotesManager.ToggleRemoteState(Name, false);
        if (fetch)
        {
            // DoFetch invokes UICommands.RepoChangedNotifier.Notify
            DoFetch();
        }
        else
        {
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    public void Disable()
    {
        Trace.Assert(Enabled);
        remotesManager.ToggleRemoteState(Name, true);
        UiCommands.RepoChangedNotifier.Notify();
    }

    public override void ApplyStyle()
    {
        base.ApplyStyle();

        TreeViewNode.ToolTipText = _remote.PushUrls.Count != 1 && _remote.FetchUrl != _remote.PushUrls[0]
            ? $"Fetch: {_remote.FetchUrl}\nPush: {string.Join("\n", _remote.PushUrls.ToArray())}"
            : _remote.FetchUrl;

        string imageKey;
        if (_remote.FetchUrl.Contains("github.com"))
        {
            imageKey = nameof(Images.GitHub);
        }
        else if (_remote.FetchUrl.Contains("bitbucket."))
        {
            imageKey = nameof(Images.BitBucket);
        }
        else if (_remote.FetchUrl.Contains("visualstudio.com") || _remote.FetchUrl.Contains("dev.azure.com"))
        {
            imageKey = nameof(Images.VisualStudioTeamServices);
        }
        else
        {
            imageKey = nameof(Images.Remote);
        }

        TreeViewNode.ImageKey = TreeViewNode.SelectedImageKey = imageKey;
    }

    internal override void OnDoubleClick()
    {
        PopupManageRemotesForm();
    }

    internal void PopupManageRemotesForm()
    {
        ((RemoteBranchTree)Tree).PopupManageRemotesForm(FullPath);
    }

    private bool DoFetch()
    {
        UiCommands.StartPullDialogAndPullImmediately(
            out var pullCompleted,
            TreeViewNode.TreeView,
            remote: FullPath,
            pullAction: GitPullAction.Fetch);
        return pullCompleted;
    }

    private bool DoPrune()
    {
        UiCommands.StartPullDialogAndPullImmediately(
            out var pullCompleted,
            TreeViewNode.TreeView,
            remote: FullPath,
            pullAction: GitPullAction.FetchPruneAll);
        return pullCompleted;
    }
}
