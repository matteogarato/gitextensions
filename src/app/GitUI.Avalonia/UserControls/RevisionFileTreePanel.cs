using Avalonia.Controls;
using Avalonia.Input;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using GitUIPluginInterfaces;

namespace GitUI;

/// <summary>
///  Browses the files of a revision one directory at a time.
/// </summary>
public sealed class RevisionFileTreePanel : UserControl
{
    private readonly TextBlock _path = new() { Name = "lblTreePath", Margin = new Avalonia.Thickness(4), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
    private readonly ListBox _entries = new() { Name = "lstTreeEntries" };
    private readonly CancellationTokenSequence _sequence = new();
    private IGitModule? _module;
    private ObjectId _revision;
    private string _directory = string.Empty;

    /// <summary>
    ///  Initializes a new instance of the <see cref="RevisionFileTreePanel"/> class.
    /// </summary>
    public RevisionFileTreePanel()
    {
        Button up = new() { Name = "btnTreeUp", Content = "..", Margin = new Avalonia.Thickness(4, 0) };
        up.Click += (_, _) => NavigateUp();
        DockPanel header = new() { LastChildFill = true };
        DockPanel.SetDock(up, Dock.Left);
        header.Children.Add(up);
        header.Children.Add(_path);
        DockPanel root = new() { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(_entries);
        Content = root;

        _entries.SelectionChanged += (_, _) => SelectedFileChanged?.Invoke(this, EventArgs.Empty);
        _entries.DoubleTapped += (_, _) => OpenSelected();
        _entries.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                OpenSelected();
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                NavigateUp();
                e.Handled = true;
            }
        };
    }

    /// <summary>
    ///  Raised when the selected entry changes.
    /// </summary>
    public event EventHandler? SelectedFileChanged;

    /// <summary>
    ///  Raised when a file entry is activated (double click or Enter).
    /// </summary>
    public event EventHandler? FileActivated;

    /// <summary>
    ///  Gets the selected file entry, or <see langword="null"/> for directories, submodules or no selection.
    /// </summary>
    public RevisionTreeEntry? SelectedFile
        => _entries.SelectedItem is RevisionTreeEntry { ObjectType: GitObjectType.Blob } entry ? entry : null;

    /// <summary>
    ///  Gets the revision currently browsed.
    /// </summary>
    public ObjectId Revision => _revision;

    /// <summary>
    ///  Shows the root of <paramref name="revision"/>.
    /// </summary>
    public void Show(IGitModule module, ObjectId revision)
    {
        _module = module;
        _revision = revision;
        Load(string.Empty);
    }

    /// <summary>
    ///  Clears the tree.
    /// </summary>
    public void Clear()
    {
        _module = null;
        _sequence.Next();
        _entries.ItemsSource = null;
        _path.Text = string.Empty;
    }

    private void Load(string directory)
    {
        if (_module is null || _revision.IsZero || _revision.IsArtificial)
        {
            Clear();
            return;
        }

        IGitModule module = _module;
        ObjectId revision = _revision;
        CancellationToken cancellationToken = _sequence.Next();
        _directory = directory;
        _path.Text = "/" + directory;
        _entries.ItemsSource = null;
        ThreadHelper.FileAndForget(async () =>
        {
            try
            {
                IReadOnlyList<RevisionTreeEntry> entries = await Task.Run(() => RevisionTreeWorkflow.ListDirectory(module, revision, directory, cancellationToken), cancellationToken);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                _entries.ItemsSource = entries;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        });
    }

    private void NavigateUp()
    {
        if (RevisionTreeWorkflow.GetParentDirectory(_directory) is string parent)
        {
            Load(parent);
        }
    }

    private void OpenSelected()
    {
        if (_entries.SelectedItem is not RevisionTreeEntry entry)
        {
            return;
        }

        if (entry.IsDirectory)
        {
            Load(entry.Path);
        }
        else if (entry.ObjectType == GitObjectType.Blob)
        {
            FileActivated?.Invoke(this, EventArgs.Empty);
        }
    }
}
