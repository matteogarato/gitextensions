namespace GitExtensions.Plugins.DeleteUnusedBranches;

public class CheckBoxHeaderCellEventArgs(bool checkedValue) : EventArgs
{
    public bool Checked { get; } = checkedValue;
}
