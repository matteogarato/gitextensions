using System.Globalization;
using System.Reflection;

namespace GitUITests;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class CultureAttribute(string cultureName) : Xunit.v3.BeforeAfterTestAttribute
{
    private CultureInfo? _originalCulture;
    private CultureInfo? _originalUiCulture;

    public override void Before(MethodInfo methodUnderTest, Xunit.v3.IXunitTest test)
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUiCulture = CultureInfo.CurrentUICulture;

        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public override void After(MethodInfo methodUnderTest, Xunit.v3.IXunitTest test)
    {
        if (_originalCulture is not null)
        {
            CultureInfo.CurrentCulture = _originalCulture;
        }

        if (_originalUiCulture is not null)
        {
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
