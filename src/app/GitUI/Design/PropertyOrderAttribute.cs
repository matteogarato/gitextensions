namespace GitUI.Design;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class PropertyOrderAttribute(int order) : Attribute
{
    public int Order { get; } = order;
}
