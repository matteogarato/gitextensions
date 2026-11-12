using GitExtensions.Extensibility.Git;
using ICSharpCode.TextEditor.Document;
using ICSharpCode.TextEditor.Document.HighlightingStrategy;
using ICSharpCode.TextEditor.Document.LineManager;
using ICSharpCode.TextEditor.Gui;

namespace GitUI.Editor;

internal sealed class RebaseTodoHighlightingStrategy(IGitModule module)
    : GitHighlightingStrategyBase("GitRebaseTodo", module)
{
    /*
    Commands:
    p, pick = use commit
    r, reword = use commit, but edit the commit message
    e, edit = use commit, but stop for amending
    s, squash = use commit, but meld into previous commit
    f, fixup = like "squash", but discard this commit's log message
    x, exec = run command (the rest of the line) using shell
    d, drop = remove commit
    */

    private readonly Dictionary<char, (string longForm, HighlightColor color, string[] options)> _commandByFirstChar =
        new()
        {
            { 'p', ("pick", new HighlightColor(nameof(SystemColors.InfoText), true, false), []) },
            {
                'r',
                ("reword",
                    new HighlightColor(Application.IsDarkModeEnabled ? Color.MediumPurple : Color.Purple, true,
                        false, false), [])
            },
            {
                'e',
                ("edit",
                    new HighlightColor(Application.IsDarkModeEnabled ? Color.LightGray : Color.DarkGray, true,
                        false, false), [])
            },
            {
                's',
                ("squash",
                    new HighlightColor(Application.IsDarkModeEnabled ? Color.CornflowerBlue : Color.DarkBlue, true,
                        false, false), [])
            },
            {
                'f',
                ("fixup",
                    new HighlightColor(Application.IsDarkModeEnabled ? Color.Coral : Color.LightCoral, true, false,
                        false), new[] { "-C", "-c" })
            },
            { 'x', ("exec", new HighlightColor(nameof(SystemColors.GrayText), true, false), []) },
            {
                'd',
                ("drop",
                    new HighlightColor(Application.IsDarkModeEnabled ? Color.IndianRed : Color.Red, true, false,
                        false), [])
            }
        };

    protected override void MarkTokens(IDocument document, IList<LineSegment> lines)
    {
        foreach (var line in lines)
        {
            if (!TryHighlightComment(document, line) &&
                !TryHighlightInteractiveRebaseCommand(document, line))
            {
                line.Words = [new TextWord(document, line, 0, line.Length, ColorNormal, true)];
            }

            document.RequestUpdate(
                new TextAreaUpdate(TextAreaUpdateType.SingleLine, line.LineNumber));
        }
    }

    private bool TryHighlightInteractiveRebaseCommand(IDocument document, LineSegment line)
    {
        if (line.Length < 1)
        {
            return false;
        }

        var c = document.GetCharAt(line.Offset);

        if (!_commandByFirstChar.TryGetValue(c, out var cmd))
        {
            return false;
        }

        var state = State.Command;
        var index = 1;
        var idStartIndex = -1;

        while (index < line.Length)
        {
            c = document.GetCharAt(line.Offset + index);

            if (c == '\r' || c == '\n')
            {
                return false;
            }

            switch (state)
            {
                case State.Command:
                    {
                        if (index == 1 && char.IsWhiteSpace(c))
                        {
                            state = State.SpacesAfterCommand;
                        }
                        else if (index == cmd.longForm.Length && char.IsWhiteSpace(c))
                        {
                            state = State.SpacesAfterCommand;
                        }
                        else if (index >= cmd.longForm.Length || c != cmd.longForm[index])
                        {
                            return false;
                        }

                        if (state == State.SpacesAfterCommand)
                        {
                            var option = cmd.options.FirstOrDefault(o =>
                                index + 1 + o.Length < line.Length &&
                                document.GetText(line.Offset + index + 1, o.Length) == o);
                            if (option is not null)
                            {
                                index += option.Length + 1;

                                continue;
                            }
                        }

                        break;
                    }

                case State.SpacesAfterCommand:
                    {
                        if (IsHexChar())
                        {
                            idStartIndex = index;
                            state = State.Id;
                        }
                        else if (!char.IsWhiteSpace(c))
                        {
                            return false;
                        }

                        break;
                    }

                case State.Id:
                    {
                        if (char.IsWhiteSpace(c))
                        {
                            var idLength = index - idStartIndex;

                            if (idLength < 4)
                            {
                                return false;
                            }

                            line.Words =
                            [
                                new TextWord(document, line, 0, idStartIndex, cmd.color, false),
                                new TextWord(document, line, idStartIndex, idLength, cmd.color, false),
                                new TextWord(document, line, index, line.Length - index, ColorNormal, true)
                            ];

                            return true;
                        }

                        if (!IsHexChar())
                        {
                            return false;
                        }

                        break;
                    }
            }

            index++;
        }

        return false;

        bool IsHexChar()
        {
            return char.IsDigit(c) || c is >= 'a' and <= 'f';
        }
    }

    private enum State
    {
        Command,
        SpacesAfterCommand,
        Id
    }
}
