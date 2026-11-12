using System.Diagnostics;

namespace GitExtensions.Extensibility;

[DebuggerDisplay("{" + nameof(Arguments) + "}")]
public readonly struct ArgumentString
{
    public string? Arguments { get; }
    public int Length { get => Arguments?.Length ?? 0; }

    private ArgumentString(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        Arguments = arguments;
    }

    public static implicit operator ArgumentString(string? args)
    {
        return new ArgumentString(args ?? "");
    }

    public static implicit operator ArgumentString(ArgumentBuilder args)
    {
        return new ArgumentString(args.ToString());
    }

    public static implicit operator string(ArgumentString args)
    {
        return args.Arguments ?? "";
    }

    public override string ToString()
    {
        return Arguments ?? "";
    }
}
