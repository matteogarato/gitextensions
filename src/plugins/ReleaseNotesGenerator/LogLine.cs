namespace GitExtensions.Plugins.ReleaseNotesGenerator;

public class LogLine(string hash, string message)
{
    public string Commit { get; } = hash;
    public IList<string> MessageLines { get; } = [message];
}
