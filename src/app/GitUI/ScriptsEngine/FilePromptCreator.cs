namespace GitUI.ScriptsEngine;

internal interface IFilePromptCreator
{
    IUserInputPrompt Create();
}

internal sealed class FilePromptCreator : IFilePromptCreator
{
    IUserInputPrompt IFilePromptCreator.Create()
    {
        return new FormFilePrompt();
    }
}
