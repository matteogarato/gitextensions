using System.Reflection;

namespace GitExtUtils.GitUI;

public static class ControlUtil
{
    private static readonly MethodInfo _setStyleMethod = typeof(TabControl)
        .GetMethod("SetStyle", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>
    ///     Enumerates all descendant controls.
    /// </summary>
    public static IEnumerable<Control> FindDescendants(this Control control)
    {
        Queue<Control> queue = new();

        foreach (Control child in control.Controls)
        {
            queue.Enqueue(child);
        }

        while (queue.Count != 0)
        {
            var c = queue.Dequeue();

            yield return c;

            foreach (Control child in c.Controls)
            {
                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    ///     Enumerates all descendant controls of type <typeparamref name="T" /> in breadth-first order.
    /// </summary>
    public static IEnumerable<T> FindDescendantsOfType<T>(this Control control)
    {
        return control.FindDescendants().OfType<T>();
    }

    /// <summary>
    ///     Finds the first descendent of <paramref name="control" /> that has type
    ///     <typeparamref name="T" /> and satisfies <paramref name="predicate" />.
    /// </summary>
    public static T? FindDescendantOfType<T>(this Control control, Func<T, bool> predicate)
    {
        return control.FindDescendants().OfType<T>().Where(predicate).FirstOrDefault();
    }

    /// <summary>
    ///     Enumerates all ancestor controls.
    /// </summary>
    /// <remarks>
    ///     The returned sequence does not include <paramref name="control" />.
    /// </remarks>
    public static IEnumerable<Control> FindAncestors(this Control control)
    {
        var parent = control.Parent;

        while (parent is not null)
        {
            yield return parent;
            parent = parent.Parent;
        }
    }

    /// <summary>
    ///     Calls protected method <see cref="Control.SetStyle" />.
    /// </summary>
    public static void SetStyle(this Control control, ControlStyles styles, bool value)
    {
        _setStyleMethod.Invoke(control, [styles, value]);
    }
}
