using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiagnosticExplorer
{
	internal static class GenericObjectCache
	{
		private static Dictionary<string, object> _objectCache = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);

		public static Type FindGenericInterface(Type targetType, Type interfaceType)
		{
			IEnumerable<Type> candidates = Enumerable.Repeat(targetType, 1)
				.Concat(targetType.GetInterfaces());

			foreach (Type candidate in candidates)
			{
				if (interfaceType.IsGenericType)
				{
					if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == interfaceType)
						return candidate;
				}
				else
				{
					if (candidate == interfaceType)
						return candidate;
				}
			}
			return null;
		}


		public static T CreateGenericObject<T>(Type genericType, params Type[] typeArguments)
		{
			string key = string.Format("{0} {1}",
																 genericType.FullName,
																 string.Join(", ", typeArguments.Select(x => x.FullName).ToArray()));

			if (!_objectCache.ContainsKey(key))
			{
				Type type = genericType.MakeGenericType(typeArguments);
				_objectCache[key] = Activator.CreateInstance(type);
			}
			return (T)_objectCache[key];
		}
	}
}
