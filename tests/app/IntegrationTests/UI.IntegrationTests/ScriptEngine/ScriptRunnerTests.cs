using System.ComponentModel.Design;
using System.Reflection;
using AwesomeAssertions.Specialized;
using CommonTestUtils;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.ScriptsEngine;
using NSubstitute;
using ResourceManager;

namespace UITests.ScriptEngine;

[UseCulture("en-US")]
public class ScriptRunnerTests : IDisposable
{
    // The scripts available during the tests are created by ScriptManager's GetDefaultScripts().
    // We will adapt the script named "Example" for the needs of the test cases in this file.
    private const int _keyOfExampleScript = 9002;

    // perf optimisation: get hold of the static ScriptsManager.ScriptRunner.RunScript method for test invocations
    // we could have used TestAccessor, but it would involve more code.
    private static readonly MethodInfo? _miRunScript =
        typeof(ScriptsManager.ScriptRunner).GetMethod("RunScriptInternal",
            BindingFlags.NonPublic | BindingFlags.Static);

    private readonly IGitUiCommands _commands = null!;
    private readonly ScriptInfo _exampleScript = null!;
    private readonly MockForm _mockForm = null!;
    private readonly IGitModule _module = null!;

    // Created once for the fixture
    private readonly ReferenceRepository _referenceRepository = null!;

    // Created once for each test
    private readonly GitUiCommands _uiCommands = null!;

    public ScriptRunnerTests()
    {
        ScriptsManager scriptsManager = new();
        scriptsManager.GetScripts();

        var serviceContainer = GlobalServiceContainer.CreateDefaultMockServiceContainer();
        serviceContainer.RemoveService<IScriptsManager>();
        serviceContainer.RemoveService<IScriptsRunner>();
        serviceContainer.AddService<IScriptsManager>(scriptsManager);
        serviceContainer.AddService<IScriptsRunner>(scriptsManager);

        _referenceRepository = new ReferenceRepository();
        _uiCommands = new GitUiCommands(serviceContainer, _referenceRepository.Module);

        _module = Substitute.For<IGitModule>();
        _module.GetCurrentRemote().ReturnsForAnyArgs("origin");
        _module.GetCurrentCheckout().ReturnsForAnyArgs(ObjectId.WorkTreeId);

        _commands = Substitute.For<IGitUiCommands>();
        _commands.Module.Returns(_module);

        _mockForm = new MockForm(_commands);

        _exampleScript = scriptsManager.GetScript(_keyOfExampleScript)!;
        _exampleScript.AskConfirmation = false; // avoid any dialogs popping up
        _exampleScript.RunInBackground = true; // avoid any dialogs popping up

        if (_miRunScript is null)
        {
            throw new InvalidOperationException();
        }
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsTheory]
    [InlineData(null)]
    [InlineData("")]
    public void RunScript_without_command_shall_return_false(string? command)
    {
        _exampleScript.Command = command;

        var result = ScriptsManager.ScriptRunner.RunScript(_exampleScript, _mockForm, _mockForm.UICommands,
            ScriptOptionsProviderBase.Default);

        result.Should().BeFalse();
    }

    [WinFormsFact]
    public void RunScript_without_arguments_shall_succeed()
    {
        _exampleScript.Command = "{git}";
        _exampleScript.Arguments = "";

        var result = ScriptsManager.ScriptRunner.RunScript(_exampleScript, _mockForm, _mockForm.UICommands,
            ScriptOptionsProviderBase.Default);

        result.Should().BeTrue();
    }

    [WinFormsFact]
    public void RunScript_with_arguments_without_options_shall_succeed()
    {
        _exampleScript.Command = "{git}";
        _exampleScript.Arguments = "--version";

        var result = ScriptsManager.ScriptRunner.RunScript(_exampleScript, _mockForm, _mockForm.UICommands,
            ScriptOptionsProviderBase.Default);

        result.Should().BeTrue();
    }

    [WinFormsFact]
    public void RunScript_with_arguments_with_c_option_shall_succeed()
    {
        _exampleScript.Command = "cmd";
        _exampleScript.Arguments = "/c echo {cHash}";

        GitRevision revision = new(ObjectId.IndexId);
        _module.GetRevision(shortFormat: true, loadRefs: true).Returns(x => revision);

        var result = ScriptsManager.ScriptRunner.RunScript(_exampleScript, _mockForm, _mockForm.UICommands,
            ScriptOptionsProviderBase.Default);

        result.Should().BeTrue();
    }

