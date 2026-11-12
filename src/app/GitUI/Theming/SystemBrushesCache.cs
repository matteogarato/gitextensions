using System.Runtime.InteropServices;

namespace GitUI.Theming;

internal sealed class SystemBrushesCache : IDisposable
{
    private readonly Dictionary<int, HandleRef> _cache = [];

    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var key in _cache.Keys)
        {
            DeleteObject(_cache[key].Handle);
        }

        _cache.Clear();
    }

    public IntPtr GetBrush(int colorref)
    {
        if (!_cache.TryGetValue(colorref, out var handle))
        {
            var hbrush = CreateSolidBrush(colorref);
            handle = new HandleRef(this, hbrush);

            _cache.Add(colorref, handle);
        }

        return handle.Handle;
    }
}
