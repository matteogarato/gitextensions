using GitCommands.Git;
using GitExtUtils.GitUI;
using GitUI.Models;

namespace GitUI.UserControls;

internal abstract class OutputHistoryControllerBase : IDisposable
{
    protected IOutputHistoryProvider OutputHistoryProvider;
    protected RichTextBox TextBox;

    internal OutputHistoryControllerBase(IOutputHistoryProvider outputHistoryProvider,
        OutputHistoryControl outputHistoryControl)
    {
        OutputHistoryProvider = outputHistoryProvider;
        TextBox = outputHistoryControl.TextBox;

        TextBox.LinkClicked += (_, e) => OsShellUtil.OpenUrlInDefaultBrowser(e.LinkText);
        outputHistoryControl.tsmiClear.Click += (_, _) => OutputHistoryProvider.ClearHistory();
        outputHistoryControl.tsmiCopy.Click += CopyToClipboard;

        OutputHistoryProvider.HistoryChanged += Update;
        Update(this, EventArgs.Empty);
    }

    void IDisposable.Dispose()
    {
        OutputHistoryProvider.HistoryChanged -= Update;
    }

    internal abstract bool FocusAndToggleIfPanel();

    private void Update(object? sender, EventArgs args)
    {
        var history = OutputHistoryProvider.History;
        TextBox.InvokeAndForget(() =>
        {
            TextBox.Text = history;
            TextBox.SelectionStart = history.Length;
            TextBox.ScrollToCaret();
        });
    }

    private void CopyToClipboard(object? sender, EventArgs args)
    {
        var text = TextBox.SelectedText;
        if (string.IsNullOrEmpty(text))
        {
            text = TextBox.Text;
        }

        Clipboard.SetText(text);
    }
}
