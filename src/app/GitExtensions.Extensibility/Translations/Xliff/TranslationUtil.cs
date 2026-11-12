using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace GitExtensions.Extensibility.Translations.Xliff;

public static class TranslationUtil
{
    private const BindingFlags _propertyFlags
        = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
          BindingFlags.NonPublic | BindingFlags.SetProperty;

    private const BindingFlags _fieldFlags
        = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
          BindingFlags.NonPublic | BindingFlags.SetField;

    private static readonly HashSet<string> _processedAssemblies = [];

    private static readonly string[] _translatableItemInComponentNames =
    [
        "AccessibleDescription",
        "AccessibleName",
        "Caption",
        "Text",
        "ToolTipText",
        "Title"
    ];

    private static readonly string[] _unTranslatableDlLs =
    [
        "mscorlib",
        "Microsoft",
        "Presentation",
        "WindowsBase",
        "ICSharpCode",
        "access",
        "SMDiag",
        "System",
        "vshost",
        "RestSharp",
        "EnvDTE",
        "Newtonsoft",
        "ConEmuWinForms",
        "TranslationApp",
        "netstandard"
    ];

    private static readonly char _posixDirectorySeparatorChar = '/';
    private static readonly char _nativeDirectorySeparatorChar = Path.DirectorySeparatorChar;

    private static bool IsTranslatableItemInComponent(PropertyInfo property, object item)
    {
        var localizableItemNames = GetLocalizablePropertiesFromAttribute(item)
                                   ?? _translatableItemInComponentNames;

        return property.PropertyType == typeof(string) &&
               localizableItemNames.Contains(property.Name, StringComparer.Ordinal);
    }

    private static bool AllowTranslateProperty([NotNullWhen(true)] string? text)
    {
        if (text is null)
        {
            return false;
        }

        return text.Any(char.IsLetter);
    }

    public static IEnumerable<(string Name, object Item)> GetObjFields(object obj, string? objName)
    {
        if (objName is not null)
        {
            yield return (objName, obj);
        }

        foreach (var field in obj.GetType().GetFields(_fieldFlags)!)
        {
            if (field.IsPublic && !field.IsInitOnly)
            {
                // if public AND modifiable (NOT readonly)
                Debug.WriteLine(
                    string.Format("Skip field {0}.{1} [{2}]", obj.GetType().Name, field.Name, field.GetValue(obj)),
                    "Translation");
                continue;
            }

            yield return (field.Name, field.GetValue(obj)!);
        }
    }

    public static void AddTranslationItem(string category, object obj, string propName, ITranslation translation)
    {
        var property = obj?.GetType().GetProperty(propName, _propertyFlags);

        if (property?.GetValue(obj, null) is string value && AllowTranslateProperty(value))
        {
            translation.AddTranslationItem(category, propName, "Text", value);
        }
    }

    public static void AddTranslationItemsFromFields(string category, object obj, ITranslation translation)
    {
        if (obj is null)
        {
            return;
        }

        AddTranslationItemsFromList(category, translation, GetObjFields(obj, "$this"));
    }

    private static IEnumerable<PropertyInfo> GetItemPropertiesEnumerator(string name, object? item)
    {
        if (item is null)
        {
            yield break;
        }

        // Skip controls with a name started with "_NO_TRANSLATE_"
        // this is a naming convention, these are not translated
        if (name.StartsWith("_NO_TRANSLATE_"))
        {
            yield break;
        }

        Func<PropertyInfo, bool> isTranslatable;
        if (item is DataGridViewColumn c)
        {
            isTranslatable = property => IsTranslatableItemInDataGridViewColumn(property, c);
        }
        else if (item is ComboBox || item is ListBox)
        {
            isTranslatable = property => IsTranslatableItemInBox(property, item);
        }
        else
        {
            isTranslatable = property => IsTranslatableItemInComponent(property, item);
        }

        foreach (var property in item.GetType().GetProperties(_fieldFlags).Where(isTranslatable))
        {
            yield return property;
        }
    }

