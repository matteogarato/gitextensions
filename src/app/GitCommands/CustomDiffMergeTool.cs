namespace GitCommands;

public class CustomDiffMergeTool(ToolStripMenuItem menuItem, EventHandler click)
{
    public ToolStripMenuItem MenuItem { get; } = menuItem;
    public EventHandler Click { get; } = click;
}
