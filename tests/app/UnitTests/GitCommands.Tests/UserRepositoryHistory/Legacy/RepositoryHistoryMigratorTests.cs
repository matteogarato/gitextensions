using System.Reflection;
using CommonTestUtils;
using GitCommands.UserRepositoryHistory.Legacy;
using NSubstitute;
using Current = GitCommands.UserRepositoryHistory;

namespace GitCommandsTests.UserRepositoryHistory.Legacy;

public class RepositoryHistoryMigratorTests
{
    private readonly RepositoryHistoryMigrator _historyMigrator = null!;
    private readonly IRepositoryStorage _repositoryStorage = null!;

    public RepositoryHistoryMigratorTests()
    {
        _repositoryStorage = Substitute.For<IRepositoryStorage>();
        _historyMigrator = new RepositoryHistoryMigrator(_repositoryStorage);
    }

    [Fact]
    public void MigrateAsync_should_throw_if_currentHistory_null()
    {
        var f = async () => { await _historyMigrator.MigrateAsync(null!); };
        f.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task MigrateSettings_should_migrate_old_categorised_settings()
    {
        var xml = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.CategorisedRepositories02.xml");
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new FileFormatException("Unexpected data");
        }

        _repositoryStorage.Load().Returns(x => new RepositoryCategoryXmlSerialiser().Deserialize(xml));

        (var currentHistory, var migrated) =
            await _historyMigrator.MigrateAsync(new List<Current.Repository>());

        currentHistory.Should().HaveCount(8);
        currentHistory.Count(r => r.Category == "Git Extensions").Should().Be(1);
        currentHistory.Count(r => r.Category == "3rd Party").Should().Be(2);
        currentHistory.Count(r => r.Category == "Tests").Should().Be(5);

        migrated.Should().BeTrue();
    }
}
