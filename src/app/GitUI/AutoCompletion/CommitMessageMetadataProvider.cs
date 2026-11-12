namespace GitUI.AutoCompletion;

internal sealed class CommitMessageMetadataProvider : IAutoCompleteProvider
{
    private static readonly string[] _keywords =
    [
        "Co-authored-by: ",
        "Signed-off-by: ",
        "BREAKING CHANGE: ",
        "Reviewed-by: ",
        "Tested-by: "
    ];

    private static readonly AutoCompleteWord[] _autoCompleteWords =
        Array.ConvertAll(_keywords, k => new AutoCompleteWord(k));

    Task<IEnumerable<AutoCompleteWord>> IAutoCompleteProvider.GetAutoCompleteWordsAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_autoCompleteWords.AsEnumerable());
    }
}
