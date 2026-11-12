using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.SettingControlBindings;
using JetBrains.Annotations;
using ResourceManager;

namespace GitUI.CommandsDialogs.SettingsDialog;

/// <summary>
///     set Text property in derived classes to set the title.
/// </summary>
public abstract partial class SettingsPageBase : TranslatedControl, ISettingsPage
{
    private readonly List<ISettingControlBinding> _controlBindings = [];
    private IReadOnlyList<string>? _childrenText;
    private ISettingsPageHost? _pageHost;

    protected SettingsPageBase(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        ServiceProvider = serviceProvider;
    }

    protected ISettingsPageHost PageHost
    {
        get
        {
            if (_pageHost is null)
            {
                throw new InvalidOperationException("PageHost instance was not passed to page: " + GetType().FullName);
            }

            return _pageHost;
        }
    }

    protected CheckSettingsLogic CheckSettingsLogic
    {
        get => PageHost.CheckSettingsLogic;
    }

    protected CommonLogic CommonLogic
    {
        get => CheckSettingsLogic.CommonLogic;
    }

    public virtual bool ReadOnly
    {
        get => false;
    }

    protected IGitModule? Module
    {
        get => CommonLogic.Module;
    }

    /// <summary>
    ///     Gets the instance of <see cref="IServiceProvider" /> as assigned in the constructor.
    /// </summary>
    protected internal IServiceProvider ServiceProvider { get; }

    protected ToolTip ToolTip
    {
        get => toolTip1;
    }

    /// <summary>
    ///     True during execution of LoadSettings(). Usually derived classes
    ///     apply settings to GUI controls. Some of controls trigger events -
    ///     IsLoadingSettings can be used for example to not execute the event action.
    /// </summary>
    protected bool IsLoadingSettings { get; private set; }

    /// <summary>
    ///     Indicates that settings have been loaded to the page.
    /// </summary>
    protected bool IsSettingsLoaded { get; private set; }

    public virtual Control GuiControl
    {
        get => this;
    }

    public virtual bool IsInstantSavePage
    {
        get => false;
    }

    public virtual SettingsPageReference PageReference
    {
        get => new SettingsPageReferenceByType(GetType());
    }

    public virtual string GetTitle()
    {
        return Text;
    }

    /// <summary>
    ///     Called when SettingsPage is shown (again);
    ///     e. g. after user clicked a tree item.
    /// </summary>
    public virtual void OnPageShown()
    {
        // to be overridden
    }

    void ISettingsPage.LoadSettings()
    {
        IsLoadingSettings = true;
        SettingsToPage();
        IsLoadingSettings = false;
        IsSettingsLoaded = true;
    }

    void ISettingsPage.SaveSettings()
    {
        if (!ReadOnly)
        {
            PageToSettings();
        }
    }

    /// <summary>
    ///     override to provide search keywords.
    /// </summary>
    public virtual IEnumerable<string> GetSearchKeywords()
    {
        return _childrenText ??= GetChildrenText(this);
    }

    protected virtual void Init(ISettingsPageHost pageHost)
    {
        _pageHost = pageHost;
    }

    public static T Create<[MeansImplicitUse] T>(ISettingsPageHost pageHost, IServiceProvider serviceProvider)
        where T : SettingsPageBase
    {
        var result = (T?)Activator.CreateInstance(typeof(T), serviceProvider);
        Validates.NotNull(result);

        result.AdjustForDpiScaling();
        result.EnableRemoveWordHotkey();
        result.FixVisualStyle();

        result.Init(pageHost);
        return result;
    }

    protected virtual void SettingsToPage()
    {
        foreach (var cb in _controlBindings)
        {
            cb.LoadSetting(GetCurrentSettings());
        }
    }

    protected virtual void PageToSettings()
    {
        foreach (var cb in _controlBindings)
        {
            cb.SaveSetting(GetCurrentSettings());
        }
    }

    protected abstract SettingsSource GetCurrentSettings();

    public void AddControlBinding(ISettingControlBinding binding)
    {
        _controlBindings.Add(binding);
    }

    protected void AddSettingBinding(ISetting<string> setting, ComboBox comboBox)
    {
        StringComboBoxAdapter adapter = new(setting, comboBox);
        AddControlBinding(SettingControlBindingsProvider.CreateControlBinding(adapter));
    }

    /// <summary>
    ///     Gets the <see cref="Control.Text" /> values of <paramref name="control" />
    ///     and all its descendants.
    /// </summary>
    private static IReadOnlyList<string> GetChildrenText(Control control)
    {
        List<string> texts = [];

        Queue<Control> queue = new();
        queue.Enqueue(control);

        while (queue.Count != 0)
        {
            var next = queue.Dequeue();

            if (!next.Visible || next is NumericUpDown)
            {
                // skip: invisible; some input controls
                continue;
            }

            if (next.Enabled && !string.IsNullOrWhiteSpace(next.Text))
            {
                // enabled AND not whitespace -> add
                // also searches text boxes and comboboxes
                // TODO(optional): search through the drop down list of comboboxes
                // TODO(optional): convert numeric dropdown values to text
                texts.Add(next.Text.Trim());
            }

            foreach (Control child in next.Controls)
            {
                queue.Enqueue(child);
            }
        }

        return texts;
    }
}

public class StringComboBoxAdapter : ChoiceSetting
{
    public StringComboBoxAdapter(ISetting<string> setting, ComboBox comboBox)
        : base(setting.FullPath, [.. comboBox.Items.Cast<string>()], setting.Default)
    {
        CustomControl = comboBox;
    }
}
