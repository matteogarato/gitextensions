namespace GitExtensions.Extensibility.Git;

/// <summary>
///     Event arguments for batch progress updating
/// </summary>
public sealed class BatchProgressEventArgs(int batchItemsProcessed, bool executionResult) : EventArgs
{
    /// <summary>
    ///     Number of items processed in this batch event
    /// </summary>
    public int ProcessedCount { get; } = batchItemsProcessed;

    /// <summary>
    ///     Batch execution result
    /// </summary>
    public bool ExecutionResult { get; } = executionResult;
}
