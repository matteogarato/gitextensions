using GitCommands.Settings;

namespace GitCommands.UserRepositoryHistory.Legacy;

/// <summary>
///     Provides the ability to persist and retrieve collections of user's git repositories.
/// </summary>
public interface IRepositoryStorage
{
    /// <summary>
    ///     Loads a collection of categorised user's git repositories (legacy).
    /// </summary>
    /// <returns>A collection of categorised user's git repositories.</returns>
    IReadOnlyList<RepositoryCategory> Load();

    /// <summary>
    ///     Removes the legacy categorised user's git repositories history
    ///     after it has been migrated to the new format.
    /// </summary>
    void Save();
}

/// <summary>
///     Persists and retrieves collections of user's git repositories.
/// </summary>
public sealed class RepositoryStorage(IRepositorySerialiser<RepositoryCategory> repositoryCategorySerialiser)
    : IRepositoryStorage
{
    private const string _keyHistory = "repositories";
    private const string _keyHistoryBackup = "repositories-backup";

    public RepositoryStorage()
        : this(new RepositoryCategoryXmlSerialiser())
    {
    }

    /// <summary>
    ///     Loads a collection of categorised user's git repositories (legacy).
    /// </summary>
    /// <returns>A collection of categorised user's git repositories.</returns>
    IReadOnlyList<RepositoryCategory> IRepositoryStorage.Load()
    {
        var legacySetting = AppSettings.GetString(_keyHistory, null);
        if (string.IsNullOrWhiteSpace(legacySetting))
        {
            return [];
        }

        // backup the original setting
        AppSettings.SetString(_keyHistoryBackup, legacySetting);

        var history = repositoryCategorySerialiser.Deserialize(legacySetting);
        if (history is null)
        {
            return [];
        }

        return history;
    }

    /// <summary>
    ///     Removes the legacy collection of user's git repositories.
    /// </summary>
    void IRepositoryStorage.Save()
    {
        // remove the legacy setting
        AppSettings.SetString(_keyHistory, "");
    }
}
