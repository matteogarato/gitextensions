using System.Text;
using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using GitUI.Editor;
using GitUI.Editor.Diff;
using GitUI.UserControls;
using GitUI.UserControls.RevisionGrid;
using ResourceManager;

namespace GitUI;

public static partial class GitUiExtensions
{
    [GeneratedRegex(@"\n\s*(@@|##)\s+(?<file>[^#:\n]+)", RegexOptions.ExplicitCapture)]
    private static partial Regex FileNameRegex { get; }

    /// <summary>
    ///     View the changes between the revisions, if possible as a diff.
    /// </summary>
    /// <param name="fileViewer">Current FileViewer.</param>
    /// <param name="item">The FileStatusItem to present changes for.</param>
    /// <param name="line">The line to display.</param>
    /// <param name="defaultText">default text if no diff is possible.</param>
    /// <param name="openWithDiffTool">The difftool command to open with.</param>
    /// <param name="additionalCommandInfo">If the diff is range-diff, this contains the current path filter.</param>
    /// <returns>Task to view.</returns>
    public static async Task ViewChangesAsync(this FileViewer fileViewer,
        FileStatusItem? item,
        CancellationToken cancellationToken,
        int? line = null,
        string defaultText = "",
        Action? openWithDiffTool = null,
        string additionalCommandInfo = null!,
        bool forceFileView = false)
    {
        if (item?.Item.IsStatusOnly ?? false)
        {
            // Present error (e.g. parsing Git)
            await fileViewer.ViewTextAsync(item.Item.Name, item.Item.ErrorMessage ?? "",
                cancellationToken: cancellationToken);
            return;
        }

        if (item?.Item is null || item.SecondRevision?.ObjectId is null)
        {
            if (!string.IsNullOrWhiteSpace(defaultText))
            {
                await fileViewer.ViewTextAsync(item?.Item?.Name, defaultText, cancellationToken: cancellationToken);
                return;
            }

            await fileViewer.ClearAsync();
            return;
        }

        var firstId = item.FirstRevision?.ObjectId ?? item.SecondRevision.FirstParentId;

        openWithDiffTool ??= OpenWithDiffTool;

        if (forceFileView || (!item.Item.IsSubmodule && (item.Item.IsNew || firstId.IsZero ||
                                                         (!item.Item.IsDeleted && FileHelper.IsImage(item.Item.Name)))))
        {
            // View blob guid from revision, or file for worktree
            await fileViewer.ViewGitItemAsync(item, line, openWithDiffTool, cancellationToken);
            return;
        }

        if (item.Item.IsRangeDiff)
        {
            // Git range-diff has cubic runtime complexity and can be slow and memory consuming,
            // give an indication of what is going on
            var range = item.BaseA.IsZero || item.BaseB.IsZero
                ? $"{firstId}...{item.SecondRevision.ObjectId}"
                : $"{item.BaseA}..{firstId} {item.BaseB}..{item.SecondRevision.ObjectId}";
            await fileViewer.ViewTextAsync(null, $"git range-diff {range} -- {additionalCommandInfo}",
                cancellationToken: cancellationToken);

            var result = await fileViewer.Module.GetRangeDiffAsync(
                firstId,
                item.SecondRevision.ObjectId,
                item.BaseA,
                item.BaseB,
                fileViewer.GetExtraDiffArguments(true),
                additionalCommandInfo,
                true,
                RangeDiffHighlightService.GetGitCommandConfiguration(fileViewer.Module),
                cancellationToken);

            if (!result.ExitedSuccessfully)
            {
                var output =
                    $"{result.StandardError}{Environment.NewLine}Git output (exit code: {result.ExitCodeDisplay}): {Environment.NewLine}{result.StandardOutput}";
                await fileViewer.ViewTextAsync(item?.Item?.Name, output, cancellationToken: cancellationToken);
                return;
            }

            // Try set highlighting from first found filename
            var match = FileNameRegex.Match(result.StandardOutput);
            var filename = match.Groups["file"].Success ? match.Groups["file"].Value : item.Item.Name;

            await fileViewer.ViewRangeDiffAsync(filename, result.StandardOutput, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.Item.GrepString))
        {
            var commandConfiguration =
                GrepHighlightService.GetGitCommandConfiguration(fileViewer.Module);
            var result = await fileViewer.Module.GetGrepFileAsync(
                item.SecondRevision.ObjectId,
                item.Item.Name,
                fileViewer.GetExtraGrepArguments(),
                item.Item.GrepString,
                true,
                true,
                commandConfiguration,
                fileViewer.Encoding,
                cancellationToken);

            if (!result.ExitedSuccessfully)
            {
                var output =
                    $"{result.StandardError}{Environment.NewLine}Git command (exit code: {result.ExitCodeDisplay}): {result}{Environment.NewLine}";
                await fileViewer.ViewTextAsync(item?.Item?.Name, output, cancellationToken: cancellationToken);
                return;
            }

            await fileViewer.ViewGrepAsync(item, result.StandardOutput, cancellationToken);
            return;
        }

        if (firstId == ObjectId.CombinedDiffId)
        {
            var result = fileViewer.Module.GetCombinedDiffContent(item.SecondRevision.ObjectId, item.Item.Name,
                fileViewer.GetExtraDiffArguments(isCombinedDiff: true),
                fileViewer.Encoding,
                out var diffOfConflict,
                fileViewer.PatchUseGitColoring,
                CombinedDiffHighlightService.GetGitCommandConfiguration(fileViewer.Module,
                    AppSettings.UseGitColoring.Value),
                cancellationToken);

            if (!result)
            {
                var output = $"Git command exit code: {result}{Environment.NewLine}{diffOfConflict}";
                await fileViewer.ViewTextAsync(item?.Item?.Name, diffOfConflict, cancellationToken: cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(diffOfConflict))
            {
                await fileViewer.ViewTextAsync(item?.Item?.Name, TranslatedStrings.UninterestingDiffOmitted,
                    cancellationToken: cancellationToken);
                return;
            }

            await fileViewer.ViewCombinedDiffAsync(item, diffOfConflict, line, openWithDiffTool, cancellationToken);
            return;
        }

        if (item.Item.IsSubmodule)
        {
            var status = item.Item.GetSubmoduleStatusAsync() is { } statusTask

                // Patch already evaluated, normal case for e.g. FileStatusList
                ? await statusTask
                : await SubmoduleHelpers.GetSubmoduleDiffChangesAsync(fileViewer.Module, item.Item.Name,
                    item.Item.OldName, firstId, item.SecondRevision.ObjectId, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            var subText = status is null
                ? $"Failed to get status for submodule \"{item.Item.Name}\""
                : await Task.Run(() => SubmoduleResources.GetSubmoduleStatusText(fileViewer.Module, status),
                        cancellationToken)
                    .ConfigureAwait(false);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await fileViewer.ViewPatchAsync(item, subText, line, openWithDiffTool, cancellationToken);
            return;
        }

        if (AppSettings.DiffDisplayAppearance.Value == DiffDisplayAppearance.Difftastic &&
            fileViewer.IsDifftasticEnabled.Value)
        {
            var isTracked = item.Item.IsTracked || (!item.Item.TreeId.IsZero && !item.SecondRevision.ObjectId.IsZero);
            (var diffArgs, var extraCacheKey) = fileViewer.GetDifftasticArguments();

            // set file name as null to not change the restore lineno
            await fileViewer.ViewTextAsync(null, $"git difftool {diffArgs} -- {item.Item.Name}",
                cancellationToken: cancellationToken);

            var result = await fileViewer.Module.GetSingleDifftoolAsync(firstId,
                item.SecondRevision.ObjectId, item.Item.Name, item.Item.OldName,
                diffArgs,
                true,
                extraCacheKey,
                isTracked,
                true,
                cancellationToken);

            if (!result.ExitedSuccessfully)
            {
                var output =
                    $"Git command exit code: {result.ExitCodeDisplay}{Environment.NewLine}{result.StandardError}";
                await fileViewer.ViewTextAsync(item?.Item?.Name, output, cancellationToken: cancellationToken);
                return;
            }

            await fileViewer.ViewDifftasticAsync(item.Item.Name, result.StandardOutput, cancellationToken);
            return;
        }

        // diff of text file
        var selectedPatch =
            await GetSelectedPatchAsync(fileViewer, firstId, item.SecondRevision.ObjectId, item.Item, cancellationToken)
            ?? defaultText;

        await fileViewer.ViewPatchAsync(item, selectedPatch, line, openWithDiffTool, cancellationToken);
        return;

        void OpenWithDiffTool()
        {
            fileViewer.Module.OpenWithDifftool(
                item.Item.Name,
                item.Item.OldName,
                firstId.IsZero ? null : firstId.ToString(),
                item.SecondRevision.ObjectId.ToString(),
                isTracked: item.Item.IsTracked);
        }

        static async Task<string?> GetSelectedPatchAsync(
            FileViewer fileViewer,
            ObjectId firstId,
            ObjectId selectedId,
            GitItemStatus file,
            CancellationToken cancellationToken)
        {
            Patch? patch;
            string? errorMessage;

            var module = fileViewer.Module;
            var isSkipWorktree = file.IsSkipWorktree;
            if (isSkipWorktree)
            {
                module.SkipWorktreeFiles([file], false, out _);
            }

            try
            {
                (patch, errorMessage) = await GetItemPatchAsync(module, file, firstId, selectedId,
                    fileViewer.GetExtraDiffArguments(), fileViewer.PatchUseGitColoring, fileViewer.Encoding,
                    cancellationToken);
            }
            finally
            {
                if (isSkipWorktree)
                {
                    try
                    {
                        file.IsSkipWorktree = false;
                        module.SkipWorktreeFiles([file], true, out _);
                    }
                    finally
                    {
                        file.IsSkipWorktree = true;
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            return patch?.Text ?? errorMessage;

            static async Task<(Patch? patch, string? errorMessage)> GetItemPatchAsync(
                IGitModule module,
                GitItemStatus file,
                ObjectId firstId,
                ObjectId secondId,
                string diffArgs,
                bool useGitColoring,
                Encoding encoding,
                CancellationToken cancellationToken)
            {
                // Files with tree guid should be presented with normal diff
                var isTracked = file.IsTracked || (!file.TreeId.IsZero && !secondId.IsZero);

                return await module.GetSingleDiffAsync(firstId, secondId, file.Name, file.OldName, diffArgs, encoding,
                    true, isTracked,
                    useGitColoring,
                    PatchHighlightService.GetGitCommandConfiguration(module, useGitColoring),
                    cancellationToken);
            }
        }
    }

    public static void RemoveIfExists(this TabControl tabControl, TabPage page)
    {
        if (tabControl.TabPages.Contains(page))
        {
            tabControl.TabPages.Remove(page);
        }
    }

    public static void InsertIfNotExists(this TabControl tabControl, int index, TabPage page)
    {
        if (!tabControl.TabPages.Contains(page))
        {
            tabControl.TabPages.Insert(index, page);
        }
    }

    public static void Mask(this Control control)
    {
        if (FindMaskPanel(control) is null)
        {
            LoadingControl panel = new()
            {
                Dock = DockStyle.Fill, IsAnimating = true, BackColor = SystemColors.AppWorkspace
            };
            control.Controls.Add(panel);
            panel.BringToFront();
        }
    }

    public static void UnMask(this Control control)
    {
        var panel = FindMaskPanel(control);
        if (panel is not null)
        {
            control.Controls.Remove(panel);
            panel.Dispose();
        }
    }

    private static LoadingControl? FindMaskPanel(Control control)
    {
        return control.Controls.Cast<Control>().OfType<LoadingControl>().FirstOrDefault();
    }

    public static IEnumerable<TreeNode> AllNodes(this TreeView tree)
    {
        return tree.Nodes.AllNodes();
    }

    private static IEnumerable<TreeNode> AllNodes(this TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            yield return node;

            foreach (var subNode in node.Nodes.AllNodes())
            {
                yield return subNode;
            }
        }
    }

    public static Control FindFocusedControl(this ContainerControl container)
    {
        while (true)
        {
            if (container.ActiveControl is ContainerControl activeContainer)
            {
                container = activeContainer;
            }
            else
            {
                return container.ActiveControl!;
            }
        }
    }
}
