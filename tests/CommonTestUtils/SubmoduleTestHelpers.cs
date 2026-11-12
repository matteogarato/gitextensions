using GitCommands.Submodules;
using GitExtensions.Extensibility.Git;

namespace CommonTestUtils;

public class SubmoduleTestHelpers
{
    public static async Task<SubmoduleInfoResult> UpdateSubmoduleStructureAndWaitForResultAsync(
        ISubmoduleStatusProvider provider, IGitModule module, bool updateStatus = true)
    {
        SubmoduleInfoResult? result = null;
        provider.StatusUpdated += ProviderStatusUpdated;
        try
        {
            await provider.UpdateSubmodulesStructureAsync(
                module.WorkingDir,
                string.Empty,
                updateStatus);

            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);
        }
        finally
        {
            provider.StatusUpdated -= ProviderStatusUpdated;
        }

        return result!;

        void ProviderStatusUpdated(object? sender, SubmoduleStatusEventArgs e)
        {
            result = e.Info;
        }
    }

    public static async Task UpdateSubmoduleStatusAndWaitForResultAsync(ISubmoduleStatusProvider provider,
        IGitModule module, IReadOnlyList<GitItemStatus> gitStatus, bool forceUpdate = true)
    {
        provider.StatusUpdated += ProviderStatusUpdated;

        await provider.UpdateSubmodulesStatusAsync(
            module.WorkingDir,
            gitStatus,
            forceUpdate);

        await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

        provider.StatusUpdated -= ProviderStatusUpdated;

        return;

        static void ProviderStatusUpdated(object? sender, SubmoduleStatusEventArgs e)
        {
        }
    }
}
