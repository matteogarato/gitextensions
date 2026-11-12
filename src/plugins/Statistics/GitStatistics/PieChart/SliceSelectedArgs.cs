namespace GitExtensions.Plugins.GitStatistics.PieChart;

public class SliceSelectedArgs(decimal value, string toolTip, object? tag) : EventArgs
{
    public SliceSelectedArgs(decimal value, string toolTip) : this(value, toolTip, null)
    {
    }

    public object? Tag { get; } = tag;
    public string ToolTip { get; } = toolTip;
    public decimal Value { get; } = value;
}
