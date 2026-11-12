using StrongOf;

namespace GitUI.UserControls;

partial class FileStatusList
{
    public sealed class GroupKey(string value) : StrongString<GroupKey>(value)
    {
    }
}
