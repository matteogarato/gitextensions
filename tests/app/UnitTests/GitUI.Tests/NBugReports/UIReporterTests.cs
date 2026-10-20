using System.Text;
using GitExtensions.Extensibility;
using GitUI.NBugReports;

namespace GitUITests.NBugReports;
public sealed class UIReporterTests : IDisposable
{
    private const string RootError = "root error";

    [Theory]
    [MemberData(nameof(GetFileNameTestCases), MemberType = typeof(UIReporterTests))]
    public void GetFileName_should_extract_file_name_from_exception(Exception exception, string expectedDllName)
    {
        string result = UIReporter.TestAccessor.GetFileName(exception);
        result.Should().Be(expectedDllName);
    }

    public static IEnumerable<object[]> GetFileNameTestCases
    {
        get
        {
            // DllNotFoundException
            yield return new object[]
            {
                new DllNotFoundException("Unable to load DLL 'vcruntime140_cor3.dll' or one of its dependencies: The specified module could not be found."),
                "vcruntime140_cor3.dll"
            };

            yield return new object[]
            {
                new DllNotFoundException("Unable to load DLL 'user32.dll': File not found."),
                "user32.dll"
            };

            yield return new object[]
            {
                new DllNotFoundException("Unable to load DLL 'my.custom.dll' for some reason"),
                "my.custom.dll"
            };

            yield return new object[]
            {
                new DllNotFoundException("No quotes in this message"),
                "No quotes in this message"
            };

            yield return new object[]
            {
                new DllNotFoundException("'only_one_quote"),
                "'only_one_quote"
            };

            yield return new object[]
            {
                new DllNotFoundException(""),
                ""
            };

            yield return new object[]
            {
                new DllNotFoundException("Unable to load DLL ''"),
                ""
            };

            // FileNotFoundException
            yield return new object[]
            {
                new FileNotFoundException("Could not load file or assembly Microsoft.CSharp", "Microsoft.CSharp"),
                "Microsoft.CSharp"
            };

            yield return new object[]
            {
                new FileNotFoundException("Could not load file or assembly 'System.Data.Common, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'", fileName: "System.Data.Common, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
                "System.Data.Common"
            };

            yield return new object[]
            {
                new FileNotFoundException("Could not load file or assembly", fileName: null),
                ""
            };

            yield return new object[]
            {
                new FileNotFoundException("Could not load file or assembly MyLib", "MyLib"),
                "MyLib"
            };

            yield return new object[]
            {
                new FileNotFoundException("Could not load file or assembly 'SomeAssembly, version=1.0.0.0'", fileName: "SomeAssembly, version=1.0.0.0"),
                "SomeAssembly"
            };

            yield return new object[]
            {
                new FileNotFoundException("Could not load file or assembly", fileName: ""),
                ""
            };

            yield return new object[]
            {
                new FileNotFoundException("Assembly not found", fileName: ", version=1.0.0.0"),
                ", version=1.0.0.0"
            };

            // Other exceptions
            yield return new object[]
            {
                new Exception("No quotes in this message"),
                "unknown"
            };

            yield return new object[]
            {
                new Exception("'only_one_quote"),
                "unknown"
            };

            yield return new object[]
            {
                new InvalidOperationException("some error"),
                "unknown"
            };
        }
    }

    [Theory]
    [InlineData("System.Data.Common", true)]
    [InlineData("System.IO", true)]
    [InlineData("System.", true)]
    [InlineData("Microsoft.CSharp", true)]
    [InlineData("Microsoft.VisualBasic", true)]
    [InlineData("Microsoft.", true)]
    [InlineData("system.data.common", true)] // case-insensitive
    [InlineData("MICROSOFT.Extensions", true)] // case-insensitive
    [InlineData("System", false)]
    [InlineData("Microsoft", false)]
    [InlineData("CustomAssembly", false)]
    [InlineData("MyApp.Core", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsDotNetFrameworkAssembly_should_identify_framework_assemblies(string? assemblyName, bool expected)
    {
        bool result = UIReporter.TestAccessor.IsDotNetFrameworkAssembly(assemblyName!);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("vcruntime140_cor3.dll", true)]
    [InlineData("vcruntime140.dll", true)]
    [InlineData("VCRUNTIME140_COR3.DLL", true)] // case-insensitive
    [InlineData("some_vcruntime_file.dll", true)]
    [InlineData("vcruntime", true)]
    [InlineData("CustomDll.dll", false)]
    [InlineData("user32.dll", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsVCRuntimeDll_should_identify_vcruntime_dlls(string? dllName, bool expected)
    {
        bool result = UIReporter.TestAccessor.IsVCRuntimeDll(dllName!);
        result.Should().Be(expected);
    }

    [Fact]
    public void CreateFailedToLoadAnAssemblyReport_should_include_report_button_for_custom_assembly()
    {
        FileNotFoundException exception = new("Could not load file or assembly CustomLib", "CustomLib");

        TaskDialogPage page = UIReporter.TestAccessor.CreateFailedToLoadAnAssemblyReport(exception, isTerminating: false);

        page.Icon.Should().Be(TaskDialogIcon.Warning);
        page.AllowCancel.Should().BeFalse();
        page.Heading.Should().Contain("CustomLib");
        page.Buttons.Should().HaveCount(2);
        page.Expander.Should().NotBeNull();
        page.Expander!.Text.Should().Be(exception.Message);
    }

    [Theory]
    [MemberData(nameof(FrameworkAssemblyAndVCRuntimeTestCases))]
    public void CreateFailedToLoadAnAssemblyReport_should_not_include_report_button(Exception exception, string expectedHeadingPart)
    {
        TaskDialogPage page = UIReporter.TestAccessor.CreateFailedToLoadAnAssemblyReport(exception, isTerminating: false);

        page.Heading.Should().Contain(expectedHeadingPart);
        page.Buttons.Should().HaveCount(1);
    }

    public static IEnumerable<object[]> FrameworkAssemblyAndVCRuntimeTestCases
    {
        get
        {
            yield return new object[]
            {
                new FileNotFoundException(
                    "Could not load file or assembly 'System.Data.Common, Version=9.0.0.0'",
                    fileName: "System.Data.Common, Version=9.0.0.0"),
                "System.Data.Common"
            };

            yield return new object[]
            {
                new FileNotFoundException(
                    "Could not load file or assembly 'Microsoft.CSharp, Version=9.0.0.0'",
                    fileName: "Microsoft.CSharp, Version=9.0.0.0"),
                "Microsoft.CSharp"
            };

            yield return new object[]
            {
                new DllNotFoundException(
                    "Unable to load DLL 'vcruntime140_cor3.dll' or one of its dependencies: The specified module could not be found."),
                "vcruntime140_cor3.dll"
            };
        }
    }

    [Fact]
    public void CreateDubiousOwnershipReport_should_create_page_with_expected_buttons()
    {
        const string errorMessage = $"fatal: detected dubious ownership in repository at 'd:/repo'\nTo add an exception for this directory, call:\n\n\tgit config --global --add safe.directory d:/repo\n";

        ExternalOperationException exception = new(
            command: "git",
            workingDirectory: "d:/repo",
            innerException: new Exception(errorMessage));

        TaskDialogPage page = UIReporter.TestAccessor.CreateDubiousOwnershipReport(exception);

        page.Icon.Should().Be(TaskDialogIcon.Error);
        page.AllowCancel.Should().BeTrue();
        page.Buttons.Should().HaveCount(4);
        page.Expander.Should().NotBeNull();
        page.Expander!.Text.Should().Be(errorMessage);
    }

    public void Dispose()
    {
        UIReporter.IgnoreFailedToLoadAnAssembly = false;
    }

    [Fact]
    public void CreateErrorReport_should_show_report_bug_and_ignore_buttons_for_internal_operation()
    {
        Exception exception = new("something broke");
        StringBuilder text = new("error details");
        OperationInfo operationInfo = new() { IsExternalOperation = false, IsUserExternalOperation = false };

        TaskDialogPage page = UIReporter.TestAccessor.CreateErrorReport(exception, RootError, text, operationInfo);

        page.Icon.Should().Be(TaskDialogIcon.Error);
        page.Heading.Should().Be(RootError);
        page.AllowCancel.Should().BeFalse();
        page.Buttons.Should().HaveCount(2);
    }

    [Fact]
    public void CreateErrorReport_should_show_report_issue_and_ignore_buttons_for_external_operation()
    {
        Exception exception = new("git failed");
        StringBuilder text = new("error details");
        OperationInfo operationInfo = new() { IsExternalOperation = true, IsUserExternalOperation = false };

        TaskDialogPage page = UIReporter.TestAccessor.CreateErrorReport(exception, RootError, text, operationInfo);

        page.Icon.Should().Be(TaskDialogIcon.Warning);
        page.Buttons.Should().HaveCount(2);
    }

    [Fact]
    public void CreateErrorReport_should_show_view_details_and_ignore_buttons_for_user_external_operation()
    {
        Exception exception = new("user script failed");
        StringBuilder text = new("error details");
        OperationInfo operationInfo = new() { IsExternalOperation = true, IsUserExternalOperation = true };

        TaskDialogPage page = UIReporter.TestAccessor.CreateErrorReport(exception, RootError, text, operationInfo);

        page.Icon.Should().Be(TaskDialogIcon.Warning);
        page.Buttons.Should().HaveCount(2);
    }

    [Fact]
    public void CreateFailedToLoadAnAssemblyReport_should_show_expander_with_exception_message()
    {
        DllNotFoundException exception = new("Unable to load DLL 'vcruntime140_cor3.dll'");

        TaskDialogPage page = UIReporter.TestAccessor.CreateFailedToLoadAnAssemblyReport(exception, isTerminating: false);

        page.Expander.Should().NotBeNull();
        page.Expander!.Text.Should().Be(exception.Message);
    }

    [Fact]
    public void ReportFailedToLoadAnAssembly_should_return_true_when_IgnoreFailedToLoadAnAssembly_is_set()
    {
        UIReporter.IgnoreFailedToLoadAnAssembly = true;
        UIReporter reporter = new();

        bool result = reporter.ReportFailedToLoadAnAssembly(
            new FileNotFoundException("Could not load file or assembly CustomLib", "CustomLib"),
            isTerminating: false);

        result.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(NonAssemblyLoadExceptionTestCases))]
    public void ReportFailedToLoadAnAssembly_should_return_false_for_non_matching_exception(Exception exception)
    {
        UIReporter reporter = new();

        bool result = reporter.ReportFailedToLoadAnAssembly(exception, isTerminating: false);

        result.Should().BeFalse();
    }

    public static IEnumerable<object[]> NonAssemblyLoadExceptionTestCases
    {
        get
        {
            yield return new object[] { new Exception("some error") };
            yield return new object[] { new InvalidOperationException("something went wrong") };
            yield return new object[] { new FileNotFoundException("File not found") };
            yield return new object[] { new DllNotFoundException("Unable to load DLL 'user32.dll'") };
            yield return new object[] { new Exception("outer", new Exception("inner")) };
        }
    }
}
