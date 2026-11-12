using System.ComponentModel;

namespace CommonTestUtils;

/// <summary>
///     Cleanup actions are coordinated by <see cref="ConfigureJoinableTaskFactoryAttribute" /> and
///     <see cref="TestAppSettingsFixture" />.
/// </summary>
public static class Epilogue
{
    private static readonly Lock _lock = new();
    private static readonly OrderedDictionary<int, Action> _afterSuiteActions = [];
    private static readonly List<Action> _afterTestActions = [];

    /// <summary>
    ///     Registers an action to be executed after the completion of
    ///     the test suite. Wait actions are executed synchronously in
    ///     the sequence indicated by <paramref name="order" />. Values
    ///     of <paramref name="order" /> must be unique.The wait action
    ///     registration is permanent. The <see cref="ExecuteAfterTestActions" /> method is
    ///     called by <see cref="TestAppSettingsFixture" /> in order to execute
    ///     the current plan of action.
    /// </summary>
    /// <param name="order">The position in sequence to run this action.</param>
    /// <param name="action">The action to be called.</param>
    public static void RegisterAfterSuiteAction(int order, Action action)
    {
        lock (_lock)
        {
            _afterSuiteActions.Add(order, action);
        }
    }

    /// <summary>
    ///     Registers an action to be performed after the completion of the
    ///     current test, including the completion of
    ///     <see cref="ConfigureJoinableTaskFactoryAttribute" />'s join of pending operations. The
    ///     <see cref="ExecuteAfterTestActions" /> method is called by that attribute in order to execute
    ///     the current plan of action. Clean-up actions are called only once
    ///     and need to be registered each time they are needed.
    /// </summary>
    /// <param name="action">The clean-up action to run after the current test.</param>
    public static void RegisterAfterTestAction(Action action)
    {
        lock (_lock)
        {
            _afterTestActions.Add(action);
        }
    }

    /// <summary>
    ///     This method supports the <see cref="Epilogue" /> infrastructure.
    ///     It is called by <see cref="ConfigureJoinableTaskFactoryAttribute" />
    ///     after the end of the current test.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void ExecuteAfterTestActions()
    {
        lock (_lock)
        {
            _afterTestActions.ForEach(action => action());
            _afterTestActions.Clear();
        }
    }

    /// <summary>
    ///     This method supports the <see cref="Epilogue" /> infrastructure.
    ///     It is called by <see cref="TestAppSettingsFixture" />
    ///     after the end of the test suite.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void ExecuteAfterSuiteActions()
    {
        lock (_lock)
        {
            _afterSuiteActions.Values.ForEach(action => action());
        }
    }
}
