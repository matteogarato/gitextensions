using System.ComponentModel;
using GitExtUtils.GitUI;
using GitUI.Properties;

namespace GitUI.UserControls.RevisionGrid;

public sealed class ErrorControl : UserControl
{
    private PictureBox _image = null!;

    public ErrorControl()
    {
        InitializeComponent();

        Dock = DockStyle.Fill;

        this.AdjustForDpiScaling();
    }

#pragma warning disable
    private void InitializeComponent()
    {
        _image = new PictureBox();
        ((ISupportInitialize)_image).BeginInit();
        SuspendLayout();
        //
        // _image
        //
        _image.Dock = DockStyle.Fill;
        _image.Image = Images.StatusBadgeError;
        _image.Location = new Point(0, 0);
        _image.Name = "_image";
        _image.Size = new Size(2080, 1447);
        _image.SizeMode = PictureBoxSizeMode.CenterImage;
        _image.TabIndex = 3;
        _image.TabStop = false;
        //
        // ErrorControl
        //
        AutoScaleDimensions = new SizeF(6F, 13F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_image);
        Name = "ErrorControl";
        Size = new Size(2080, 1447);
        ((ISupportInitialize)_image).EndInit();
        ResumeLayout(false);
    }
#pragma warning enable
}
