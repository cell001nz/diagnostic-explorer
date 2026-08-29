using System;

namespace DiagnosticExplorer;

internal sealed class CustomPropertyGetter : PropertyGetter
{
    private readonly Func<object, string> _categoryFormatter;
    private readonly Func<object, string> _descriptionFormatter;
    private readonly ConfiguredValue<bool> _initiallyExpanded;

    public CustomPropertyGetter(CustomPropertyConfiguration configuration)
    {
        ConfigureCustomProperty(configuration);
        GetFunc = configuration.Value;
        _categoryFormatter = configuration.CategoryFormatter;
        _descriptionFormatter = configuration.DescriptionFormatter;
        _initiallyExpanded = configuration.InitiallyExpanded;
    }

    internal override bool IsDirectProperty => false;

    public override void GetProperties(object obj, PropertyBag bag, string catPrepend)
    {
        if (!_initiallyExpanded.IsSet || GetFunc(obj) is not IInlineCustomObject inlineCustomObject)
        {
            base.GetProperties(obj, bag, catPrepend);
            return;
        }

        string category = CombineCategories(catPrepend, GetName(obj));
        inlineCustomObject.AddProperties(bag, category);

        Category expandedCategory = bag.FindOrCreateCategory(category);
        expandedCategory.IsExpanded = _initiallyExpanded.Value;
        expandedCategory.IsExpandedProperty = true;
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
