using GitCommands.UserRepositoryHistory.Legacy;
using NSubstitute;
using AppSettings = GitCommands.Settings.AppSettings;
using Current = GitCommands.UserRepositoryHistory;

namespace GitCommandsTests.UserRepositoryHistory.Legacy;

public class RepositoryStorageTests
{
    private readonly Current.IRepositorySerialiser<RepositoryCategory> _repositoryCategorySerialiser = null!;
    private readonly RepositoryStorage _repositoryStorage = null!;

    public RepositoryStorageTests()
    {
        _repositoryCategorySerialiser = Substitute.For<Current.IRepositorySerialiser<RepositoryCategory>>();
        _repositoryStorage = new RepositoryStorage(_repositoryCategorySerialiser);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LoadLegacy_should_return_empty_collection_if_settings_empty(string? setting)
    {
        AppSettings.SetString("repositories", setting!);
        RepositoryStorage repositoryStorage = new();
        var repositories = repositoryStorage.Load();

        repositories.Should().BeEmpty();
    }

    [Fact]
    public void LoadLegacy_should_return_empty_collection_if_failed_to_deserialise()
    {
        AppSettings.SetString("repositories", "repo");
        _repositoryCategorySerialiser.Deserialize(Arg.Any<string>()).Returns(x => null);

        var repositories = _repositoryStorage.Load();

        repositories.Should().BeEmpty();
    }

    [Fact]
    public void LoadLegacy_should_return_collection()
    {
        AppSettings.SetString("repositories", "repos");
        List<RepositoryCategory> history =
        [
            new()
            {
                Repositories =
                [
                    new Repository
                    {
                        Path = "C:\\Development\\RibbonWinForms\\", Description = "Check it out!", Anchor = "None"
                    },
                    new Repository { Path = "", Anchor = "None" }
                ],
                CategoryType = "Repositories",
                Description = "3rd Party"
            },
            new()
            {
                Repositories =
                [
                    new Repository
                    {
                        Title = "Git Extensions",
                        Path = "C:\\Development\\gitextensions\\",
                        Description = "Mega project!",
                        Anchor = "Pinned"
                    }
                ],
                CategoryType = "Repositories",
                Description = "Test"
            }
        ];
        _repositoryCategorySerialiser.Deserialize(Arg.Any<string>()).Returns(x => history);

        var repositories = _repositoryStorage.Load();

        repositories.Should().BeSameAs(history);
    }
}