    [WinFormsFact]
    public void RunScript_with_arguments_with_c_option_without_revision_shall_display_error_and_return_false()
    {
        _exampleScript.Command = "cmd";
        _exampleScript.Arguments = "/c echo {cHash}";

        _module.GetCurrentCheckout().Returns(default(ObjectId));

        var ex =
            ((Action)(() => ExecuteRunScript(_exampleScript, _mockForm, _commands, ScriptOptionsProviderBase.Default)))
            .Should()
            .Throw<UserExternalOperationException>();
        ex.And.Context.Should()
            .Be(
                $"Script: '{_exampleScript.GetDisplayName()}'\r\nA valid revision is required to substitute the argument options");
        ex.And.Command.Should().Be(_exampleScript.Command);
        ex.And.Arguments.Should().Be(_exampleScript.Arguments);
        ex.And.WorkingDirectory.Should().Be(_module.WorkingDir);
    }

    [WinFormsFact]
    public void RunScript_with_arguments_with_s_option_without_RevisionGrid_shall_display_error_and_return_false()
    {
        _exampleScript.Command = "cmd";
        _exampleScript.Arguments = "/c echo {sHash}";

        _mockForm.UICommands.BrowseRepo = null;

        var ex =
            ((Action)(() =>
                ExecuteRunScript(_exampleScript, _mockForm, _mockForm.UICommands, ScriptOptionsProviderBase.Default)))
            .Should()
            .Throw<UserExternalOperationException>();
        ex.And.Context.Should()
            .Be(
                $"Script: '{_exampleScript.GetDisplayName()}'\r\n'sHash' option is only supported when invoked from the revision grid");
        ex.And.Command.Should().Be(_exampleScript.Command);
        ex.And.Arguments.Should().Be(_exampleScript.Arguments);
        ex.And.WorkingDirectory.Should().Be(_module.WorkingDir);
    }

    [WinFormsFact]
    public void
        RunScript_with_arguments_with_s_option_with_RevisionGrid_without_selection_shall_display_error_and_return_false()
    {
        _exampleScript.Command = "cmd";
        _exampleScript.Arguments = "/c echo {sHash}";

        RunFormTest(async formBrowse =>
        {
            // wait until the revisions are loaded
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            // check for correct test setup
            formBrowse.RevisionGridControl.GetTestAccessor().ClearSelection();
            formBrowse.RevisionGridControl.GetSelectedRevisions().Count.Should().Be(0);
            formBrowse.RevisionGridControl.LatestSelectedRevision.Should().BeNull();

            var ex =
                ((Action)(() => ExecuteRunScript(_exampleScript, formBrowse, formBrowse.UiCommands,
                    ScriptOptionsProviderBase.Default))).Should()
                .Throw<UserExternalOperationException>();
            ex.And.Context.Should()
                .Be(
                    $"Script: '{_exampleScript.GetDisplayName()}'\r\nA valid revision is required to substitute the argument options");
            ex.And.Command.Should().Be(_exampleScript.Command);
            ex.And.Arguments.Should().Be(_exampleScript.Arguments);
            ex.And.WorkingDirectory.Should().Be(_referenceRepository.Module.WorkingDir);
        });
    }

    [WinFormsFact]
    public void RunScript_with_arguments_with_s_option_with_RevisionGrid_with_selection_shall_succeed()
    {
        _exampleScript.Command = "cmd";
        _exampleScript.Arguments = "/c echo {sHash}";
        _referenceRepository.CheckoutRevision();

        RunFormTest(async formBrowse =>
        {
            // wait until the revisions are loaded
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            formBrowse.RevisionGridControl.GetSelectedRevisions().Count.Should().Be(1);

            string? errorMessage = null;
            var result = ExecuteRunScript(_exampleScript, formBrowse, formBrowse.UiCommands,
                ScriptOptionsProviderBase.Default);

            errorMessage.Should().BeNull();
            result.Should().BeTrue();
        });
    }

    private static bool ExecuteRunScript(ScriptInfo script, IWin32Window owner, IGitUiCommands uiCommands,
        IScriptOptionsProvider scriptOptionsProvider)
    {
        try
        {
            var result = (bool)_miRunScript!.Invoke(null,
            [
                script,
                owner,
                uiCommands,
                scriptOptionsProvider
            ])!;
            return result;
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException!;
        }
    }

    private void RunFormTest(Func<FormBrowse, Task> testDriverAsync)
    {
        UiTest.RunForm(
            () => _uiCommands.StartBrowseDialog(owner: null).Should().BeTrue(),
            testDriverAsync);
    }

    private class MockForm(IGitUiCommands commands) : IWin32Window, IGitModuleForm
    {
        public IGitUiCommands UICommands { get; } = commands;
        IntPtr IWin32Window.Handle { get; }
    }
}
