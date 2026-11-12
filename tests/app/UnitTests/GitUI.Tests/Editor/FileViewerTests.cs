using GitCommands.Settings;
using GitUI.Editor;

namespace GitUITests.Editor;

public class FileViewerTests : IDisposable
{
    private readonly FileViewer _fileViewer = null!;

    public FileViewerTests()
    {
        _fileViewer = new FileViewer();
    }

    void IDisposable.Dispose()
    {
        _fileViewer.Dispose();
    }

    [StaTheory]
    [InlineData(IgnoreWhitespaceKind.None, IgnoreWhitespaceKind.Eol, true, false, false)]
    [InlineData(IgnoreWhitespaceKind.None, IgnoreWhitespaceKind.Change, true, true, false)]
    [InlineData(IgnoreWhitespaceKind.None, IgnoreWhitespaceKind.AllSpace, true, true, true)]
    [InlineData(IgnoreWhitespaceKind.Eol, IgnoreWhitespaceKind.Change, true, true, false)]
    [InlineData(IgnoreWhitespaceKind.Eol, IgnoreWhitespaceKind.AllSpace, true, true, true)]
    [InlineData(IgnoreWhitespaceKind.Change, IgnoreWhitespaceKind.Eol, true, false, false)]
    [InlineData(IgnoreWhitespaceKind.Change, IgnoreWhitespaceKind.AllSpace, true, true, true)]
    [InlineData(IgnoreWhitespaceKind.AllSpace, IgnoreWhitespaceKind.Eol, true, false, false)]
    [InlineData(IgnoreWhitespaceKind.AllSpace, IgnoreWhitespaceKind.Change, true, true, false)]
    public void Should_correctly_setup_IgnoreWhitespaceMethod_with_one_click(IgnoreWhitespaceKind oldIgnoreWhitespace,
        IgnoreWhitespaceKind newIgnoreWhitespace, bool ignoreEol, bool ignoreChange, bool ignoreAllSpace)
    {
        newIgnoreWhitespace.Should().NotBe(oldIgnoreWhitespace);

        var accessor = _fileViewer.GetTestAccessor();

        accessor.IgnoreWhitespace = oldIgnoreWhitespace;

        switch (newIgnoreWhitespace)
        {
            case IgnoreWhitespaceKind.Eol:
                accessor.IgnoreWhitespaceAtEolToolStripMenuItem_Click(null!, null!);
                break;
            case IgnoreWhitespaceKind.Change:
                accessor.IgnoreWhitespaceChangesToolStripMenuItemClick(null!, null!);
                break;
            case IgnoreWhitespaceKind.AllSpace:
                accessor.IgnoreAllWhitespaceChangesToolStripMenuItem_Click(null!, null!);
                break;
        }

        accessor.IgnoreWhitespace.Should().Be(newIgnoreWhitespace);

        accessor.IgnoreWhitespaceAtEolButton.Checked.Should().Be(ignoreEol);
        accessor.IgnoreWhitespaceAtEolMenuItem.Checked.Should().Be(ignoreEol);

        accessor.IgnoreWhiteSpacesButton.Checked.Should().Be(ignoreChange);
        accessor.IgnoreWhiteSpacesMenuItem.Checked.Should().Be(ignoreChange);

        accessor.IgnoreAllWhitespacesButton.Checked.Should().Be(ignoreAllSpace);
        accessor.IgnoreAllWhitespacesMenuItem.Checked.Should().Be(ignoreAllSpace);
    }

    [StaTheory]
    [InlineData(IgnoreWhitespaceKind.None, IgnoreWhitespaceKind.Eol)]
    [InlineData(IgnoreWhitespaceKind.None, IgnoreWhitespaceKind.Change)]
    [InlineData(IgnoreWhitespaceKind.None, IgnoreWhitespaceKind.AllSpace)]
    [InlineData(IgnoreWhitespaceKind.Eol, IgnoreWhitespaceKind.Change)]
    [InlineData(IgnoreWhitespaceKind.Eol, IgnoreWhitespaceKind.AllSpace)]
    [InlineData(IgnoreWhitespaceKind.Change, IgnoreWhitespaceKind.Eol)]
    [InlineData(IgnoreWhitespaceKind.Change, IgnoreWhitespaceKind.AllSpace)]
    [InlineData(IgnoreWhitespaceKind.AllSpace, IgnoreWhitespaceKind.Eol)]
    [InlineData(IgnoreWhitespaceKind.AllSpace, IgnoreWhitespaceKind.Change)]
    public void Should_correctly_reset_IgnoreWhitespaceMethod_to_None_with_two_clicks(
        IgnoreWhitespaceKind oldIgnoreWhitespace, IgnoreWhitespaceKind newIgnoreWhitespace)
    {
        newIgnoreWhitespace.Should().NotBe(oldIgnoreWhitespace);

        var accessor = _fileViewer.GetTestAccessor();

        accessor.IgnoreWhitespace = oldIgnoreWhitespace;

        switch (newIgnoreWhitespace)
        {
            case IgnoreWhitespaceKind.Eol:
                accessor.IgnoreWhitespaceAtEolToolStripMenuItem_Click(null!, null!);
                accessor.IgnoreWhitespaceAtEolToolStripMenuItem_Click(null!, null!);
                break;
            case IgnoreWhitespaceKind.Change:
                accessor.IgnoreWhitespaceChangesToolStripMenuItemClick(null!, null!);
                accessor.IgnoreWhitespaceChangesToolStripMenuItemClick(null!, null!);
                break;
            case IgnoreWhitespaceKind.AllSpace:
                accessor.IgnoreAllWhitespaceChangesToolStripMenuItem_Click(null!, null!);
                accessor.IgnoreAllWhitespaceChangesToolStripMenuItem_Click(null!, null!);
                break;
        }

        accessor.IgnoreWhitespace.Should().Be(IgnoreWhitespaceKind.None);

        accessor.IgnoreWhitespaceAtEolButton.Checked.Should().Be(false);
        accessor.IgnoreWhitespaceAtEolMenuItem.Checked.Should().Be(false);

        accessor.IgnoreWhiteSpacesButton.Checked.Should().Be(false);
        accessor.IgnoreWhiteSpacesMenuItem.Checked.Should().Be(false);

        accessor.IgnoreAllWhitespacesButton.Checked.Should().Be(false);
        accessor.IgnoreAllWhitespacesMenuItem.Checked.Should().Be(false);
    }
}
