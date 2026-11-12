using ConEmuWinForms;
using GitUI.ConsoleEmulation;
using GitUI.ConsoleEmulation.ConEmu;
using GitModule = GitCommands.Git.GitModule;

namespace GitUITests.UserControls;

public class ConsoleEmulatorOutputControllerFixture
{
    [Fact]
    public void FilterOutConsoleCommandLine_NoFlush()
    {
        var cmd = "\"C:\\Program Files\\Git\\bin\\git.exe\" rebase  -i --autosquash --autostash \"branch_foo\"";
        var outputData = "output data";
        var received = string.Empty;

        void FireDataReceived(ConsoleOutputEventArgs e)
        {
            received += e.Text;
        }

        ConsoleCommandLineOutputProcessor filter = new(cmd.Length, FireDataReceived);

        var chunk1 = cmd[..10];
        var chunk2 = cmd[10..] + Environment.NewLine + outputData;
        filter.AnsiStreamChunkReceived(null!, new AnsiStreamChunkEventArgs(GitModule.SystemEncoding.GetBytes(chunk1)));
        filter.AnsiStreamChunkReceived(null!, new AnsiStreamChunkEventArgs(GitModule.SystemEncoding.GetBytes(chunk2)));

        received.Should().Be(string.Empty);
    }

    [Fact]
    public void FilterOutConsoleCommandLine_Flush()
    {
        var cmd = "\"C:\\Program Files\\Git\\bin\\git.exe\" rebase  -i --autosquash --autostash \"branch_foo\"";
        var outputData = "output data";
        var received = string.Empty;

        void FireDataReceived(ConsoleOutputEventArgs e)
        {
            received += e.Text;
        }

        ConsoleCommandLineOutputProcessor filter = new(cmd.Length, FireDataReceived);

        var chunk1 = cmd[..10];
        var chunk2 = cmd[10..] + Environment.NewLine + outputData;
        filter.AnsiStreamChunkReceived(null!, new AnsiStreamChunkEventArgs(GitModule.SystemEncoding.GetBytes(chunk1)));
        filter.AnsiStreamChunkReceived(null!, new AnsiStreamChunkEventArgs(GitModule.SystemEncoding.GetBytes(chunk2)));
        filter.Flush();

        received.Should().Be(outputData);
    }

    [Fact]
    public void PercentageOutput()
    {
        var cmd = "\"C:\\Program Files\\Git\\bin\\git.exe\" rebase  -i --autosquash --autostash \"branch_foo\"";

        string[] outputData =
        [
            cmd,
            Environment.NewLine,
            "Receiving: 10%\r",
            "Receiving: 20%\r",
            "Receiving: 30%\rR",
            "eceiving: 40%\r",
            "Receiving: 100%\nReceived data\n"
        ];

        List<string> received = [];

        void FireDataReceived(ConsoleOutputEventArgs e)
        {
            received.Add(e.Text);
        }

        ConsoleCommandLineOutputProcessor filter = new(cmd.Length, FireDataReceived);

        foreach (var chunk in outputData)
        {
            filter.AnsiStreamChunkReceived(null!,
                new AnsiStreamChunkEventArgs(GitModule.SystemEncoding.GetBytes(chunk)));
        }

        filter.Flush();

        string[] expectedData =
        [
            "Receiving: 10%\r",
            "Receiving: 20%\r",
            "Receiving: 30%\r",
            "Receiving: 40%\r",
            "Receiving: 100%\n",
            "Received data\n"
        ];

        received.Should().Equal(expectedData);
    }

    [Fact]
    public void CrlfInOutput()
    {
        var cmd = "\"C:\\Program Files\\Git\\bin\\git.exe\" rebase  -i --autosquash --autostash \"branch_foo\"";

        string[] outputData =
        [
            cmd,
            Environment.NewLine,
            "Receiving: 10%\r",
            "Receiving: 20%\r",
            "Receiving: 30%\rR",
            "eceiving: 40%\r",
            "Receiving: 100%\nReceived\r\ndata\n"
        ];

        List<string> received = [];

        void FireDataReceived(ConsoleOutputEventArgs e)
        {
            received.Add(e.Text);
        }

        ConsoleCommandLineOutputProcessor filter = new(cmd.Length, FireDataReceived);

        foreach (var chunk in outputData)
        {
            filter.AnsiStreamChunkReceived(null!,
                new AnsiStreamChunkEventArgs(GitModule.SystemEncoding.GetBytes(chunk)));
        }

        filter.Flush();

        string[] expectedData =
        [
            "Receiving: 10%\r",
            "Receiving: 20%\r",
            "Receiving: 30%\r",
            "Receiving: 40%\r",
            "Receiving: 100%\n",
            "Received\r",
            "\n",
            "data\n"
        ];

        received.Should().Equal(expectedData);
    }
}
