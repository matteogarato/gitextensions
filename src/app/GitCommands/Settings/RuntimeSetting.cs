namespace GitCommands.Settings;

/// <summary>
///     Represents a setting which has to be explicitly saved, if changed at runtime.
/// </summary>
/// <typeparam name="T">The type of setting.</typeparam>
public interface IRuntimeSetting<T> : ISetting<T>, IRuntimeSetting
{
    /// <summary>
    ///     Optionally calls <cref>Reload</cref> and returns the current value.
    /// </summary>
    T GetValue(bool reload = false);
}

/// <summary>
///     Represents a setting which has to be explicitly saved, if changed at runtime.
/// </summary>
/// <typeparam name="T">The type of setting.</typeparam>
/// <param name="persistentSetting">
///     The <see cref="ISetting{T}" /> instance used to persist the default value of the
///     setting.
/// </param>
public class RuntimeSetting<T>(ISetting<T> persistentSetting) : IRuntimeSetting<T>
{
    private bool _loaded;
    private T _value = persistentSetting.Default;

    T ISetting<T>.Default
    {
        get => persistentSetting.Default;
    }

    string ISetting<T>.FullPath
    {
        get => persistentSetting.FullPath;
    }

    string ISetting<T>.Name
    {
        get => persistentSetting.Name;
    }

    T ISetting<T>.Value
    {
        get => GetValue();
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value))
            {
                return;
            }

            _value = value;
        }
    }

    T IRuntimeSetting<T>.GetValue(bool reload = false)
    {
        if (reload || !_loaded)
        {
            Reload();
        }

        return _value;
    }

    void IRuntimeSetting.Reload()
    {
        Value = persistentSetting.Value;
        _loaded = true;
    }

    void IRuntimeSetting.ResetToDefault()
    {
        Value = Default;
    }

    void IRuntimeSetting.Save()
    {
        persistentSetting.Value = Value;
    }

    /// <summary>
    ///     Implicit conversion for direct access to the RuntimeSetting value.
    /// </summary>
    /// <param name="setting">The RuntimeSetting whose value is returned as conversion result.</param>
    public static implicit operator T(RuntimeSetting<T> setting)
    {
        return setting.Value;
    }
}
