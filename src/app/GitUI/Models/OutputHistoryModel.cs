using System.Diagnostics;
using System.Text;
using GitUI.Editor.Diff;
using ICSharpCode.TextEditor.Document.MarkerStrategy;

namespace GitUI.Models;

internal sealed class OutputHistoryModel(in int historyDepth) : IOutputHistoryProvider, IOutputHistoryRecorder
{
    private const string _endMark = "###";
    private const string _noExecutable = "---";
    private readonly List<StringBuilder> _outputHistory = new(historyDepth);

    private readonly Lock _outputHistoryLock = new();

    event EventHandler? IOutputHistoryProvider.HistoryChanged
    {
        add => HistoryChanged += value;
        remove => HistoryChanged -= value;
    }

    bool IOutputHistoryProvider.Enabled
    {
        get => _outputHistory.Capacity > 0;
    }

    string IOutputHistoryProvider.History
    {
        get
        {
            StringBuilder sb = new();
            lock (_outputHistoryLock)
            {
                foreach (var entry in _outputHistory)
                {
                    sb.Append(entry);
                }
            }

            sb.AppendLine(_endMark);

            return sb.ToString();
        }
    }

    void IOutputHistoryProvider.ClearHistory()
    {
        lock (_outputHistoryLock)
        {
            _outputHistory.Clear();
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    void IOutputHistoryRecorder.RecordHistory(in string message)
    {
        var time = DateTime.Now.ToShortTimeString();
        Add(new StringBuilder(time, time.Length + 1 + message.Length).Append(' ').AppendLine(message));
    }

    void IOutputHistoryRecorder.RecordHistory(in Exception exception)
    {
        RecordHistory(exception.ToStringDemystified());
    }

    void IOutputHistoryRecorder.RecordHistory(in RunProcessInfo runProcess)
    {
        Add(Format(runProcess));

        return;

        static StringBuilder Format(in RunProcessInfo runProcess)
        {
            StringBuilder sb = new();
            sb.Append(runProcess.FinishTime.ToShortTimeString()).Append(' ');
            if (string.IsNullOrWhiteSpace(runProcess.Executable))
            {
                sb.AppendLine(_noExecutable);
            }
            else
            {
                sb.Append(runProcess.Executable).Append(' ').AppendLine(runProcess.Arguments);
            }

            List<TextMarker> textMarkers = [];
            AnsiEscapeUtilities.ParseEscape(runProcess.Output.Trim(), sb, textMarkers, traceErrors: false);

            return sb.AppendLine().AppendLine();
        }
    }

    private event EventHandler? HistoryChanged;

    private void Add(in StringBuilder entry)
    {
        if (!Enabled)
        {
            return;
        }

        lock (_outputHistoryLock)
        {
            if (_outputHistory.Count == _outputHistory.Capacity)
            {
                _outputHistory.RemoveAt(0);
            }

            _outputHistory.Add(entry);
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
