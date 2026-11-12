namespace GitUI;

public record struct RunProcessInfo(
    string Executable,
    string Arguments,
    string Output,
    DateTime FinishTime);
