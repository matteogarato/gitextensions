using System.Net;
using System.Text;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using ResourceManager;

namespace GitExtensions.Plugins.ReleaseNotesGenerator;

/// <summary>
///     Test on GE repository from "2.00" to "2.10". Should display 687 items.
/// </summary>
public partial class ReleaseNotesGeneratorForm : GitExtensionsFormBase
{
    private const string _mostRecentHint = "most recent changes are listed on top";
    private readonly TranslationString _caption = new("Invalid input");
    private readonly TranslationString _commitLogFrom = new("Commit log from '{0}' to '{1}' ({2}):");
    private readonly TranslationString _fromCommitNotSpecified = new("'From' commit must be specified");
    private readonly IGitLogLineParser _gitLogLineParser;
    private readonly GitUiEventArgs _gitUiCommands;
    private readonly TranslationString _toCommitNotSpecified = new("'To' commit must be specified");
    private IEnumerable<LogLine> _lastGeneratedLogLines = [];

    public ReleaseNotesGeneratorForm(GitUiEventArgs gitUiCommands)
    {
        InitializeComponent();
        InitializeComplete();

        _gitUiCommands = gitUiCommands;
        _gitLogLineParser = new GitLogLineParser();
    }

    private void ReleaseNotesGeneratorForm_Load(object sender, EventArgs e)
    {
        Icon = Owner?.Icon;
        textBoxResult_TextChanged(sender, e);
    }

    private void buttonGenerate_Click(object sender, EventArgs e)
    {
        textBoxResult.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(textBoxRevFrom.Text))
        {
            MessageBoxes.ShowError(this, _fromCommitNotSpecified.Text, _caption.Text);
            textBoxRevFrom.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_NO_TRANSLATE_textBoxRevTo.Text))
        {
            MessageBoxes.ShowError(this, _toCommitNotSpecified.Text, _caption.Text);
            _NO_TRANSLATE_textBoxRevTo.Focus();
            return;
        }

        GitArgumentBuilder args = new("log")
        {
            string.Format(_NO_TRANSLATE_textBoxGitLogArguments.Text, textBoxRevFrom.Text,
                _NO_TRANSLATE_textBoxRevTo.Text)
        };

        var result = _gitUiCommands.GitModule.GitExecutable.GetOutput(args);

        if (OperatingSystem.IsWindows())
        {
            result = string.Join(Environment.NewLine,
                result.Split([Environment.NewLine], StringSplitOptions.None).SelectMany(l => l.Split('\n')));
        }

        textBoxResult.Text = result;
        try
        {
            _lastGeneratedLogLines = _gitLogLineParser.Parse(textBoxResult.Lines);
            labelRevCount.Text = _lastGeneratedLogLines.Count().ToString();
        }
        catch
        {
            labelRevCount.Text = "n/a";
        }

        textBoxResult_TextChanged(sender, e);
    }

    private void textBoxResult_TextChanged(object sender, EventArgs e)
    {
        groupBoxCopy.Enabled = _lastGeneratedLogLines?.Any() is true;
    }

    private void buttonCopyOrigOutput_Click(object sender, EventArgs e)
    {
        ClipboardUtil.TrySetText(textBoxResult.Text);
    }

    private void buttonCopyAsPlainText_Click(object sender, EventArgs e)
    {
        var result = CreateTextTable(_lastGeneratedLogLines);
        ClipboardUtil.TrySetText(result);
    }

    private void buttonCopyAsTextTableSpace_Click(object sender, EventArgs e)
    {
        var result = CreateTextTable(_lastGeneratedLogLines, true, false);
        ClipboardUtil.TrySetText(result);
    }

    private void buttonCopyAsHtml_Click(object sender, EventArgs e)
    {
        var headerHtml = string.Format("<p>Commit log from '{0}' to '{1}' ({2}):</p>", textBoxRevFrom.Text,
            _NO_TRANSLATE_textBoxRevTo.Text, _mostRecentHint);
        var tableHtml = CreateHtmlTable(_lastGeneratedLogLines);
        HtmlFragment.CopyToClipboard(headerHtml + tableHtml);
    }

    private string CreateTextTable(IEnumerable<LogLine> logLines, bool suppressEmptyLines = true,
        bool separateColumnWithTabInsteadOfSpaces = true)
    {
        var headerText = string.Format(_commitLogFrom.Text,
            textBoxRevFrom.Text, _NO_TRANSLATE_textBoxRevTo.Text, _mostRecentHint);

        var colSeparatorFirstLine = separateColumnWithTabInsteadOfSpaces ? "\t" : " ";
        var colSeparatorRestLines = separateColumnWithTabInsteadOfSpaces ? "\t" : "        ";

        StringBuilder stringBuilder = new();

        foreach (var logLine in logLines)
        {
            var message = string.Join(Environment.NewLine + colSeparatorRestLines,
                logLine.MessageLines.Where(a => !suppressEmptyLines || !string.IsNullOrWhiteSpace(a)));
            stringBuilder.AppendFormat("{0}{1}{2}{3}", logLine.Commit, colSeparatorFirstLine, message,
                Environment.NewLine);
        }

        var result = headerText + Environment.NewLine + stringBuilder;
        return result;
    }

    private static string CreateHtmlTable(IEnumerable<LogLine> logLines)
    {
        StringBuilder stringBuilder = new();
        stringBuilder.Append("<table>\r\n");
        foreach (var logLine in logLines)
        {
            var message = string.Join("<br/>", logLine.MessageLines.Select(a => WebUtility.HtmlEncode(a)));
            stringBuilder.AppendFormat("<tr>\r\n  <td>{0}</td>\r\n  <td>{1}</td>\r\n</tr>\r\n", logLine.Commit,
                message);
        }

        stringBuilder.Append("</table>");
        return stringBuilder.ToString();
    }
}
