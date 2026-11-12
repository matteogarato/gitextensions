using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using GitExtUtils.GitUI;
using Microsoft.VisualStudio.Threading;
using Xunit.v3;

namespace CommonTestUtils;

/// <summary>
///     Configures <see cref="ThreadHelper.JoinableTaskContext" /> around each test and joins pending operations
///     afterwards.
///     Apply it at assembly level. Tests that need the main thread must run on an STA thread (e.g. <c>[StaFact]</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ConfigureJoinableTaskFactoryAttribute : BeforeAfterTestAttribute
{
    // xUnit creates a new attribute instance per access, so the state shared between Before and After must be static.
    // Tests using the joinable task context must not run in parallel, so a single slot is sufficient.
    private static DenyExecutionSynchronizationContext? _denyExecutionSynchronizationContext;
    private static HangReporter? _hangReporter;
    private static ExceptionDispatchInfo? _threadException;

    static ConfigureJoinableTaskFactoryAttribute()
    {
        Application.ThreadException += HandleApplicationThreadException;
    }

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        ThreadHelper.HasJoinableTaskContext.Should().BeFalse("Tests with joinable tasks must not be run in parallel!");

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            _denyExecutionSynchronizationContext =
                new DenyExecutionSynchronizationContext(SynchronizationContext.Current);
            ThreadHelper.JoinableTaskContext = new JoinableTaskContext(_denyExecutionSynchronizationContext.MainThread,
                _denyExecutionSynchronizationContext);
            return;
        }

        _denyExecutionSynchronizationContext = null;

        // This form is created to obtain a UI synchronization context only.
        using (new Form())
        {
            // Store the shared JoinableTaskContext
            ThreadHelper.JoinableTaskContext = new JoinableTaskContext();
            _hangReporter = new HangReporter(ThreadHelper.JoinableTaskContext);
        }
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        try
        {
            try
            {
                // Wait for eventual pending operations triggered by the test.
                using CancellationTokenSource cts = new(AsyncTestHelper.UnexpectedTimeout);
                try
                {
                    ThreadHelper.CancelSwitchToMainThread();

                    // Note that ThreadHelper.JoinableTaskContext.Factory must be used to bypass the default behavior of
                    // ThreadHelper.JoinableTaskFactory since the latter adds new tasks to the collection and would therefore
                    // never complete.
                    ThreadHelper.JoinableTaskContext.Factory.Run(() =>
                        ThreadHelper.JoinPendingOperationsAsync(cts.Token));
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    if (int.TryParse(Environment.GetEnvironmentVariable("GE_TEST_SLEEP_SECONDS_ON_HANG"),
                            out var sleepSeconds) && sleepSeconds > 0)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
                    }

                    throw;
                }
            }
            finally
            {
                ThreadHelper.JoinableTaskContext = null!;
                if (_denyExecutionSynchronizationContext is not null)
                {
                    SynchronizationContext.SetSynchronizationContext(_denyExecutionSynchronizationContext
                        .UnderlyingContext);
                }
            }

            _denyExecutionSynchronizationContext?.ThrowIfSwitchOccurred();
        }
        catch (Exception ex) when (_threadException is not null)
        {
            StoreThreadException(ex);
        }
        finally
        {
            try
            {
                // Deferred clean-up (e.g. temp repositories) must run only after all pending operations were joined above.
                Epilogue.ExecuteAfterTestActions();
            }
            finally
            {
                // Reset _threadException to null, and throw if it was set during the current test.
                Interlocked.Exchange(ref _threadException, null)?.Throw();
            }
        }
    }

    private static void HandleApplicationThreadException(object? sender, ThreadExceptionEventArgs e)
    {
        StoreThreadException(e.Exception);
    }

    private static void StoreThreadException(Exception ex)
    {
        if (_threadException is not null)
        {
            ex = new AggregateException(_threadException.SourceException, ex);
        }

        _threadException = ExceptionDispatchInfo.Capture(ex);
    }

    private class DenyExecutionSynchronizationContext : SynchronizationContext
    {
        private readonly StrongBox<ExceptionDispatchInfo> _failedTransfer;

        public DenyExecutionSynchronizationContext(SynchronizationContext? underlyingContext)
            : this(underlyingContext, null, null)
        {
        }

        private DenyExecutionSynchronizationContext(SynchronizationContext? underlyingContext, Thread? mainThread,
            StrongBox<ExceptionDispatchInfo>? failedTransfer)
        {
            UnderlyingContext = underlyingContext;
            MainThread = mainThread ?? new Thread(MainThreadStart);
            _failedTransfer = failedTransfer ?? new StrongBox<ExceptionDispatchInfo>();
        }

        internal SynchronizationContext? UnderlyingContext { get; }

        internal Thread MainThread { get; }

        private static void MainThreadStart()
        {
            throw new InvalidOperationException("This thread should never be started.");
        }

        internal void ThrowIfSwitchOccurred()
        {
            _failedTransfer.Value?.Throw();
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            try
            {
                if (_failedTransfer.Value is null)
                {
                    ThrowFailedTransferExceptionForCapture();
                }
            }
            catch (InvalidOperationException e)
            {
                _failedTransfer.Value = ExceptionDispatchInfo.Capture(e);
            }

#pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs
            (UnderlyingContext ?? new SynchronizationContext()).Post(d, state);
#pragma warning restore VSTHRD001 // Avoid legacy thread switching APIs
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            try
            {
                if (_failedTransfer.Value is null)
                {
                    ThrowFailedTransferExceptionForCapture();
                }
            }
            catch (InvalidOperationException e)
            {
                _failedTransfer.Value = ExceptionDispatchInfo.Capture(e);
            }

#pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs
            (UnderlyingContext ?? new SynchronizationContext()).Send(d, state);
#pragma warning restore VSTHRD001 // Avoid legacy thread switching APIs
        }

        public override SynchronizationContext CreateCopy()
        {
            return new DenyExecutionSynchronizationContext(UnderlyingContext?.CreateCopy(), MainThread,
                _failedTransfer);
        }

        private static void ThrowFailedTransferExceptionForCapture()
        {
            throw new InvalidOperationException(
                "Tests cannot use SwitchToMainThreadAsync unless they are marked with ApartmentState.STA.");
        }
    }

    private sealed class HangReporter : JoinableTaskContextNode
    {
        public HangReporter(JoinableTaskContext context)
            : base(context)
        {
            RegisterOnHangDetected();
        }

        protected override void OnHangDetected(TimeSpan hangDuration, int notificationCount, Guid hangId)
        {
            if (notificationCount > 1)
            {
                return;
            }

            StringBuilder output = new();
            output.AppendLine();
            output.AppendLine($"HANG DETECTED: guid {hangId}");

            var report = ((IHangReportContributor)Context).GetHangReport();
            if (report.ContentName!.EndsWith("dgml", StringComparison.InvariantCultureIgnoreCase))
            {
                try
                {
                    var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                    var reportLocation = Path.GetDirectoryName(assemblyLocation);
                    var reportFileName = Path.Combine(reportLocation!, $"{hangId}.dgml");

                    File.WriteAllText(reportFileName, report.Content);
                    output.AppendLine($"HANG report: {reportFileName}");
                }
                catch
                {
                    /* no-op */
                }
            }
            else
            {
                output.AppendLine(report.ContentName);
                output.AppendLine(report.ContentType);
                output.AppendLine(report.Content);
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(output.ToString());
            Console.ResetColor();

            // Allow seeing the output in Release builds
            Trace.WriteLine(output.ToString());

            if (Environment.GetEnvironmentVariable("GE_TEST_LAUNCH_DEBUGGER_ON_HANG") != "1")
            {
                return;
            }

            Console.WriteLine("launching debugger...");

            Debugger.Launch();
            Debugger.Break();
        }
    }
}
