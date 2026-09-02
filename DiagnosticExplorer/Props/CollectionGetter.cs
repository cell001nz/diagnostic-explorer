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
    private Func<object, int, string> _indexedNameFormatter;
    private Func<object, object> _valueFunc;
    private Func<object, string> _valueFormatter;
    private Func<object, object> _descrFunc;
    private Func<object, string> _descriptionFormatter;
    private Func<object, object> _catFunc;
    private Func<object, string> _categoryFormatter;
    private IReadOnlyList<PropertyStatusConfiguration> _itemStatuses;
    private StatusIconSize _itemStatusIconSize;
    private bool _itemIsJson;
    private int _itemWidth;
    private bool _initiallyExpanded;
    private NestedPropertyRenderMode _itemRenderMode;

    public CollectionGetter(PropertyInfo info, CollectionPropertyAttribute attr, bool isStatic)
        : this(info, attr.CreateOptions(), attr, null, isStatic) { }

    internal override bool IsDirectProperty => _mode != CollectionMode.ExpandedItems;

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
        _separator = options.Separator ?? ", ";
        _mode = options.Mode;
        _nameFormatter = options.NameFormatter;
        _indexedNameFormatter = options.IndexedNameFormatter;
        _valueFormatter = options.ValueFormatter;
        _descriptionFormatter = options.DescriptionFormatter;
        _categoryFormatter = options.CategoryFormatter;
        _itemStatuses = options.ItemStatuses;
        _itemStatusIconSize = options.ItemStatusIconSize.IsSet ? options.ItemStatusIconSize.Value : StatusIconSize.Small;
        _itemIsJson = options.ItemIsJson;
        _itemWidth = options.ItemWidth;
        _initiallyExpanded = options.InitiallyExpanded;
        _itemRenderMode = options.PrimaryPropertiesOnly ? NestedPropertyRenderMode.PrimaryOnly : NestedPropertyRenderMode.All;

        Type collectionType = info?.PropertyType ?? configuration.ValueType;
        Type genericType =
            GenericObjectCache.FindGenericInterface(collectionType, typeof(IDictionary<,>))
            ?? GenericObjectCache.FindGenericInterface(collectionType, typeof(IReadOnlyDictionary<,>));
        bool isDictionary = collectionType.GetInterfaces().Contains(typeof(IDictionary));

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
            _nameFunc = PropertyToFunction(GetListProperty(collectionType, options.NameProperty), isStatic);
            _valueFunc = PropertyToFunction(GetListProperty(collectionType, options.ValueProperty), isStatic);
            _descrFunc = PropertyToFunction(GetListProperty(collectionType, options.DescriptionProperty), isStatic);
            _catFunc = PropertyToFunction(GetListProperty(collectionType, options.CategoryProperty), isStatic);
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

    private PropertyInfo GetListProperty(Type collectionType, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (!collectionType.IsGenericType)
            return null;
        if (collectionType.GetGenericArguments().Length != 1)
            return null;

        Type colType = collectionType.GetGenericArguments()[0];
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
                bag.AddProperty(CreateOutputProperty(name, null, obj, null), PrependToCategory(catPrepend, obj));
                return;
            }

            int count = col.Cast<object>().Count();

            if (count == 0)
            {
                AddCountProperty(name, count, col, bag, catPrepend, obj);
                return;
            }

            switch (_mode)
            {
                case CollectionMode.Count:
                {
                    AddCountProperty(name, count, col, bag, catPrepend, obj);
                    break;
                }
                case CollectionMode.Concatenate:
                {
                    AppendConcatenated(col, bag, catPrepend, obj);
                    break;
                }
                case CollectionMode.List:
                    AppendAllProperties(col, count, bag, catPrepend, obj);
                    break;
                case CollectionMode.Categories:
                    AppendSeparateCategories(col, count, bag, catPrepend, obj);
                    break;
                case CollectionMode.ExpandedItems:
                    AppendExpandedItems(col, count, bag, catPrepend, obj);
                    break;
            }
        }
        catch (Exception ex) // May get exception if the collection is modified during iteration
        {
            string error = $"<{ex.Message}>";
            bag.AddProperty(CreateOutputProperty(GetName(obj), error, obj, null), PrependToCategory(catPrepend, obj));
        }
    }

    private void AddCountProperty(string name, int count, IEnumerable collection, PropertyBag bag, string catPrepend, object owner)
    {
        Property property = CreateOutputProperty(name, FormatValue(count), owner, collection);
        if (count > 0)
            ApplyDrillDown(property, collection, owner);
        bag.AddProperty(property, PrependToCategory(catPrepend, owner));
    }

    private void AppendSeparateCategories(IEnumerable col, int count, PropertyBag bag, string catPrepend, object owner)
    {
        int index = 0;
        foreach (object listObject in GetLimitedItems(col))
        {
            string valCategory =
                _categoryFormatter?.Invoke(listObject) ?? Convert.ToString(GetNextPropVal(listObject, _catFunc, index++, GetName(owner)));
            string category = GetCategory(owner);
            if (!string.IsNullOrEmpty(category))
            {
                if (category.IndexOf("{") != -1)
                    valCategory = string.Format(category, valCategory);
                else
                    valCategory = $"{category}.{valCategory}";
            }

            string newPrepend = CombineCategories(catPrepend, valCategory);

            foreach (PropertyGetter getter in DiagnosticManager.GetPropertyGetters(listObject))
                getter.GetProperties(listObject, bag, newPrepend);

            Category cat = bag.Categories.FindByName(newPrepend);
            if (cat != null)
            {
                cat.ValueObject = listObject;
                if (DrillDownEnabled && DiagnosticManager.IsDrillDownValue(listObject))
                {
                    cat.CanDrillDown = true;
                    cat.DrillDownObject = listObject;
                    cat.DrillDownMaxItems = DrillDownMaxItems;
                }
            }
        }

        AddTruncationProperty(count, bag, catPrepend, owner);
    }

    private void AppendExpandedItems(IEnumerable col, int count, PropertyBag bag, string catPrepend, object owner)
    {
        bool isInlineCustomProjection = owner is IInlineCustomObject;
        string category = isInlineCustomProjection ? catPrepend : CombineCategories(PrependToCategory(catPrepend, owner), GetName(owner));
        int index = 0;
        foreach (object listObject in GetLimitedItems(col))
        {
            string itemName =
                _categoryFormatter?.Invoke(listObject) ?? Convert.ToString(GetNextPropVal(listObject, _catFunc, index++, GetName(owner)));
            string itemCategory = CombineCategories(category, itemName);

            NestedPropertyRenderer.Render(listObject, bag, itemCategory, _itemRenderMode);

            Category item = bag.Categories.FindByName(itemCategory);
            if (item != null)
            {
                item.ValueObject = listObject;
                item.Statuses = GetItemStatuses(listObject);
                item.StatusIconSize = _itemStatusIconSize;
                if (DrillDownEnabled && DiagnosticManager.IsDrillDownValue(listObject))
                {
                    item.CanDrillDown = true;
                    item.DrillDownObject = listObject;
                    item.DrillDownMaxItems = DrillDownMaxItems;
                }
            }
        }

        if (!isInlineCustomProjection)
        {
            Category expandedCategory = bag.FindOrCreateCategory(category);
            expandedCategory.IsExpanded = _initiallyExpanded;
            expandedCategory.IsExpandedProperty = true;
        }
        AddTruncationProperty(count, bag, category, owner, applyOwnerCategory: false);
    }

    private void AppendAllProperties(IEnumerable col, int count, PropertyBag bag, string catPrepend, object owner)
    {
        int index = 0;
        foreach (object obj in GetLimitedItems(col))
        {
            int itemIndex = index++;
            object objectValue = obj;
            string name =
                _indexedNameFormatter?.Invoke(obj, itemIndex)
                ?? _nameFormatter?.Invoke(obj)
                ?? Convert.ToString(GetNextPropVal(obj, _nameFunc, itemIndex, GetName(owner)));
            string val = _valueFormatter?.Invoke(obj) ?? (_valueFunc == null ? FormatValue(obj) : GetValue(obj, _valueFunc, out objectValue));
            string desc = _descriptionFormatter?.Invoke(obj) ?? (_descrFunc == null ? null : GetValue(obj, _descrFunc, out objectValue));
            string cat = _categoryFormatter?.Invoke(obj) ?? (_catFunc == null ? null : GetValue(obj, _catFunc, out objectValue));

            Property prop = new Property(name, val, desc)
            {
                ValueObject = objectValue,
                Statuses = GetItemStatuses(obj),
                IsJson = _itemIsJson,
                Width = _itemWidth,
            };
            ApplyDrillDown(prop, obj, owner);
            bag.AddProperty(prop, CombineCategories(PrependToCategory(catPrepend, owner), cat));
        }

        AddTruncationProperty(count, bag, catPrepend, owner);
    }

    private void AppendConcatenated(IEnumerable col, PropertyBag bag, string catPrepend, object owner)
    {
        IEnumerable collection = col;
        if (_valueFormatter != null)
            col = col.Cast<object>().Select(_valueFormatter);
        else if (_valueFunc != null)
            col = col.Cast<object>().Select(_valueFunc);

        string val = FormatEnumerable(col, _separator, _maxItems, includeCount: false);
        Property property = CreateOutputProperty(GetName(owner), val, owner, collection);
        ApplyDrillDown(property, collection, owner);
        bag.AddProperty(property, PrependToCategory(catPrepend, owner));
    }

    private Property CreateOutputProperty(string name, string value, object owner, object valueObject)
    {
        return new Property(name, value, GetDescription(owner))
        {
            ValueObject = valueObject,
            Alerts = GetAlerts(owner),
            Statuses = GetStatuses(owner),
            SourceObject = owner,
            SourceProperty = PropInfo,
            NoTruncate = NoTruncate,
        };
    }

    private List<PropertyStatus> GetItemStatuses(object item)
    {
        if (_itemStatuses == null || _itemStatuses.Count == 0)
            return null;

        List<PropertyStatus> activeStatuses = new();
        foreach (PropertyStatusConfiguration status in _itemStatuses)
        {
            try
            {
                if (status.Condition(item))
                    activeStatuses.Add(new PropertyStatus(status.Status, status.Text(item)));
            }
            catch (Exception ex)
            {
                activeStatuses.Add(new PropertyStatus(StatusCode.Error, $"<{ex.Message}>"));
                break;
            }
        }

        return activeStatuses.Count == 0 ? null : activeStatuses;
    }

    private object GetNextPropVal(object obj, Func<object, object> propFunc, int index, string name)
    {
        if (propFunc == null)
            return $"{name} {index}";

        return propFunc(obj);
    }

    private void AddTruncationProperty(int count, PropertyBag bag, string catPrepend, object owner, bool applyOwnerCategory = true)
    {
        int remaining = count - GetMaxItems();
        if (remaining <= 0)
            return;

        string name = GetName(owner) + " (more)";
        string value = string.Format("{0} more item{1}", remaining, remaining == 1 ? "" : "s");
        string category = applyOwnerCategory ? PrependToCategory(catPrepend, owner) : catPrepend;
        bag.AddProperty(CreateOutputProperty(name, value, owner, null), category);
    }

    private IEnumerable<object> GetLimitedItems(IEnumerable collection)
    {
        return collection.Cast<object>().Take(GetMaxItems());
    }

    private int GetMaxItems() => _maxItems <= 0 ? MaxConcatItems : _maxItems;
}
