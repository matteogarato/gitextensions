// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace CommonTestUtils;

/// <summary>
///     Use (within a using) to eat asserts.
/// </summary>
public sealed class NoAssertContext : IDisposable
{
    public NoAssertContext()
    {
        _sSuppressedThreads.AddOrUpdate(Environment.CurrentManagedThreadId, 1, (key, oldValue) => oldValue + 1);

        // Lock to make sure we are hooked properly if two threads come into the constructor/dispose at the same time.
        lock (_sLock)
        {
            if (!_sHooked)
            {
                // Hook our custom listener first so we don't lose assertions from other threads when
                // we disconnect the default listener.
                Trace.Listeners.Add(_sNoAssertListener);
                Trace.Listeners.Remove(_sDefaultListener);
                _sHooked = true;
            }
        }
    }

    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);

        var currentThread = Environment.CurrentManagedThreadId;
        if (_sSuppressedThreads.TryRemove(currentThread, out var count))
        {
            if (count > 1)
            {
                // We're in a nested assert context on a given thread, re-add with a decremented count.
                // This doesn't need to be atomic as we're currently on the thread that would care about
                // being rerouted.
                _sSuppressedThreads.TryAdd(currentThread, --count);
            }
        }

        lock (_sLock)
        {
            if (_sHooked && _sSuppressedThreads.IsEmpty)
            {
                // We're the first to hit the need to unhook. Add the default listener back first to
                // ensure we don't lose any asserts from other threads.
                Trace.Listeners.Add(_sDefaultListener);
                Trace.Listeners.Remove(_sNoAssertListener);
                _sHooked = false;
            }
        }
    }

    ~NoAssertContext()
    {
        // We need this class to be used in a using to effectively rationalize about a test.
        throw new InvalidOperationException($"Did not dispose {nameof(NoAssertContext)}");
    }

    private class NoAssertListener() : TraceListener(typeof(NoAssertListener).FullName)
    {
        public override void Fail(string? message)
        {
            if (!_sSuppressedThreads.TryGetValue(Environment.CurrentManagedThreadId, out _))
            {
                _sDefaultListener.Fail(message);
            }
        }

        public override void Fail(string? message, string? detailMessage)
        {
            if (!_sSuppressedThreads.TryGetValue(Environment.CurrentManagedThreadId, out _))
            {
                _sDefaultListener.Fail(message, detailMessage);
            }
        }

        // Write and WriteLine are virtual

        public override void Write(string? message)
        {
            if (!_sSuppressedThreads.TryGetValue(Environment.CurrentManagedThreadId, out _))
            {
                _sDefaultListener.Write(message);
            }
        }

        public override void WriteLine(string? message)
        {
            if (!_sSuppressedThreads.TryGetValue(Environment.CurrentManagedThreadId, out _))
            {
                _sDefaultListener.WriteLine(message);
            }
        }
    }
    // For any given thread we don't need to lock to decide how to route messages, as any messages for that
    // given thread will not happen while we're in the constructor or dispose method on that thread. That
    // means we can safely check to see if we've hooked our thread without locking (outside of using a
    // concurrent collection to make sure the collection is in a known state).
    //
    // We do, however need to lock around hooking/unhooking our custom listener to make sure that we
    // are rerouting correctly if multiple threads are creating/disposing this class concurrently.

#pragma warning disable SA1308 // Variable names should not be prefixed
    private static readonly Lock _sLock = new();
    private static bool _sHooked;

    private static readonly ConcurrentDictionary<int, int> _sSuppressedThreads = new();

    // "Default" is the listener that terminates the process when debug assertions fail.
    private static readonly TraceListener _sDefaultListener = Trace.Listeners["Default"]!;
    private static readonly NoAssertListener _sNoAssertListener = new();
#pragma warning restore SA1308 // Variable names should not be prefixed
}
