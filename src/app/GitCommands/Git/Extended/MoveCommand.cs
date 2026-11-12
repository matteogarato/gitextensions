using GitExtensions.Extensibility;
using GitExtUtils;

namespace GitCommands.Git.Extended;

#pragma warning disable SA1313 // ParameterNamesMustBeginWithLowerCaseLetter
public sealed class MoveCommand(IExecutable gitExecutable) : IExtendedCommand<MoveCommand.Arguments>
#pragma warning restore SA1313 // ParameterNamesMustBeginWithLowerCaseLetter
{
    void IExtendedCommand<Arguments>.Execute(Arguments arguments)
    {
        CreateDestinationFolder(arguments, gitExecutable.WorkingDir);
        gitExecutable.Execute(GetArgumentString(arguments));
    }

    bool IExtendedCommand<Arguments>.Validate(Arguments arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments.OldName)
            || string.IsNullOrWhiteSpace(arguments.NewName)
            || arguments.OldName == arguments.NewName)
        {
            return false;
        }

        // Git does not support changing only the case of folders in Windows
        return !arguments.IsFolder || string.Compare(arguments.OldName, arguments.NewName, true) != 0 ||
               !IsRunningOnNativeWindows();

        bool IsRunningOnNativeWindows()
        {
            return OperatingSystem.IsWindows() && !PathUtil.IsWslPath(gitExecutable.WorkingDir);
        }
    }

    private static void CreateDestinationFolder(Arguments arguments, string workingDir)
    {
        var relativePath = Path.GetDirectoryName(arguments.NewName);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var fullPath = Path.Join(workingDir, relativePath);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }
    }

    public static ArgumentString GetArgumentString(Arguments arguments)
    {
        // single file: "old" -> "new"
        // folder: "old" -> "new/" renames the folder
        //         "old" -> "new" results in "new/old" - to be avoided because unexpected
        var newName = arguments.IsFolder ? arguments.NewName.EnsureTrailingPathSeparator(true) : arguments.NewName;
        return new GitArgumentBuilder("mv") { arguments.OldName.Quote(), newName.Quote() };
    }

    public record Arguments(bool IsFolder, string OldName, string NewName);
}
