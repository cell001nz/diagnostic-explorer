#region Copyright

// Diagnostic Explorer, a .Net diagnostic toolset
// Copyright (C) 2010 Cameron Elliot
//
// This file is part of Diagnostic Explorer.
//
// Diagnostic Explorer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Diagnostic Explorer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with Diagnostic Explorer.  If not, see <http://www.gnu.org/licenses/>.
//
// http://diagexplorer.sourceforge.net/

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DiagnosticExplorer;

namespace DiagnosticExplorer;

internal class ExtendedPropertyGetter : PropertyGetter
{
    public ExtendedPropertyGetter(PropertyInfo info, ExtendedPropertyAttribute attr, bool isStatic)
        : this(info, attr, attr, null, isStatic) { }

    internal ExtendedPropertyGetter(
        PropertyInfo info,
        ExtendedPropertyAttribute attr,
        DiagnosticPropertyAttribute metadata,
        PropertyConfiguration configuration,
        bool isStatic,
        bool applyAttributes = true,
        string defaultFormat = null
    )
        : base(info, metadata, configuration, isStatic, applyAttributes, defaultFormat) { }

    public override void GetProperties(object obj, PropertyBag bag, string catPrepend)
    {
        string newPrepend = CombineCategories(catPrepend, GetName(obj));

        object val = GetFunc(obj);
        if (val == null)
        {
            Property p = new Property
            {
                Name = "null",
                CanSet = CanSet,
                SourceObject = obj,
                SourceProperty = PropInfo,
            };

            string prependToCategory = PrependToCategory(newPrepend, obj);
            bag.AddProperty(p, prependToCategory);
        }
        else
        {
            List<PropertyGetter> getters = DiagnosticManager.GetPropertyGetters(val);
            foreach (PropertyGetter getter in getters)
            {
                getter.GetProperties(val, bag, newPrepend);
            }
            Category cat = bag.Categories.FindByName(newPrepend);
            if (cat != null)
                cat.ValueObject = val;
        }
    }
}
