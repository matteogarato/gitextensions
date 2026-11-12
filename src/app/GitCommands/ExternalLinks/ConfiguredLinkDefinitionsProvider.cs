using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;
using GitExtUtils;

namespace GitCommands.ExternalLinks;

/// <summary>
///     Provides the ability to retrieves available persisted external link definitions.
/// </summary>
public interface IConfiguredLinkDefinitionsProvider
{
    /// <summary>
    ///     Loads all persisted external link definitions across all setting layers.
    /// </summary>
    IReadOnlyList<ExternalLinkDefinition> Get(DistributedSettings settings);
}

/// <summary>
///     Retrieves available persisted external link definitions.
/// </summary>
public sealed class ConfiguredLinkDefinitionsProvider(IExternalLinksStorage externalLinksStorage)
    : IConfiguredLinkDefinitionsProvider
{
    /// <summary>
    ///     Loads all persisted external link definitions across all setting layers.
    /// </summary>
    IReadOnlyList<ExternalLinkDefinition> IConfiguredLinkDefinitionsProvider.Get(DistributedSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        DistributedSettings cachedSettings = new(null, settings.SettingsCache, SettingLevel.Unknown);
        IEnumerable<ExternalLinkDefinition>? effective = externalLinksStorage.Load(cachedSettings);

        Validates.NotNull(effective);

        if (settings.LowerPriority is not null)
        {
            ConfiguredLinkDefinitionsProvider lowerPriorityLoader = new(externalLinksStorage);
            effective = effective.Union(lowerPriorityLoader.Get(settings.LowerPriority));
        }

        return effective.ToList();
    }
}
