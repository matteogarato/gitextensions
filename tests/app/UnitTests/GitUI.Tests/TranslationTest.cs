using GitExtensions.Extensibility.Translations;
using GitExtensions.Extensibility.Translations.Xliff;
using GitUI;

namespace GitUITests;

public sealed class TranslationTest : IDisposable
{
    public TranslationTest()
    {
        GitModuleForm.IsUnitTestActive = true;
    }

    void IDisposable.Dispose()
    {
        GitModuleForm.IsUnitTestActive = false;
    }

    [StaFact]
    public void CreateInstanceOfClass()
    {
        UserEnvironmentInformation.Initialise("0123456789012345678901234567890123456789", false);

        var translatableTypes = TranslationUtil.GetTranslatableTypes();

        List<(string typeName, Exception exception)> problems = [];

        foreach (var types in translatableTypes.Values)
        {
            TranslationFile translation = new();

            foreach (var type in types)
            {
                try
                {
                    using var obj = (ITranslate)TranslationUtil.CreateInstanceOfClass(type)!;
                    obj.AddTranslationItems(translation);
                    obj.TranslateItems(translation);
                }
                catch (Exception ex)
                {
                    problems.Add((type.FullName!, ex));
                }
            }
        }

        if (problems.Count != 0)
        {
            Assert.Fail(string.Join(
                "\n\n--------\n\n",
                problems.Select(p => $"Problem with type {p.typeName}\n\n{p.exception}")));
        }
    }
}
