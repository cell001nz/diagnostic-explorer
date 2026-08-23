using System;

namespace DiagnosticExplorer;

internal sealed class CustomPropertyGetter : PropertyGetter
{
    private readonly Func<object, string> _categoryFormatter;
    private readonly Func<object, string> _descriptionFormatter;

    public CustomPropertyGetter(CustomPropertyConfiguration configuration)
    {
        ConfigureCustomProperty(configuration);
        GetFunc = configuration.Value;
        _categoryFormatter = configuration.CategoryFormatter;
        _descriptionFormatter = configuration.DescriptionFormatter;
    }

    protected override string GetCategory(object obj)
    {
        if (_categoryFormatter == null)
            return base.GetCategory(obj);

        try
        {
            return _categoryFormatter(obj);
        }
        catch (Exception ex)
        {
            return $"<{ex.Message}>";
        }
    }

    protected override string GetDescription(object obj)
    {
        if (_descriptionFormatter == null)
            return base.GetDescription(obj);

        try
        {
            return _descriptionFormatter(obj);
        }
        catch (Exception ex)
        {
            return $"<{ex.Message}>";
        }
    }
}
