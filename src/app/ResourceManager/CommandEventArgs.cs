namespace ResourceManager;

public class CommandEventArgs(string command, string? data) : EventArgs
{
    public string Command { get; } = command;
    public string? Data { get; } = data;
}
