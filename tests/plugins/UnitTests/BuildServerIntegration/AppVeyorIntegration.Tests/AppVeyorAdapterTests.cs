using System.Reflection;
using System.Runtime.CompilerServices;
using CommonTestUtils;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Plugins.AppVeyorIntegration;
using GitUIPluginInterfaces.BuildServerIntegration;
using NSubstitute;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace AppVeyorIntegrationTests;

public class AppVeyorAdapterTests
{
    private const string _projectId = "account/repo";

    [Fact]
    public void Should_return_no_build_Info_When_Api_Json_is_empty()
    {
        var buildInfo = new AppVeyorAdapter().ExtractBuildInfo(_projectId, string.Empty);

        buildInfo.Should().BeEmpty();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [Fact]
    public async Task Should_return_a_build_Info_When_Json_content_is_the_one_of_a_pull_request_build()
    {
        await Verify(BuildBuildInfoForFile("AppVeyorResult_pull_request_build.json")).UseDirectory("ApprovedFiles");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [Fact]
    public async Task Should_return_a_build_Info_When_Json_content_is_the_one_of_a_master_build()
    {
        await Verify(BuildBuildInfoForFile("AppVeyorResult_master.json")).UseDirectory("ApprovedFiles");
    }

    private string BuildBuildInfoForFile(string filename)
    {
        var resultString = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.{filename}");
        AppVeyorAdapter appVeyorAdapter = new();
        appVeyorAdapter.Initialize(Substitute.For<IBuildServerWatcher>(), Substitute.For<SettingsSource>(), () => { },
            id => true);

        List<AppVeyorBuildInfo> buildInfo = [.. appVeyorAdapter.ExtractBuildInfo(_projectId, resultString)];
        return YamlSerialize(buildInfo);
    }

    private static string YamlSerialize(List<AppVeyorBuildInfo> buildInfo)
    {
        var serializer = new SerializerBuilder()
            .WithTypeConverter(new CommitsYamlTypeConverter())
            .Build();

        return serializer.Serialize(buildInfo);
    }
}

public class CommitsYamlTypeConverter : IYamlTypeConverter
{
    bool IYamlTypeConverter.Accepts(Type type)
    {
        if (type == typeof(ObjectId)
            || type == typeof(ObjectId[])
            || type == typeof(DateTime))
        {
            return true;
        }

        return false;
    }

    object IYamlTypeConverter.ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        throw new NotImplementedException();
    }

    void IYamlTypeConverter.WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (type == typeof(DateTime))
        {
            emitter.Emit(new Scalar(null, ((DateTime)value!).ToUniversalTime().ToString("O")));
        }

        if (type == typeof(ObjectId))
        {
            emitter.Emit(new Scalar(null, value!.ToString()!));
        }

        if (type == typeof(ObjectId[]))
        {
            var commits = (ObjectId[])value!;
            emitter.Emit(new SequenceStart(null, null, false, SequenceStyle.Block));

            foreach (var commit in commits)
            {
                emitter.Emit(new Scalar(null, commit.ToString()));
            }

            emitter.Emit(new SequenceEnd());
        }
    }
}
