namespace GitUI.CommandsDialogs;

public partial class FormClone
{
    private record RemoteActionResult<TResult>(TResult? Result, bool AuthenticationFail, bool HostKeyFail);
}
