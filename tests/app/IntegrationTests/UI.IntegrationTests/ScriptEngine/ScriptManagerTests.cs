using System.ComponentModel;
using GitCommands.Settings;
using GitUI.ScriptsEngine;

namespace UITests.ScriptEngine;

public class ScriptManagerTests
{
    [Fact]
    public async Task Can_save_settings()
    {
        var originalScripts = AppSettings.OwnScripts;

        try
        {
            ScriptsManager scriptsManager = new();

            AppSettings.OwnScripts = "<ArrayOfScriptInfo />";

            var scripts = scriptsManager.GetScripts();

            scripts.Add(new ScriptInfo { Name = "name", Command = "cmd", Arguments = "args" });

            var xml = scriptsManager.SerializeIntoXml();

            // Verify as a string, as the xml verifier ignores line breaks.
            await VerifyXml(xml);
        }
        finally
        {
            AppSettings.OwnScripts = originalScripts;
        }
    }
}
