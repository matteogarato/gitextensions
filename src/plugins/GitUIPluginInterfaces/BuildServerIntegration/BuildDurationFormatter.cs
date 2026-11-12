namespace GitUIPluginInterfaces.BuildServerIntegration;

public interface IBuildDurationFormatter
{
    string Format(long? durationMilliseconds);
}

public class BuildDurationFormatter : IBuildDurationFormatter
{
    string IBuildDurationFormatter.Format(long? durationMilliseconds)
    {
        return durationMilliseconds.HasValue
            ? TimeSpan.FromMilliseconds(durationMilliseconds.Value).ToString(@"mm\:ss")
            : string.Empty;
    }
}
