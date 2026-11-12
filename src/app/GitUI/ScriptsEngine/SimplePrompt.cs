namespace GitUI.ScriptsEngine;

internal partial class SimplePrompt : Form, IUserInputPrompt
{
    public SimplePrompt(string? title, string? label, string? defaultValue)
    {
        InitializeComponent();
        txtUserInput.Text = defaultValue;

        if (!string.IsNullOrWhiteSpace(title))
        {
            Text = title;
        }

        if (!string.IsNullOrWhiteSpace(label))
        {
            labelInput.Text = $"{label}:";
        }
    }

    private string UserInput { get; set; } = "";

    string IUserInputPrompt.UserInput
    {
        get => UserInput;
    }

    private void btnOk_Click(object sender, EventArgs e)
    {
        UserInput = txtUserInput.Text;
        Close();
    }

    private void SimplePrompt_Shown(object sender, EventArgs e)
    {
        txtUserInput.Focus();
    }

    private void txtUserInput_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == (char)Keys.Escape)
        {
            if (txtUserInput.SelectionLength == 0)
            {
                DialogResult = DialogResult.Cancel;
            }
            else
            {
                txtUserInput.SelectionLength = 0;
            }

            e.Handled = true;
        }
    }
}
