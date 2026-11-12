using System.Globalization;
using System.Reflection;
using Xunit.v3;

namespace GitUITests;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class CultureAttribute(string cultureName) : BeforeAfterTestAttribute
{
    private CultureInfo? _originalCulture;
    private CultureInfo? _originalUiCulture;

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUiCulture = CultureInfo.CurrentUICulture;

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
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
