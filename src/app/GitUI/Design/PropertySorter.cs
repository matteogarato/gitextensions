using System.ComponentModel;

namespace GitUI.Design;

internal sealed class PropertySorter : ExpandableObjectConverter
{
    public override bool GetPropertiesSupported(ITypeDescriptorContext? context)
    {
        return true;
    }

    public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object value,
        Attribute[]? attributes)
    {
        var pdc = TypeDescriptor.GetProperties(value, attributes);
        List<(string name, int order)> orderedProperties = [];
        foreach (PropertyDescriptor pd in pdc)
        {
            var attribute = pd.Attributes[typeof(PropertyOrderAttribute)];
            if (attribute is not null)
            {
                var poa = (PropertyOrderAttribute)attribute;
                orderedProperties.Add((pd.Name, poa.Order));
            }
            else
            {
                orderedProperties.Add((pd.Name, 100));
            }
        }

        return pdc.Sort([.. orderedProperties.OrderBy(p => p.order).Select(p => p.name)]);
    }
}
