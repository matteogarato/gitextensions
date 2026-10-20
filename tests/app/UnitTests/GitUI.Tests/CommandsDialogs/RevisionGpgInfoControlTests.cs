using GitCommands.Git.Gpg;
using GitUI;
using GitUI.CommandsDialogs;

namespace GitUITests.CommandsDialogs;

public class RevisionGpgInfoControlTests : IDisposable
{
    public RevisionGpgInfoControlTests()
    {
        GitModuleForm.IsUnitTestActive = true;
    }

    public void Dispose()
    {
        GitModuleForm.IsUnitTestActive = false;
    }

    [StaFact]
    public void Commit_must_not_be_reported_as_unsigned_before_it_was_verified()
    {
        using RevisionGpgInfoControl control = new();

        control.GetTestAccessor().CommitGpgInfoText.Should().Be(TranslatedStrings.LoadingData);
    }

    [StaFact]
    public void DisplayVerificationPending_must_drop_the_result_of_the_previous_revision()
    {
        using RevisionGpgInfoControl control = new();
        control.DisplayGpgInfo(new GpgInfo(CommitStatus.GoodSignature, "Good signature", TagStatus.NoTag, TagVerificationMessage: null));

        control.DisplayVerificationPending();

        RevisionGpgInfoControl.TestAccessor accessor = control.GetTestAccessor();
        accessor.CommitGpgInfoText.Should().Be(TranslatedStrings.LoadingData);
        accessor.CommitSignPictureVisible.Should().BeFalse();
    }

    [StaFact]
    public void DisplayGpgInfo_must_report_an_unsigned_commit_once_it_was_verified()
    {
        using RevisionGpgInfoControl control = new();

        control.DisplayGpgInfo(null);

        control.GetTestAccessor().CommitGpgInfoText.Should().Be("Commit is not signed");
    }
}
