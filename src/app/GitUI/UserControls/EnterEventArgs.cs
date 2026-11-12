namespace GitUI.UserControls;

public class EnterEventArgs(bool byMouse) : EventArgs
{
    public bool ByMouse { get; } = byMouse;
}
