using GitCommands.Remotes;

namespace GitUI.UserControls;

public partial class RemotesComboboxControl : GitModuleControl
{
    private bool _allowMultiselect;

    public RemotesComboboxControl()
    {
        InitializeComponent();
        InitializeComplete();
        AllowMultiselect = false;
    }

    public string SelectedRemote
    {
        get => comboBoxRemotes.Text;
        set => comboBoxRemotes.Text = value;
    }

    public bool AllowMultiselect
    {
        get => _allowMultiselect;
        set
        {
            _allowMultiselect = value;
            buttonSelectMultipleRemotes.Visible = _allowMultiselect;
            if (_allowMultiselect)
            {
                throw new NotImplementedException();
            }
        }
    }

    private void RemotesComboboxControl_Load(object sender, EventArgs e)
    {
        if (Site?.DesignMode is true)
        {
            return;
        }

        ConfigFileRemoteSettingsManager remotesManager = new(() => Module);
        comboBoxRemotes.DataSource = remotesManager.LoadRemotes(false).Select(x => x.Name).ToList();
    }
}
