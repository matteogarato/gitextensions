using GitCommands;
using GitCommands.Logging;

namespace GitCommandsTests;
public class CommandLogTests
{
    [Theory]
    [InlineData("somegit.exe", "", "")]
    [InlineData("somegit.exe", @"verb -c config", "verb")]
    [InlineData("somegit.exe", @"-c config verb", "verb")]
    [InlineData("somegit.exe", @"-c config --no-optional-locks verb", "verb")]
    [InlineData("somegit.exe", @"-c config1 -c config2=x verb", "verb")]
    [InlineData("somegit.exe", @"-c config1 -c config2 x verb", "x verb")]
    [InlineData("something.exe", @"-c config=""value with space"" verb", "verb")]
    [InlineData("gitOnTheRocks", "config list --local --includes --null", "config list --local --includes --null")]
    [InlineData("bitbucket", "--pam --param --parapam", "--pam --param --parapam")]
    [InlineData(@"C:\bitbucket\bucketbit.kexe", "--pam --param --parapam", "--pam --param --parapam")]
    // Not needed and not implemented yet. It just removes '-c core.xxx=""something with \""' at the moment.
    [InlineData("somegit.exe", @"-c core.xxx=""something with \""value1 in escaped quotes\"" \""value2\"""" verb", @"value1 in escaped quotes\"" \""value2\"""" verb")]
    [InlineData(@"C:\yesterday\someday.plexe", @"-c core.xxx=""something with \""value1 in escaped quotes\"" \""value2\"""" verb", @"value1 in escaped quotes\"" \""value2\"""" verb")]
    public void CommanLogEntry_ColumnLine_should_display_native_git_command(string fileName, string arguments, string expectedMainArguments)
    {
        string origGitCommandValue = AppSettings.GitCommand;
        AppSettings.GitCommandValue = fileName;

        CommandLogEntry commandLogEntry = new(fileName, arguments, workingDir: "", startedAt: DateTime.MinValue, isOnMainThread: true);
        commandLogEntry.ColumnLine.Should().Be($"00:00:00.000   running         UI     {CommandLogEntry.NativeGitLogName} {expectedMainArguments}");

        AppSettings.GitCommandValue = origGitCommandValue;
    }

    [Theory]
    [InlineData("wsl", @"-d Ubuntu --cd ""\\wsl$\Ubuntu\home\user\repo\project"" git -c config=""value with space"" arg1 arg2", "arg1 arg2")]
    [InlineData(@"wsl -d Ubuntu --cd ""\\wsl$\Ubuntu\home\user\repo\project"" git", @"-c config=""value with space"" arg1 arg2", "arg1 arg2")]
    [InlineData("wsl", @"-d Ubuntu --cd ""\\wsl$\Ubuntu\home\user\repo\project"" git config list --local --includes --null", "config list --local --includes --null")]
    [InlineData(@"wsl -d Ubuntu --cd ""\\wsl$\\Ubuntu\\home\\user\\repo\\project git", "config list --local --includes --null", "config list --local --includes --null")]
    public void CommanLogEntry_ColumnLine_should_display_wsl_git_command(string fileName, string arguments, string expectedMainArguments)
    {
        string origGitCommandValue = AppSettings.GitCommand;
        AppSettings.GitCommandValue = "git";

        CommandLogEntry commandLogEntry = new(fileName, arguments, workingDir: "", startedAt: DateTime.MinValue, isOnMainThread: true);
        commandLogEntry.ColumnLine.Should().Be($"00:00:00.000   running         UI     {CommandLogEntry.WslGitLogName} {expectedMainArguments}");

        AppSettings.GitCommandValue = origGitCommandValue;
    }

    [Theory]
    [InlineData("anycmd", @"-d Ubuntu --cd ""\\wsl$\Ubuntu\home\user\repo\project"" git -c config=""value with space"" agr1 arg2", @"-d Ubuntu --cd ""\\wsl$\Ubuntu\home\user\repo\project"" git -c config=""value with space"" agr1 arg2")]
    [InlineData(@"anycmd -d Ubuntu --cd ""\\wsl$\Ubuntu\home\user\repo\project"" git", @"-c config=""value with space"" arg1 arg2", @"-c config=""value with space"" arg1 arg2")]
    [InlineData("notAgit.exe", "", "")]
    [InlineData("notAgit.exe", "69 < 420", "69 < 420")]
    [InlineData("notAgit", "69 < 420", "69 < 420")]
    [InlineData("git", "69 < 420", "69 < 420")]
    public void CommanLogEntry_ColumnLine_should_display_non_git_command(string fileName, string arguments, string expectedMainArguments)
    {
        string origGitCommandValue = AppSettings.GitCommand;
        AppSettings.GitCommandValue = "notanycmd";

        CommandLogEntry commandLogEntry = new(fileName, arguments, workingDir: "", startedAt: DateTime.MinValue, isOnMainThread: true);
        commandLogEntry.ColumnLine.Should().Be($"00:00:00.000   running         UI     {fileName} {expectedMainArguments}");

        AppSettings.GitCommandValue = origGitCommandValue;
    }
}