    public static void AddTranslationItemsFromList(string category, ITranslation translation,
        IEnumerable<(string Name, object Item)> items)
    {
        foreach ((var itemName, var itemObj) in items)
        {
            if (itemObj is ToolTip tooltip)
            {
                var toolTipTitle = tooltip.ToolTipTitle;

                if (!string.IsNullOrEmpty(toolTipTitle))
                {
                    translation.AddTranslationItem(category, itemName, "ToolTipTitle", toolTipTitle);
                }

                foreach ((var itemNameForTooltip, var itemObjForTooltip) in items)
                {
                    if (itemObjForTooltip is Control control)
                    {
                        var tooltipString = tooltip.GetToolTip(control);
                        if (!string.IsNullOrEmpty(tooltipString))
                        {
                            // Will add an entry in the xlf file with id `NameOfControl.NameOfTooltipControl` ex: "PushToRemote.toolTip1"
                            translation.AddTranslationItem(category, itemNameForTooltip, itemName, tooltipString);
                        }
                    }
                }

                continue;
            }

            foreach (var property in GetItemPropertiesEnumerator(itemName, itemObj))
            {
                var value = property.GetValue(itemObj, null);

                if (value is null)
                {
                    continue;
                }

                if (value is string valueStr)
                {
                    if (AllowTranslateProperty(valueStr))
                    {
                        translation.AddTranslationItem(category, itemName, property.Name, valueStr);
                    }

                    continue;
                }

                if (value is IList listItems)
                {
                    for (var index = 0; index < listItems.Count; index++)
                    {
                        var listItem = listItems[index] as string;

                        if (AllowTranslateProperty(listItem))
                        {
                            translation.AddTranslationItem(category, itemName, "Item" + index, listItem);
                        }
                    }
                }
            }
        }
    }

    public static void TranslateItemsFromList(string category, ITranslation translation,
        IEnumerable<(string Name, object Item)> items)
    {
        foreach ((var itemName, var itemObj) in items)
        {
            // If a user control is being translated through its host control (e.g. FilterToolBar is hosted on FormBrowse,
            // and its translations are recorded under 'FormBrowse' node) then the host's "$this.Text" will be applied to
            // the user control's "Text" property as well.
            // Which is obviously incorrect, so skip any "$this" for types other than the host's type.
            if (itemName == "$this" && itemObj.GetType().Name != category)
            {
                Debug.WriteLine($"TRANSLATION: [{category}]: Skip '$this' for {itemObj.GetType().Name}");
                continue;
            }

            if (itemObj is ToolTip tooltip)
            {
                static string? ProvideDefaultValue()
                {
                    return null;
                }

                var toolTipTitle =
                    translation.TranslateItem(category, itemName, "ToolTipTitle", ProvideDefaultValue);

                if (!string.IsNullOrEmpty(toolTipTitle))
                {
                    tooltip.ToolTipTitle = toolTipTitle;
                }

                foreach ((var itemNameForTooltip, var itemObjForTooltip) in items)
                {
                    if (itemObjForTooltip is Control control)
                    {
                        var tooltipString = translation.TranslateItem(category, itemNameForTooltip, itemName,
                            ProvideDefaultValue);

                        if (!string.IsNullOrEmpty(tooltipString))
                        {
                            tooltip.SetToolTip(control, tooltipString);
                        }
                    }
                }

                continue;
            }

            foreach (var propertyInfo in GetItemPropertiesEnumerator(itemName, itemObj))
            {
                var property = propertyInfo; // copy for lambda

                if (property.Name == "Items" && typeof(IList).IsAssignableFrom(property.PropertyType))
                {
                    var list = (IList)property.GetValue(itemObj, null)!;
                    for (var index = 0; index < list.Count; index++)
                    {
                        if (list[index] is string listValue)
                        {
                            string ProvideDefaultValue()
                            {
                                return listValue;
                            }

                            var value = translation.TranslateItem(category, itemName, "Item" + index,
                                ProvideDefaultValue);

                            if (!string.IsNullOrEmpty(value))
                            {
                                list[index] = value;
                            }
                        }
                    }
                }
                else if (property.PropertyType.IsEquivalentTo(typeof(string)))
                {
                    string ProvideDefaultValue()
                    {
                        return (string)property.GetValue(itemObj, null)!;
                    }

                    var value = translation.TranslateItem(category, itemName, property.Name, ProvideDefaultValue);

                    if (!string.IsNullOrEmpty(value))
                    {
                        if (property.CanWrite)
                        {
                            property.SetValue(itemObj, value, null);
                        }
                    }
                    else if (property.Name == "ToolTipText" &&
                             !string.IsNullOrEmpty((string?)property.GetValue(itemObj, null)))
                    {
                        value = translation.TranslateItem(category, itemName, "Text", ProvideDefaultValue);
                        if (!string.IsNullOrEmpty(value))
                        {
                            if (property.CanWrite)
                            {
                                property.SetValue(itemObj, value, null);
                            }
                        }
                    }
                }
            }
        }
    }

