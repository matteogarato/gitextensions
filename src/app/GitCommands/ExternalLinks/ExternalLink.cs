namespace GitCommands.ExternalLinks;

public sealed class ExternalLink(string? caption, string uri)
{
    public string? Caption { get; } = caption;
    public string Uri { get; } = uri;

    public override bool Equals(object? obj)
    {
        return obj is ExternalLink link && string.Equals(Uri, link.Uri);
    }

    public override int GetHashCode()
    {
        return Uri.GetHashCode();
    }
}
