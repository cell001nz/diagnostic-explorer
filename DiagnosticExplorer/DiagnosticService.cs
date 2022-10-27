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
using System.Diagnostics;
using System.Linq;
using DiagnosticExplorer.Util;

namespace DiagnosticExplorer
{
	public class DiagnosticService : IDiagnosticService
	{

	    DiagnosticResponse IDiagnosticService.GetDiagnostics(string context)
		{
			return GetDiagnostics(context);
		}
	
		OperationResponse IDiagnosticService.ExecuteOperation(string path, string operation, string[] arguments)
		{
			return DiagnosticManager.ExecuteOperation(path, operation, arguments);
		}

		OperationResponse IDiagnosticService.SetProperty(string path, string value)
		{
			return DiagnosticManager.SetProperty(path, value);
		}
	
		private static byte[] GetProtoDiagnostics(string context, bool includeEvents)
		{
			Stopwatch watch = Stopwatch.StartNew();
			try
			{
				DiagnosticResponse response = DiagnosticManager.GetDiagnostics(context, includeEvents);
				Property lastRequestProp = new Property("LastRequest", watch.ElapsedMilliseconds + "ms");
				PropertyBag sysEnvironment = response.PropertyBags.FirstOrDefault(x => x.Category == "System" && x.Name == "Environment");
				sysEnvironment?.AddProperty(lastRequestProp, null);

				return ProtobufUtil.Compress(response, 1024);
			}
			catch (Exception ex)
			{
				DiagnosticResponse response = new DiagnosticResponse
				       {
				       	ExceptionMessage = ex.Message,
				       	ExceptionDetail = ex.ToString()
				       };
				return ProtobufUtil.Compress(response, 1024);
			}
		}

		public static DiagnosticResponse GetDiagnostics(string context)
		{
			return new DiagnosticResponse {ProtoResponse = GetProtoDiagnostics(context, true)};
		}

	}
}