using System.Runtime.InteropServices;
using CommonTestUtils;
using GitUI;
using GitUI.CommandsDialogs.BrowseDialog;
using Xunit;

namespace GitExtensions.UITests.CommandsDialogs;

public class FormUpdatesTests : System.IDisposable
{
    // Created once for the fixture
    private ReferenceRepository _referenceRepository = null!;

    // Created once for each test
    private GitUICommands _commands = null!;
    public FormUpdatesTests()
    {
        _referenceRepository = new ReferenceRepository();
        _commands = new GitUICommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(), _referenceRepository.Module);
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void Should_hide_NET_runtime_link_if_no_required_version()
    {
        RunFormTest(
            form =>
            {
                FormUpdates.TestAccessor accessor = form.GetTestAccessor();

                accessor.DisplayNetRuntimeLink("Required: .NET {0} Desktop Runtime {1} or later {2}.x", requiredNetRuntimeVersion: null!);
                accessor.linkRequiredNetRuntime.Visible.Should().BeFalse();
            });
    }

    [WinFormsTheory]
    [MemberData(nameof(NetRuntimeLinkTextTestCases))]
    public void Should_NET_runtime_link_text_be_correct(Version runtimeVersion, string format, string expectedText, LinkArea expectedLinkArea)
    {
        RunFormTest(
            form =>
            {
                FormUpdates.TestAccessor accessor = form.GetTestAccessor();

                accessor.DisplayNetRuntimeLink(format, runtimeVersion);

                accessor.linkRequiredNetRuntime.Visible.Should().BeTrue();
                accessor.linkRequiredNetRuntime.Text.Should().Be(expectedText);
                accessor.linkRequiredNetRuntime.LinkArea.Should().Be(expectedLinkArea);
            });
    }

    public static IEnumerable<object[]> NetRuntimeLinkTextTestCases()
    {
        yield return
        [
            new Version(8, 10, 134),
            "Required: .NET {0} Desktop Runtime {1} or later {2}.x",
            "Required: .NET 8.10 Desktop Runtime 8.10.134 or later 8.x",
            new LinkArea(36, 8)
        ];

        yield return
        [
            new Version(10, 0, 2),
            "Требуется: .NET {0} Desktop Runtime {1} или более поздняя версия {2}.x",
            "Требуется: .NET 10.0 Desktop Runtime 10.0.2 или более поздняя версия 10.x",
            new LinkArea(37, 6)
        ];

        yield return
        [
            new Version(7, 11, 10),
            "Erforderlich: .NET {0} Desktop Runtime {1} oder höher {2}.x",
            "Erforderlich: .NET 7.11 Desktop Runtime 7.11.10 oder höher 7.x",
            new LinkArea(40, 7)
        ];
    }

    [WinFormsTheory]
    [MemberData(nameof(NetRuntimeLinkTestCases))]
    public void Should_NET_runtime_link_url_be_correct(Version runtimeVersion, string expectedUrl)
    {
        RunFormTest(
            form =>
            {
                FormUpdates.TestAccessor accessor = form.GetTestAccessor();
                accessor.DisplayNetRuntimeLink("Required: .NET {0} Desktop Runtime {1} or later {2}.x", runtimeVersion);

                accessor.NetRuntimeDownloadUrl.Should().Be(expectedUrl);
            });
    }

    public static IEnumerable<object[]> NetRuntimeLinkTestCases()
    {
        // FormUpdates builds the URL from RuntimeInformation.OSArchitecture (lowercased), so the expected URL must use the
        // same architecture; hard-coding "x64" made these cases fail on the arm64 CI runner where the URL is correctly "arm64".
        string arch = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

        yield return
        [
            new Version(8, 10, 134),
            $"https://aka.ms/dotnet-core-applaunch?missing_runtime=true&arch={arch}&rid=win-{arch}&apphost_version=8.10.134&gui=true"
        ];

        yield return
        [
            new Version(10, 0, 2),
            $"https://aka.ms/dotnet-core-applaunch?missing_runtime=true&arch={arch}&rid=win-{arch}&apphost_version=10.0.2&gui=true"
        ];

        yield return
        [
            new Version(7, 11, 10),
            $"https://aka.ms/dotnet-core-applaunch?missing_runtime=true&arch={arch}&rid=win-{arch}&apphost_version=7.11.10&gui=true"
        ];
    }

    private void RunFormTest(Action<FormUpdates> testDriver)
    {
        RunFormTest(
            form =>
            {
                testDriver(form);
                return Task.CompletedTask;
            });
    }

    private void RunFormTest(Func<FormUpdates, Task> testDriverAsync)
    {
        UITest.RunForm(
            () =>
            {
                using FormUpdates form = new(new Version(4, 2, 0));
                form.ShowDialog(owner: null);
            },
            testDriverAsync);
    }

    public void Dispose()
    {
        DisposeTest();
    }
}
