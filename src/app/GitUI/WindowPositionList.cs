using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;
using GitCommands.Settings;

namespace GitUI;

/// <summary>
///     Stores the state and position of a single window.
/// </summary>
[DebuggerDisplay("Name={Name} Rect={Rect} DeviceDpi={DeviceDpi} State={State}")]
public class WindowPosition
{
    protected WindowPosition()
    {
        DeviceDpi = 96;
    }

    public WindowPosition(Rectangle rect, int deviceDpi, FormWindowState state, string name)
    {
        Rect = rect;
        DeviceDpi = deviceDpi;
        State = state;
        Name = name;
    }

    public Rectangle Rect { get; set; }

    [DefaultValue(96)] public int DeviceDpi { get; set; }

    public FormWindowState State { get; set; }
    public string? Name { get; set; }
}

public class WindowPositionList
{
    private static readonly string _configFilePath =
        Path.Join(AppSettings.LocalApplicationDataPath.Value!, "WindowPositions.xml");

    private static readonly XmlSerializer _serializer = new(typeof(WindowPositionList));

    protected WindowPositionList()
    {
    }

    public List<WindowPosition> WindowPositions { get; set; } = [];

    public WindowPosition? Get(string name)
    {
        return WindowPositions.FirstOrDefault(r => r.Name == name);
    }

    public void AddOrUpdate(WindowPosition pos)
    {
        WindowPositions.RemoveAll(r => r.Name == pos.Name);
        WindowPositions.Add(pos);
    }

    public static WindowPositionList? Load()
    {
        if (!File.Exists(_configFilePath))
        {
            return new WindowPositionList();
        }

        try
        {
            using var stream = File.Open(_configFilePath, FileMode.OpenOrCreate, FileAccess.Read,
                FileShare.ReadWrite);
            return (WindowPositionList?)_serializer.Deserialize(stream);
        }
        catch
        {
            return new WindowPositionList();
        }
    }

    public void Save()
    {
        using var stream = File.Open(_configFilePath, FileMode.Create, FileAccess.Write);
        _serializer.Serialize(stream, this);
    }
}
