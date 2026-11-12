namespace GitExtUtils.GitUI.Theming;

internal sealed class TabControlRenderer(TabControl tabs)
{
    public void Setup()
    {
        tabs.SetStyle(ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
        tabs.Paint += HandlePaint;
        tabs.Disposed += HandleDisposed;
    }

    private static void HandlePaint(object? s, PaintEventArgs e)
    {
        new TabControlPaintContext((TabControl)s!, e).Paint();
    }

    private void HandleDisposed(object? sender, EventArgs e)
    {
        tabs.Paint -= HandlePaint;
        tabs.Disposed -= HandleDisposed;
    }
}
