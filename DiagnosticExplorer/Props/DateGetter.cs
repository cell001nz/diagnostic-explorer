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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DiagnosticExplorer
{
	internal class DateGetter : PropertyGetter
	{
		private bool _exposeDate = true;
		private bool _exposeElapsed = false;
		private bool _exposeTimeUntil = false;
		private bool _isUtc = false;

		public DateGetter(PropertyInfo prop, DatePropertyAttribute attr, bool isStatic) : base(prop, isStatic)
		{
			if (attr != null)
			{
				_exposeDate = attr.ExposeDate;
				_exposeElapsed = attr.ExposeElapsed;
				_exposeTimeUntil = attr.ExposeTimeUntil;
                _isUtc = attr.IsUTC;
            }
		}

		public override void GetProperties(object obj, PropertyBag bag, string catPrepend)
		{
			DateTime? dateVal = (DateTime?)GetFunc(obj);

            bool isUtc = dateVal?.Kind == DateTimeKind.Unspecified
                ? _isUtc
                : dateVal?.Kind == DateTimeKind.Utc;

			if (_exposeDate)
			{
				base.GetProperties(obj, bag, catPrepend);
			}

            DateTime now = isUtc ? DateTime.UtcNow : DateTime.Now;
			if (_exposeElapsed)
			{
				string val = dateVal == null ? "" : FormatTimeSpan(now.Subtract(dateVal.Value));
				Property property = new Property("Time since " + Name, val);
				bag.AddProperty(property, PrependToCategory(catPrepend));
			}
			if (_exposeTimeUntil)
			{
				string val = dateVal == null ? "" : FormatTimeSpan(dateVal.Value.Subtract(now));
				Property property = new Property("Time until " + Name, val);
				bag.AddProperty(property, PrependToCategory(catPrepend));
			}
		}
	}
}