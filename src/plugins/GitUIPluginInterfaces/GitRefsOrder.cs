using System.ComponentModel;

namespace GitUIPluginInterfaces;

// NB: The values are fed directly into 'git for-each-ref --sort==', casing is important!
// Special handling for Default and Version.
public enum GitRefsSortBy
{
    [Description("Git default")] Default = 0,

    [Description("Author date")] Authordate,

    [Description("Committer date")] Committerdate,

    [Description("Creator date")] Creatordate,

    [Description("Tagger date")] Taggerdate,

    [Description("Alpha-numeric")] Refname,

    // NB: sort key is version:refname
    [Description("Version")] VersionRefname,

    [Description("Object size")] Objectsize,

    [Description("Originating remote")] Upstream
}

public enum GitRefsSortOrder
{
    [Description("A ↓ Z")] Ascending = 0,

    [Description("Z ↑ A")] Descending = 1
}
