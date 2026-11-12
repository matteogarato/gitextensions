namespace GitUI.ScriptsEngine;

internal interface ISimplePromptCreator
{
    IUserInputPrompt Create(string? title, string? label, string? defaultValue);
}

internal sealed class SimplePromptCreator : ISimplePromptCreator
{
    IUserInputPrompt ISimplePromptCreator.Create(string? title, string? label, string? defaultValue)
    {
        return new SimplePrompt(title, label, defaultValue);
    }
}
