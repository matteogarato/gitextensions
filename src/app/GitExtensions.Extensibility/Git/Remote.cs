namespace GitExtensions.Extensibility.Git;

public readonly struct Remote(string name, string fetchUrl, string? firstPushUrl)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public string FetchUrl { get; } = fetchUrl ?? throw new ArgumentNullException(nameof(fetchUrl));

    public List<string> PushUrls { get; } = firstPushUrl is not null
        ? [firstPushUrl]
        : throw new ArgumentNullException(nameof(firstPushUrl));

    // At least one push URL must be added
}
