namespace GitExtensions.Plugins.DeleteUnusedBranches;

public sealed class Branch(string name, DateTime date, string author, string message, bool delete)
{
    public string Name { get; } = name;
    public DateTime Date { get; } = date;
    public bool Delete { get; set; } = delete;
    public string Author { get; } = author;
    public string Message { get; } = message;
}
