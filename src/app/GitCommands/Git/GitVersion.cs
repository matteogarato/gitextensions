using System.Diagnostics;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitCommands.Git;

public class GitVersion : IComparable<GitVersion>, IGitVersion
{
    private static readonly GitVersion _unknown = new("0.0.0");
    private static readonly GitVersion _v2190 = new("2.19.0");
    private static readonly GitVersion _v2200 = new("2.20.0");
    private static readonly GitVersion _v2320 = new("2.32.0");
    private static readonly GitVersion _v2350 = new("2.35.0");
    private static readonly GitVersion _v2360 = new("2.36.0");
    private static readonly GitVersion _v2380 = new("2.38.0");
    private static readonly GitVersion _v2420 = new("2.42.0");
    private static readonly GitVersion _v2460 = new("2.46.0");

    /// <summary>
    ///     The recommended Git version (normally latest official before a GE release).
    ///     This and later versions are green in the settings check.
    /// </summary>
    public static readonly GitVersion LastRecommendedVersion = new("2.53.0");

    /// <summary>
    ///     The oldest version with reasonable reliable support in GE.
    ///     Older than this version is red in settings.
    /// </summary>
    public static readonly GitVersion LastSupportedVersion = new("2.43.0");

    /// <summary>
    ///     The oldest Git version without known incompatibilities.
    /// </summary>
    public static readonly GitVersion LastVersionWithoutKnownLimitations = new("2.15.2");

    private static readonly Dictionary<string, GitVersion> _current = [];
    private static readonly Lock _currentLock = new();
    private readonly int _a;
    private readonly int _b;
    private readonly int _c;
    private readonly int _d;

    private readonly string _fullVersionMoniker;

    public GitVersion(string? version)
    {
        _fullVersionMoniker = Fix();

        var numbers = GetNumbers();
        _a = Get(numbers, 0);
        _b = Get(numbers, 1);
        _c = Get(numbers, 2);
        _d = Get(numbers, 3);

        string Fix()
        {
            if (version is null)
            {
                return "";
            }

            const string prefix = "git version";

            if (version.StartsWith(prefix))
            {
                return version[prefix.Length..].Trim();
            }

            return version.Trim();
        }

        IReadOnlyList<int> GetNumbers()
        {
            return ParseNumbers().ToList();

            IEnumerable<int> ParseNumbers()
            {
                foreach (var number in _fullVersionMoniker.LazySplit('.'))
                {
                    if (int.TryParse(number, out int value))
                    {
                        yield return value;
                    }
                }
            }
        }

        int Get(IReadOnlyList<int> values, int index)
        {
            return index < values.Count ? values[index] : 0;
        }
    }

    /// <summary>
    ///     GitVersion for the native ("Windows") Git
    /// </summary>
    public static IGitVersion Current { get; private set; } = CurrentVersion();

    int IComparable<GitVersion>.CompareTo(GitVersion? other)
    {
        return Compare(this, other);
    }

    bool IGitVersion.IsUnknown
    {
        get => this == _unknown;
    }

    bool IGitVersion.SupportAmendCommits
    {
        get => this >= _v2320;
    }

    bool IGitVersion.SupportGuiMergeTool
    {
        get => this >= _v2200;
    }

    bool IGitVersion.SupportLsFilesFormat
    {
        get => this >= _v2420;
    }

    bool IGitVersion.SupportNewGitConfigSyntax
    {
        get => this >= _v2460;
    }

    bool IGitVersion.SupportRangeDiffPath
    {
        get => this >= _v2380;
    }

    bool IGitVersion.SupportRangeDiffTool
    {
        get => this >= _v2190;
    }

    bool IGitVersion.SupportRebaseMerges
    {
        get => this >= _v2190;
    }

    bool IGitVersion.SupportStashStaged
    {
        get => this >= _v2350;
    }

    bool IGitVersion.SupportUpdateRefs
    {
        get => this >= _v2380;
    }

    bool IGitVersion.SupportWorktreeZ
    {
        get => this >= _v2360;
    }

    int IComparable<IGitVersion>.CompareTo(IGitVersion? other)
    {
        return Compare(this, other as GitVersion);
    }

    public override string ToString()
    {
        return _fullVersionMoniker;
    }

    /// <summary>
    ///     Gets the Git version for the <paramref name="gitExecutable" />.
    /// </summary>
    /// <returns>A <see cref="GitVersion" />.</returns>
    public static IGitVersion CurrentVersion(IExecutable? gitExecutable = null)
    {
        var gitIdentifiable = gitExecutable is null
            ? AppSettings.GitCommand
            : string.IsNullOrWhiteSpace(gitExecutable.PrefixArguments)
                ? gitExecutable.Command
                : $"{gitExecutable.Command} {gitExecutable.PrefixArguments}";

        // Lock version also over the git-version call; only one gitIdentifiable at a time.
        lock (_currentLock)
        {
            if (_current.TryGetValue(gitIdentifiable, out var gitVersion) && !gitVersion.IsUnknown)
            {
                return gitVersion;
            }

            try
            {
                gitExecutable ??= new Executable(gitIdentifiable);
                var output = gitExecutable.GetOutput("--version");
                gitVersion = new GitVersion(output);
                _current[gitIdentifiable] = gitVersion;

                if (gitVersion < LastVersionWithoutKnownLimitations)
                {
                    // Report the last supported version rather than the last version without known issues
                    MessageBoxes.ShowError(null,
                        $"{gitVersion} is lower than {LastSupportedVersion}. Some commands can fail.",
                        "Unsupported Git version");
                }
            }
            catch (Exception exception)
            {
                Trace.WriteLine(exception);
                gitVersion = _unknown;
            }

            return gitVersion;
        }
    }

    public static void ResetVersion()
    {
        lock (_currentLock)
        {
            _current.Clear();
            Current = CurrentVersion();
        }
    }

    private static int Compare(GitVersion? left, GitVersion? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (right is null)
        {
            return 1;
        }

        if (left is null)
        {
            return -1;
        }

        var compareA = left._a.CompareTo(right._a);
        if (compareA != 0)
        {
            return compareA;
        }

        var compareB = left._b.CompareTo(right._b);
        if (compareB != 0)
        {
            return compareB;
        }

        var compareC = left._c.CompareTo(right._c);
        if (compareC != 0)
        {
            return compareC;
        }

        return left._d.CompareTo(right._d);
    }

    public static bool operator >(GitVersion left, GitVersion right)
    {
        return Compare(left, right) > 0;
    }

    public static bool operator <(GitVersion left, GitVersion right)
    {
        return Compare(left, right) < 0;
    }

    public static bool operator >=(GitVersion left, GitVersion right)
    {
        return Compare(left, right) >= 0;
    }

    public static bool operator <=(GitVersion left, GitVersion right)
    {
        return Compare(left, right) <= 0;
    }
}
