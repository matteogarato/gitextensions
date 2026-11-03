using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;
using GitUI.ScriptsEngine;

namespace GitExtensionsTests;

public sealed class ScriptInfoTests
{
    [Fact]
    public void ScriptInfo_should_round_trip_the_shared_xml_properties()
    {
        ScriptInfo expected = new()
        {
            Enabled = true,
            Name = "&Run helper",
            Command = "helper",
            Arguments = "{sHash}",
            AddToRevisionGridContextMenu = true,
            OnEvent = ScriptEvent.ShowInUserMenuBar,
            AskConfirmation = true,
            RunInBackground = true,
            IsPowerShell = true,
            HotkeyCommandIdentifier = 9008,
            Icon = "Git",
            IconFilePath = "/tmp/helper.png",
        };
        XmlSerializer serializer = new(typeof(BindingList<ScriptInfo>));
        BindingList<ScriptInfo> scripts = [expected];
        using StringWriter writer = new();
        serializer.Serialize(writer, scripts);
        string xml = writer.ToString();

        xml.Should().Contain("<OnEvent>ShowInUserMenuBar</OnEvent>");
        xml.Should().Contain("<Icon>Git</Icon>");
        xml.Should().Contain("<IconFilePath>/tmp/helper.png</IconFilePath>");

        using StringReader reader = new(xml);
        object? deserialized = serializer.Deserialize(reader);
        BindingList<ScriptInfo> roundTripped = deserialized as BindingList<ScriptInfo>
            ?? throw new InvalidOperationException("The scripts XML did not contain a script list.");
        roundTripped.Should().ContainSingle();
        roundTripped[0].Name.Should().Be(expected.Name);
        roundTripped[0].Command.Should().Be(expected.Command);
        roundTripped[0].Arguments.Should().Be(expected.Arguments);
        roundTripped[0].OnEvent.Should().Be(expected.OnEvent);
        roundTripped[0].Icon.Should().Be(expected.Icon);
        roundTripped[0].IconFilePath.Should().Be(expected.IconFilePath);
    }
}
