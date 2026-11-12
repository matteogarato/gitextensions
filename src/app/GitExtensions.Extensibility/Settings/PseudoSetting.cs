namespace GitExtensions.Extensibility.Settings;

/// <summary>
///     Not a real setting (as it save no setting value). It is used to display a control that is not a setting (linklabel,
///     text,...)
/// </summary>
public class PseudoSetting : ISetting
{
    public PseudoSetting(Control control, string caption = "")
    {
        Caption = caption;
        CustomControl = control;
    }

    public PseudoSetting(string text, string caption = "    ", int? height = null,
        Action<TextBox>? textboxSettings = null)
    {
        Caption = caption;

        TextBoxCreator = () =>
        {
            TextBox textbox = new() { ReadOnly = true, BorderStyle = BorderStyle.None, Text = text };

            if (height.HasValue)
            {
                textbox.Multiline = true;
                textbox.Height = height.Value;
            }

            textboxSettings?.Invoke(textbox);
            return textbox;
        };

        CustomControl = TextBoxCreator();
    }

    public Control? CustomControl { get; set; }

    public Func<TextBox>? TextBoxCreator { get; }

    string ISetting.Name { get; } = "PseudoSetting";
    string ISetting.Caption { get; }
}
