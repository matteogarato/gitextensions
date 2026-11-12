using System.Text;
using CommonTestUtils;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.Editor;

namespace UITests.CommandsDialogs;

public class FormEditorTests : IDisposable
{
    // Created once for each test
    private readonly GitUiCommands _commands = null!;

    // Created once for the fixture
    private readonly ReferenceRepository _referenceRepository = null!;

    public FormEditorTests()
    {
        _referenceRepository = new ReferenceRepository();
        _commands = new GitUiCommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(),
            _referenceRepository.Module);
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void HasChanges_updated_correctly()
    {
        var filePath = Path.Combine(_referenceRepository.Module.WorkingDir, Path.GetRandomFileName());

        try
        {
            File.WriteAllText(filePath, "Hello↔world", _commands.Module.FilesEncoding);

            UiTest.RunForm<FormEditor>(
                () =>
                {
                    using FormEditor formEditor = new(_commands, filePath, false);
                    formEditor.ShowDialog();
                },
                form =>
                {
                    form.GetTestAccessor().HasChanges.Should().BeFalse();

                    var fileViewerInternal =
                        form.GetTestAccessor().FileViewer.GetTestAccessor().FileViewerInternal;
                    fileViewerInternal.SetText(fileViewerInternal.GetText() + "!", openWithDifftool: null);

                    form.GetTestAccessor().HasChanges.Should().BeTrue();

                    form.GetTestAccessor().SaveChanges();

                    form.GetTestAccessor().HasChanges.Should().BeFalse();

                    return Task.CompletedTask;
                });
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [WinFormsFact]
    public void Should_set_linenumber_correctly()
    {
        var filePath = Path.Combine(_referenceRepository.Module.WorkingDir, Path.GetRandomFileName());

        try
        {
            const int lineNumberDefault = 2;
            File.WriteAllText(filePath, "Hello↔world\nline2\nline3\n\n\n", _commands.Module.FilesEncoding);

            UiTest.RunForm<FormEditor>(
                () =>
                {
                    using FormEditor formEditor = new(_commands, filePath, false, lineNumber: lineNumberDefault);
                    formEditor.ShowDialog();
                },
                form =>
                {
                    form.GetTestAccessor().HasChanges.Should().BeFalse();

                    var fileViewerInternal =
                        form.GetTestAccessor().FileViewer.GetTestAccessor().FileViewerInternal;
                    fileViewerInternal.GetTestAccessor().CurrentViewPositionCache.GetTestAccessor()
                        .SetCurrentIdentification("hello-world");
                    fileViewerInternal.SetText(fileViewerInternal.GetText() + "!", null, ViewMode.Text, true,
                        "hello-world");

                    form.GetTestAccessor().HasChanges.Should().BeTrue();

                    form.GetTestAccessor().SaveChanges();

                    form.GetTestAccessor().HasChanges.Should().BeFalse();

                    fileViewerInternal.CurrentFileLine().Should().Be(lineNumberDefault);

                    return Task.CompletedTask;
                });
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [WinFormsFact(Skip =
        "Test cannot pass because _commands.Module.FilesEncoding has a preamble which is always used by FormEditor.SaveChanges")]
    public void Should_preserve_encoding_utf8()
    {
        Should_preserve_encoding(new UTF8Encoding(false));
    }

    [WinFormsFact]
    public void Should_preserve_encoding_utf8_bom()
    {
        Should_preserve_encoding(new UTF8Encoding(true));
    }

    private void Should_preserve_encoding(Encoding encoding)
    {
        UTF8Encoding.Default.ToString().Should().Be(_commands.Module.FilesEncoding.ToString());
        (_commands.Module.FilesEncoding.GetPreamble().SequenceEqual(UTF8Encoding.Default.GetPreamble())
         || _commands.Module.FilesEncoding.GetPreamble().SequenceEqual(encoding.GetPreamble())).Should().BeTrue();

        var filePath = Path.Combine(_referenceRepository.Module.WorkingDir, Path.GetRandomFileName());

        try
        {
            File.WriteAllText(filePath, "Hello↔world", encoding);

            UiTest.RunForm<FormEditor>(
                () =>
                {
                    using FormEditor formEditor = new(_commands, filePath, false);
                    formEditor.ShowDialog();
                },
                form =>
                {
                    var fileViewerInternal =
                        form.GetTestAccessor().FileViewer.GetTestAccessor().FileViewerInternal;
                    fileViewerInternal.SetText(fileViewerInternal.GetText() + "!", openWithDifftool: null);
                    form.GetTestAccessor().SaveChanges();
                    return Task.CompletedTask;
                });

            File.ReadAllBytes(filePath).Should()
                .BeEquivalentTo(encoding.GetPreamble().Concat(encoding.GetBytes("Hello↔world!")));
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
