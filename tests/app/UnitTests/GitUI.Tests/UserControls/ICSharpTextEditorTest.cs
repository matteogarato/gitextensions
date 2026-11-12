using System.Reflection;
using System.Xml.Linq;
using ICSharpCode.TextEditor.Document.HighlightingStrategy.SyntaxModes;

namespace GitUITests.UserControls;

public sealed class IcSharpTextEditorTest
{
    [Fact]
    public void ResourceStreamNamesMatchSyntaxNodes()
    {
        var assembly = typeof(ResourceSyntaxModeProvider).Assembly;
        var resources = assembly.GetManifestResourceNames();
        var syntaxNodesResource = resources.First(r => r.Contains("SyntaxModes"));
        var xmlModes =
            XElement.Load(assembly.GetManifestResourceStream(syntaxNodesResource)!).Elements("Mode");
        var resourcesToCheck = resources.Where(r => r.EndsWith("xshd"));

        var matched = from xml in xmlModes
            join res in resourcesToCheck on xml!.Attribute("file")!.Value equals
                res.Replace("ICSharpCode.TextEditor.Resources.", "")
            select new { ResourceName = res, XMLModeFile = xml!.Attribute("file")!.Value };

        var missingInXml = resourcesToCheck.Except(matched.Select(m => m.ResourceName));

        var missingInResources = from nd in xmlModes
            where !matched.Select(m => m.XMLModeFile).Contains(nd!.Attribute("file")!.Value)
            select nd!.Attribute("file")!.Value;

        missingInXml.Any().Should()
            .BeFalse(
                "The SyntaxNodes.xml file is out of sync with the actual resources. Check the following resource names that don't exist in xml file. {0}",
                string.Join(",", missingInXml));
        missingInResources.Any().Should()
            .BeFalse(
                "The SyntaxNodes.xml file is out of sync with the actual resources. Check the following resource names that don't exist in embedded resources. {0}",
                string.Join(",", missingInResources));
    }
}
