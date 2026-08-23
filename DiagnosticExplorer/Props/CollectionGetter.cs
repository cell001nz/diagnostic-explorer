using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace DiagnosticExplorer;

internal class CollectionGetter : PropertyGetter
{
    private string _separator;
    private CollectionMode _mode;
    private int _maxItems;
    private Func<object, object> _nameFunc;
    private Func<object, string> _nameFormatter;
    private Func<object, object> _valueFunc;
    private Func<object, string> _valueFormatter;
    private Func<object, object> _descrFunc;
    private Func<object, string> _descriptionFormatter;
    private Func<object, object> _catFunc;
    private Func<object, string> _categoryFormatter;

    public CollectionGetter(PropertyInfo info, CollectionPropertyAttribute attr, bool isStatic)
        : this(info, attr.CreateOptions(), attr, null, isStatic) { }

    internal CollectionGetter(
        PropertyInfo info,
        CollectionOptions options,
        DiagnosticPropertyAttribute metadata,
        PropertyConfiguration configuration,
        bool isStatic,
        bool applyAttributes = true,
        string defaultFormat = null
    )
        : base(info, metadata, configuration, isStatic, applyAttributes, defaultFormat)
    {
        _separator = options.Separator ?? Environment.NewLine;
        _mode = options.Mode;
        _nameFormatter = options.NameFormatter;
        _valueFormatter = options.ValueFormatter;
        _descriptionFormatter = options.DescriptionFormatter;
        _categoryFormatter = options.CategoryFormatter;

        Type genericType = GenericObjectCache.FindGenericInterface(info.PropertyType, typeof(IDictionary<,>));
        bool isDictionary = info.PropertyType.GetInterfaces().Contains(typeof(IDictionary));

        if (genericType != null)
        {
            DictPropGetter propGetter = GenericObjectCache.CreateGenericObject<DictPropGetter>(
                typeof(DictPropGetter<,>),
                genericType.GetGenericArguments()
            );
            _nameFunc = propGetter.GetNameGetter();
            _valueFunc = propGetter.GetValueGetter();
        }
        else if (isDictionary)
        {
            _nameFunc = x => ((DictionaryEntry)x).Key;
            _valueFunc = x => ((DictionaryEntry)x).Value;
        }
        else
        {
            _nameFunc = PropertyToFunction(GetListProperty(info, options.NameProperty), isStatic);
            _valueFunc = PropertyToFunction(GetListProperty(info, options.ValueProperty), isStatic);
            _descrFunc = PropertyToFunction(GetListProperty(info, options.DescriptionProperty), isStatic);
            _catFunc = PropertyToFunction(GetListProperty(info, options.CategoryProperty), isStatic);
        }
        _maxItems = options.MaxItems;
    }

    public abstract class DictPropGetter
    {
        public abstract Func<object, object> GetNameGetter();
        public abstract Func<object, object> GetValueGetter();
    }

    public class DictPropGetter<TKey, TValue> : DictPropGetter
    {
        public override Func<object, object> GetNameGetter()
        {
            return value => ((KeyValuePair<TKey, TValue>)value).Key;
        }

        public override Func<object, object> GetValueGetter()
        {
            return value => ((KeyValuePair<TKey, TValue>)value).Value;
        }
    }

    private PropertyInfo GetListProperty(PropertyInfo info, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (!info.PropertyType.IsGenericType)
            return null;
        if (info.PropertyType.GetGenericArguments().Length != 1)
            return null;

        Type colType = info.PropertyType.GetGenericArguments()[0];
        PropertyInfo propInfo = colType.GetProperty(name, DiagnosticManager.PublicInstancePropertyFlags);

        if (propInfo == null)
            Debug.WriteLine($"Diagnostics: Can't find property '{name}' on class '{colType}'");

        return propInfo;
    }

