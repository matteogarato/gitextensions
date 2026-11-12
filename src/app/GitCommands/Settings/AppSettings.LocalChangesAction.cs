namespace GitCommands.Settings;

public enum LocalChangesAction
{
    // DO NOT RENAME THESE -- doing so will break user preferences
    DontChange,
    Merge,
    Reset,
    Stash
}
