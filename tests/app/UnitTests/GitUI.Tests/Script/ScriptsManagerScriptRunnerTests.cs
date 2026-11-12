using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.ScriptsEngine;
using NSubstitute;
using static GitUI.ScriptsEngine.ScriptsManager;

namespace GitUITests.Script;

[Culture("en-US")]
public class ScriptsManagerScriptRunnerTests
{
    private readonly IGitUiCommands _commands = null!;
    private readonly IGitModule _module = null!;
    private readonly IScriptOptionsProvider _scriptOptionsProvider = null!;

    public ScriptsManagerScriptRunnerTests()
    {
        _module = Substitute.For<IGitModule>();
        _scriptOptionsProvider = Substitute.For<IScriptOptionsProvider>();

        _commands = Substitute.For<IGitUiCommands>();
        _commands.Module.Returns(_module);
    }

    // Regression test for https://github.com/gitextensions/gitextensions/issues/11892
    [Fact]
    public void Parse_should_not_fail_if_arguments_null()
    {
        var result = ScriptRunner.ParseUserInputs("script_name", null,
            _commands,
            null!, _scriptOptionsProvider);

        result.abort.Should().Be(false);
        result.cancel.Should().Be(false);
        result.arguments.Should().BeNull();
    }

    [Fact]
    public void Parse_should_parse_distincts_userInput()
    {
        _commands.GetService<ISimplePromptCreator>().Returns(new SimplePromptCreatorForTest(
            ("input label1", "value", "foo1"), ("input label2", "value2", "bar1"), (null!, string.Empty, "foo2")));
        var result = ScriptRunner.ParseUserInputs("script_name",
            "{UserInput:input label1=value}_{{UserInput:input label2=value2}}_{UserInput}_{{UserInput}}", _commands,
            null!, _scriptOptionsProvider);

        result.abort.Should().Be(false);
        result.cancel.Should().Be(false);
        result.arguments.Should().Be(@"foo1_""bar1""_foo2_""foo2""");
    }

    [Fact]
    public void Parse_should_parse_customized_userInput_without_default_value_specified()
    {
        _commands.GetService<ISimplePromptCreator>()
            .Returns(new SimplePromptCreatorForTest(("input label1", string.Empty, "foo1")));
        var result = ScriptRunner.ParseUserInputs("script_name",
            "{UserInput:input label1}", _commands,
            null!, _scriptOptionsProvider);

        result.abort.Should().Be(false);
        result.cancel.Should().Be(false);
        result.arguments.Should().Be(@"foo1");
    }

    [Fact]
    public void Parse_should_parse_customized_userInputs_and_replace_all_with_same_label()
    {
        _commands.GetService<ISimplePromptCreator>()
            .Returns(new SimplePromptCreatorForTest(("input label1", "value", "foo1")));
        var result = ScriptRunner.ParseUserInputs("script_name",
            "{UserInput:input label1=value}_{UserInput:input label1}_{{UserInput:input label1}}", _commands,
            null!, _scriptOptionsProvider);

        result.abort.Should().Be(false);
        result.cancel.Should().Be(false);
        result.arguments.Should().Be(@"foo1_foo1_""foo1""");
    }

    [Fact]
    public void Parse_should_parse_userInput_with_default_value_as_expression_containing_arguments_inside()
    {
        _module.GetRevision(default, Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(new GitRevision(ObjectId.Parse("79b9792ca4db3d01d7c0f2cd95419dd53665ec41")));
        _commands.GetService<ISimplePromptCreator>().Returns(new SimplePromptCreatorForTest(("input label1",
            "file_79b9792ca4db3d01d7c0f2cd95419dd53665ec41.bak", "file_foo.bak")));
        var result = ScriptRunner.ParseUserInputs("script_name",
            "{UserInput:input label1=file_{HEAD}.bak}", _commands,
            null!, _scriptOptionsProvider);

        result.abort.Should().Be(false);
        result.cancel.Should().Be(false);
        result.arguments.Should().Be(@"file_foo.bak");
    }

    [Fact]
    public void Parse_should_parse_userFiles()
    {
        _commands.GetService<IFilePromptCreator>().Returns(new FilePromptCreatorForTest(@"C:\a_file_path\file.txt"));
        var result = ScriptRunner.ParseUserInputs("script_name",
            "unquoted path:{UserFiles} - quoted path:{{UserFiles}}", _commands,
            null!, _scriptOptionsProvider);

        result.abort.Should().Be(false);
        result.cancel.Should().Be(false);
        result.arguments.Should()
            .Be(@"unquoted path:C:\a_file_path\file.txt - quoted path:""C:\a_file_path\file.txt""");
    }

    private class SimplePromptCreatorForTest(params (string title, string defaultValue, string returnedValue)[] values)
        : ISimplePromptCreator
    {
        private readonly (string label, string defaultValue, string returnedValue)[] _values = values;
        private int _indexOfNextFakeValueReturned;

        IUserInputPrompt ISimplePromptCreator.Create(string? title, string? label, string? defaultValue)
        {
            if (_indexOfNextFakeValueReturned >= _values.Length)
            {
                throw new ArgumentException(
                    $"SimplePrompt expected to be called only {_values.Length} time(s): Test badly configured? Or implementation bug?!?");
            }

            var value = _values[_indexOfNextFakeValueReturned];
            _indexOfNextFakeValueReturned++;

            label.Should().Be(value.label);
            defaultValue.Should().Be(value.defaultValue);

            return new UserInputForTest(value.returnedValue);
        }
    }

    private class UserInputForTest(string returnValue) : IUserInputPrompt
    {
        string IUserInputPrompt.UserInput
        {
            get => returnValue;
        }

        void IDisposable.Dispose()
        {
        }

        DialogResult IUserInputPrompt.ShowDialog(IWin32Window owner)
        {
            return DialogResult.OK;
        }
    }

    private class FilePromptCreatorForTest(string returnValue) : IFilePromptCreator
    {
        public readonly string ReturnValue = returnValue;

        IUserInputPrompt IFilePromptCreator.Create()
        {
            return new UserInputForTest(ReturnValue);
        }
    }
}