    public override void GetProperties(object obj, PropertyBag bag, string catPrepend)
    {
        try
        {
            IEnumerable col = GetFunc(obj) as IEnumerable;
            string name = GetName(obj);

            if (col == null)
            {
                bag.AddProperty(new Property(name, null), PrependToCategory(catPrepend, obj));
                return;
            }

            int count = col.Cast<object>().Count();

            if (count == 0)
            {
                bag.AddProperty(new Property(name, FormatValue(count)), PrependToCategory(catPrepend, obj));
                return;
            }

            switch (_mode)
            {
                case CollectionMode.Count:
                {
                    bag.AddProperty(new Property(name, FormatValue(count)), PrependToCategory(catPrepend, obj));
                    break;
                }
                case CollectionMode.Concatenate:
                {
                    AppendConcatenated(col, bag, catPrepend, obj);
                    break;
                }
                case CollectionMode.List:
                    AppendAllProperties(col, bag, catPrepend, obj);
                    break;
                case CollectionMode.Categories:
                    AppendSeparateCategories(col, bag, catPrepend, obj);
                    break;
            }
        }
        catch (Exception ex) // May get exception if the collection is modified during iteration
        {
            string error = $"<{ex.Message}>";
            bag.AddProperty(new Property(GetName(obj), error), PrependToCategory(catPrepend, obj));
        }
    }

    private void AppendSeparateCategories(IEnumerable col, PropertyBag bag, string catPrepend, object owner)
    {
        int index = 0;
        foreach (object listObject in GetLimitedItems(col))
        {
            object catPropVal = GetNextPropVal(listObject, _catFunc, index++, GetName(owner));
            string valCategory = Convert.ToString(catPropVal);
            string category = GetCategory(owner);
            if (!string.IsNullOrEmpty(category))
            {
                if (category.IndexOf("{") != -1)
                    valCategory = string.Format(category, catPropVal);
                else
                    valCategory = $"{category}.{valCategory}";
            }

            string newPrepend = CombineCategories(catPrepend, valCategory);

            foreach (PropertyGetter getter in DiagnosticManager.GetPropertyGetters(listObject))
                getter.GetProperties(listObject, bag, newPrepend);

            Category cat = bag.Categories.FindByName(newPrepend);
            if (cat != null)
                cat.ValueObject = listObject;
        }
    }

    private void AppendAllProperties(IEnumerable col, PropertyBag bag, string catPrepend, object owner)
    {
        int index = 0;
        foreach (object obj in GetLimitedItems(col))
        {
            object objectValue = obj;
            string name = _nameFormatter?.Invoke(obj) ?? Convert.ToString(GetNextPropVal(obj, _nameFunc, index++, GetName(owner)));
            string val = _valueFormatter?.Invoke(obj) ?? (_valueFunc == null ? FormatValue(obj) : GetValue(obj, _valueFunc, out objectValue));
            string desc = _descriptionFormatter?.Invoke(obj) ?? (_descrFunc == null ? null : GetValue(obj, _descrFunc, out objectValue));
            string cat = _categoryFormatter?.Invoke(obj) ?? (_catFunc == null ? null : GetValue(obj, _catFunc, out objectValue));

            Property prop = new Property(name, val, desc);
            prop.ValueObject = objectValue;
            bag.AddProperty(prop, CombineCategories(PrependToCategory(catPrepend, owner), cat));
        }
    }

    private void AppendConcatenated(IEnumerable col, PropertyBag bag, string catPrepend, object owner)
    {
        if (_valueFunc != null)
            col = col.Cast<object>().Select(_valueFunc);

        string val = FormatEnumerable(col, _separator, _maxItems);
        bag.AddProperty(new Property(GetName(owner), val), PrependToCategory(catPrepend, owner));
    }

    private object GetNextPropVal(object obj, Func<object, object> propFunc, int index, string name)
    {
        if (propFunc == null)
            return $"{name} {index}";

        return propFunc(obj);
    }

    private IEnumerable<object> GetLimitedItems(IEnumerable collection)
    {
        int maxItems = _maxItems <= 0 ? MaxConcatItems : _maxItems;
        return collection.Cast<object>().Take(maxItems);
    }
}
