using GitExtUtils.GitUI;

namespace GitExtUtilsTests;

public class ComboBoxExtensionsTests
{
    [StaFact]
    public void AdjustWidthToFitContent_should_throw_if_combo_null()
    {
        ((Action)(() => ((ComboBox)null!).AdjustWidthToFitContent())).Should().Throw<ArgumentNullException>();
    }

    [StaFact]
    public void ResizeDropDownWidth_ComboBox_should_throw_if_combo_null()
    {
        ((Action)(() => ((ComboBox)null!).ResizeDropDownWidth(1, 2))).Should().Throw<ArgumentNullException>();
    }

    [StaFact]
    public void ResizeDropDownWidth_ToolStripComboBox_should_throw_if_combo_null()
    {
        ((Action)(() => ((ToolStripComboBox)null!).ResizeDropDownWidth(1, 2))).Should().Throw<ArgumentNullException>();
    }

    [StaFact]
    public void AdjustWidthToFitContent_ComboBox_Should_Account_For_Scrollbar()
    {
        // Arrange
        var somewhatLongString = "This is a somewhat long string to force ComboBox drop-down to be adjusted";

        using ComboBox comboBox = new();
        comboBox.Items.Add(somewhatLongString);

        var initialWidth = comboBox.Width;

        // Act
        comboBox.AdjustWidthToFitContent();
        var adjustedWidth1 = comboBox.Width;

        comboBox.Items.Add(somewhatLongString);
        comboBox.AdjustWidthToFitContent();
        var adjustedWidth2 = comboBox.Width;

        for (var i = 0; i < 50; i++)
        {
            comboBox.Items.Add(somewhatLongString);
        }

        comboBox.AdjustWidthToFitContent();
        var adjustedWidth52 = comboBox.Width;

        // Assert
        adjustedWidth1.Should().BeGreaterThan(initialWidth);
        // For some reason in headless mode a vertical scrollbar appears with just 2 items
        adjustedWidth2.Should().BeGreaterThan(adjustedWidth1);
        adjustedWidth52.Should().Be(adjustedWidth2);
    }

    [StaFact]
    public void ResizeDropDownWidth_ToolStripComboBox_Sets_Width_To_A_Minimum_When_Its_Higher_Than_Content_Width()
    {
        // Arrange
        var veryLongString = string.Join(", ", Enumerable.Repeat("A very long string", 20));

        using ToolStripComboBox comboBox = new();
        comboBox.Items.Add(veryLongString);

        // Act
        comboBox.ResizeDropDownWidth(4000, 5000);

        // Assert
        comboBox.DropDownWidth.Should().Be(4000);
    }

    [StaFact]
    public void ResizeDropDownWidth_ToolStripComboBox_Caps_At_Maximum_When_Item_Too_Long()
    {
        // Arrange
        var veryLongString = string.Join(", ", Enumerable.Repeat("A very long string", 20));

        using ToolStripComboBox comboBox = new();
        comboBox.Items.Add(veryLongString);

        // Act
        comboBox.ResizeDropDownWidth(1, 400);

        // Assert
        comboBox.DropDownWidth.Should().Be(400);
    }

    [StaFact]
    public void ResizeDropDownWidth_ToolStripComboBox_Respects_DisplayMember()
    {
        // Arrange
        var veryLongString = string.Join(", ", Enumerable.Repeat("A very long string", 20));

        using ComboBox comboBox = new() { DisplayMember = "Value" };
        comboBox.Items.Add(new ComboBoxItem(veryLongString, ""));

        // Act
        comboBox.ResizeDropDownWidth(1, 400);

        // Assert
        comboBox.DropDownWidth.Should().Be(400);
    }

    [StaFact]
    public void ResizeDropDownWidth_ToolStripComboBox_Respects_DisplayMember_When_Value_Is_Null()
    {
        // Arrange
        var veryLongString = string.Join(", ", Enumerable.Repeat("A very long string", 20));

        using ComboBox comboBox = new() { DisplayMember = "Value" };
        comboBox.Items.Add(new ComboBoxItem(null, veryLongString));

        // Act
        comboBox.ResizeDropDownWidth(1, 400);

        // Assert
        comboBox.DropDownWidth.Should().BeLessThan(400);
    }

    [StaFact]
    public void ResizeDropDownWidth_ToolStripComboBox_Uses_ToString_Value()
    {
        // Arrange
        var veryLongString = string.Join(", ", Enumerable.Repeat("A very long string", 20));

        using ComboBox comboBox = new();
        comboBox.Items.Add(new ComboBoxItem("", veryLongString));

        // Act
        comboBox.ResizeDropDownWidth(1, 400);

        // Assert
        comboBox.DropDownWidth.Should().Be(400);
    }

    [StaFact]
    public void ResizeDropDownWidth_ToolStripComboBox_Uses_ToString_Value_When_DisplayMember_Is_Not_On_Item()
    {
        // Arrange
        var veryLongString = string.Join(", ", Enumerable.Repeat("A very long string", 20));

        using ComboBox comboBox = new() { DisplayMember = "NonExistentMemberName" };
        comboBox.Items.Add(new ComboBoxItem("", veryLongString));

        // Act
        comboBox.ResizeDropDownWidth(1, 400);

        // Assert
        comboBox.DropDownWidth.Should().Be(400);
    }

    private record ComboBoxItem(string? Value, string ToStringValue)
    {
        public override string ToString()
        {
            return ToStringValue;
        }
    }
}
