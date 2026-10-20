using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommonTestUtils;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;

namespace GitCommandsTests;
public sealed class RevisionReaderTests
{
    private readonly Encoding _logOutputEncoding = Encoding.UTF8;
    private readonly long _sixMonths = new DateTimeOffset(new DateTime(2021, 01, 01)).ToUnixTimeSeconds();

    public RevisionReaderTests()
    {
    }

    [Fact]
    public void BuildArguments_should_be_NUL_terminated()
    {
        RevisionReader reader = RevisionReader.TestAccessor.RevisionReader(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), ""), _logOutputEncoding, _sixMonths);
        ArgumentBuilder args = reader.GetTestAccessor().BuildArguments("", "");

        args.ToString().Should().Contain(" log -z ");
    }

    [Fact]
    public void GetLog_shall_add_Autostash()
    {
        const string autostashLabel = nameof(autostashLabel);
        List<GitRevision> allRevisions = [];

        ObjectId autostashId = ObjectId.Random();
        using ReferenceRepository repo = new(createCommit: true);
        string commitHash = repo.CommitHash ?? throw new ArgumentNullException(nameof(repo.CommitHash));
        string rebaseMergeDir = Path.Combine(repo.Module.WorkingDirGitDir, "rebase-merge");
        string autostashFilename = Path.Combine(rebaseMergeDir, "autostash");
        Directory.CreateDirectory(rebaseMergeDir);
        File.WriteAllText(autostashFilename, autostashId.ToString());
        File.WriteAllText(Path.Combine(rebaseMergeDir, "orig-head"), commitHash);
        long unixTime = DateTimeUtils.ToUnixTime(File.GetLastWriteTime(autostashFilename));

        RevisionReader reader = RevisionReader.TestAccessor.RevisionReader(repo.Module, _logOutputEncoding, _sixMonths);
        Subject<IReadOnlyList<GitRevision>> observeRevisions = new();
        IDisposable revisionSubscription = observeRevisions.Subscribe(OnNextRevisions);
        reader.GetLog(observeRevisions, revisionFilter: "", pathFilter: "", hasNotes: false, autostashLabel, cancellationToken: TestContext.Current.CancellationToken);

        allRevisions.Count.Should().Be(2);
        GitRevision autostash = allRevisions[0];
        GitRevision head = allRevisions[1];

        autostash.IsArtificial.Should().BeFalse();
        autostash.IsAutostash.Should().BeTrue();
        autostash.IsStash.Should().BeFalse();
        autostash.ObjectId.Should().Be(autostashId);
        autostash.ParentIds.Should().BeEquivalentTo([head.ObjectId]);
        autostash.Subject.Should().Be(autostashLabel);
        autostash.AuthorUnixTime.Should().Be(unixTime);
        autostash.CommitUnixTime.Should().Be(unixTime);
        autostash.Author.Should().BeNull();
        autostash.AuthorEmail.Should().BeNull();
        autostash.Committer.Should().BeNull();
        autostash.CommitterEmail.Should().BeNull();

        head.ObjectId.ToString().Should().Be(commitHash);
        head.IsArtificial.Should().BeFalse();
        head.IsAutostash.Should().BeFalse();
        head.IsStash.Should().BeFalse();

        CommitData commitData = new CommitDataManager(() => repo.Module).CreateFromRevision(autostash, children: [head.ObjectId]);
        commitData.Author.Should().BeNull();
        commitData.Committer.Should().BeNull();
        commitData.ObjectId.Should().Be(autostashId);
        commitData.Body.Should().Be(autostashLabel);

        return;

        void OnNextRevisions(IReadOnlyList<GitRevision> revisions)
        {
            allRevisions.AddRange(revisions);
        }
    }

    [Fact]
    public void GetRevision_should_return_null_if_objectid_does_not_exist()
    {
        RevisionReader reader = RevisionReader.TestAccessor.RevisionReader(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), ""), _logOutputEncoding, _sixMonths);
        GitRevision? revision = reader.GetRevision(ObjectId.Random().ToString(), hasNotes: false, throwOnError: false, cancellationToken: TestContext.Current.CancellationToken);

        revision.Should().BeNull();
    }

    [Fact]
    public void GetRevision_should_return_null_if_revision_does_not_exist()
    {
        RevisionReader reader = RevisionReader.TestAccessor.RevisionReader(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), ""), _logOutputEncoding, _sixMonths);
        GitRevision? revision = reader.GetRevision("non/existing/ref", hasNotes: false, throwOnError: false, cancellationToken: TestContext.Current.CancellationToken);

        revision.Should().BeNull();
    }

    [Fact]
    public void GetRevision_should_throw_if_revision_is_artificial()
    {
        RevisionReader reader = RevisionReader.TestAccessor.RevisionReader(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), ""), _logOutputEncoding, _sixMonths);
        ((Action)(() =>
            reader.GetRevision(GitRevision.WorkTreeGuid, hasNotes: false, throwOnError: true, cancellationToken: TestContext.Current.CancellationToken))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryParseRevisionshould_return_false_if_argument_is_invalid()
    {
        ArraySegment<byte> chunk = null!;
        RevisionReader reader = RevisionReader.TestAccessor.RevisionReader(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), ""), _logOutputEncoding, _sixMonths);

        // Set to a high value so Debug.Assert do not raise exceptions
        reader.GetTestAccessor().NoOfParseError = 100;
        bool res = reader.GetTestAccessor().TryParseRevision(chunk, out _);
        res.Should().BeFalse();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]

    // Avoid launching the difftool at differences
    // APPVEYOR should be detected automatically, this forces the setting (also in local tests)
    // The popup will hang the tests without failure information
    [Theory]
    [InlineData("bad_parentid", false)]
    [InlineData("bad_parentid_length", false)]
    [InlineData("bad_sha", false)]
    [InlineData("empty", false)]
    [InlineData("illegal_timestamp", false)]
    [InlineData("multi_pathfilter", true)]
    [InlineData("no_subject", true)]
    [InlineData("normal", true)]
    [InlineData("short_sha", false)]
    [InlineData("simple_pathfilter", true)]
    [InlineData("subject_no_body", true)]
    [InlineData("empty_commit", true)]
    [InlineData("vertical_tab", true)]
    [InlineData("reflogselector", true, true, false)]
    [InlineData("reflogselector_empty", true, true)]
    [InlineData("notes_data", true, false, true, true)]
    [InlineData("notes_empty", true, false, true, true)]
    [InlineData("subject_starts_with_newline", true)]
    public async Task TryParseRevision_test(string testName, bool expectedReturn, bool hasReflogSelector = false, bool hasEmptyReflogSelector = true, bool hasNotes = false)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData/RevisionReader", testName + ".bin");
        ArraySegment<byte> chunk = File.ReadAllBytes(path);
        RevisionReader reader = RevisionReader.TestAccessor.RevisionReader(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), ""), _logOutputEncoding, _sixMonths);
        reader.GetTestAccessor().SetParserAttributes(hasReflogSelector, hasNotes);

        // Set to a high value so Debug.Assert do not raise exceptions
        reader.GetTestAccessor().NoOfParseError = 100;
        reader.GetTestAccessor().TryParseRevision(chunk, out GitRevision? rev)
            .Should().Be(expectedReturn);

        if (!expectedReturn)
        {
            return;
        }

        // No LocalTime for the time stamps
        JsonSerializerOptions timeZoneOptions = new()
        {
            Converters =
            {
                new UtcDateTimeJsonConverter(),
                new EncodingJsonConverter(),
            },
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
        };

        if (hasEmptyReflogSelector)
        {
            rev!.ReflogSelector.Should().BeNull();
        }
        else
        {
            rev!.ReflogSelector.Should().NotBeNull();
        }

        await Verifier.VerifyJson(JsonSerializer.Serialize(rev, timeZoneOptions))
            .UseParameters(testName);
    }

    /// <summary>
    ///  Serializes <see cref="DateTime"/> values as UTC to keep snapshot output stable across time zones.
    /// </summary>
    private sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetDateTime();

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    // STJ cannot serialize Encoding (contains ReadOnlySpan<byte> Preamble). Serialize as name string.
    private sealed class EncodingJsonConverter : JsonConverter<Encoding>
    {
        public override Encoding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => Encoding.GetEncoding(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, Encoding value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.WebName);
    }
}