    public static void TranslateProperty(string category, object obj, string propName, ITranslation translation)
    {
        var property = obj?.GetType().GetProperty(propName, _propertyFlags);

        if (property is null)
        {
            return;
        }

        var value = translation.TranslateItem(category, propName, "Text", ProvideDefaultValue);

        if (!string.IsNullOrEmpty(value) && property.CanWrite)
        {
            property.SetValue(obj, value, null);
        }

        static string ProvideDefaultValue()
        {
            return "";
        }
    }

    public static void TranslateItemsFromFields(string category, object obj, ITranslation translation)
    {
        if (obj is null)
        {
            return;
        }

        TranslateItemsFromList(category, translation, GetObjFields(obj, "$this"));
    }

    private static bool IsTranslatableItemInDataGridViewColumn(PropertyInfo propertyInfo, DataGridViewColumn viewCol)
    {
        return propertyInfo.Name.Equals("HeaderText", StringComparison.CurrentCulture) && viewCol.Visible;
    }

    private static bool IsTranslatableItemInBox(PropertyInfo property, object itemObj)
    {
        if (IsTranslatableItemInComponent(property, itemObj))
        {
            return true;
        }

        var localizableProperties = GetLocalizablePropertiesFromAttribute(itemObj) ?? ["Items"];

        return localizableProperties.Contains(property.Name, StringComparer.Ordinal) &&
               property.GetValue(itemObj, null) is IList items &&
               items.Count != 0;
    }

    /// <summary>true if the specified <see cref="Assembly" /> may be translatable.</summary>
    private static bool IsTranslatable(this Assembly assembly)
    {
        var isInvalid =
            _unTranslatableDlLs.Any(asm => assembly.FullName!.StartsWith(asm, StringComparison.OrdinalIgnoreCase));

        return !isInvalid;
    }

    public static Dictionary<string, List<Type>> GetTranslatableTypes()
    {
        Dictionary<string, List<Type>> dictionary = [];
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(p => !p.IsDynamic))
        {
            if (_processedAssemblies.Contains(assembly.FullName!))
            {
                continue;
            }

            _processedAssemblies.Add(assembly.FullName!);

            if (!assembly.IsTranslatable())
            {
                continue;
            }

            var isPlugin = assembly.Location.ToPosixPath().Contains("/Plugins/", StringComparison.OrdinalIgnoreCase);
            var key = isPlugin ? ".Plugins" : "";

            if (!dictionary.TryGetValue(key, out var list))
            {
                list = [];
                dictionary.Add(key, list);
            }

            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type.IsClass && typeof(ITranslate).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    list.Add(type);
                }
            }
        }

        return dictionary;
    }

    public static object? CreateInstanceOfClass(Type type)
    {
        const BindingFlags constructorFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        object? obj = null;

        var constructors = type.GetConstructors(constructorFlags);

        // try to find parameterless constructor first
        foreach (var constructor in constructors)
        {
            if (constructor.GetParameters().Length == 0)
            {
                obj = Activator.CreateInstance(type, true);
            }
        }

        if (obj is null && constructors.Length != 0)
        {
            var parameterConstructor = constructors[0];
            var parameters = parameterConstructor.GetParameters();
            obj = parameterConstructor.Invoke(new object[parameters.Length]);
        }

        return obj;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            Console.WriteLine($"ERROR getting types of {assembly.Location}:");
            Console.WriteLine(e);
            Console.WriteLine("Processing the following types anyway:");
            foreach (var type in e.Types.Where(t => t != null)!)
            {
                Console.WriteLine(type?.FullName);
            }

            return e.Types.Where(t => t != null)!;
        }
    }

    private static string[]? GetLocalizablePropertiesFromAttribute(object item)
    {
        return item.GetType()
            .GetCustomAttribute<LocalizablePropertiesAttribute>()
            ?.TranslatableProperties;
    }

    /// <summary>
    ///     Replaces native path separator with POSIX path separator (/).
    /// </summary>
    [return: NotNullIfNotNull(nameof(path))]
    private static string? ToPosixPath(this string? path)
    {
        return path?.Replace(_nativeDirectorySeparatorChar, _posixDirectorySeparatorChar);
    }
}
