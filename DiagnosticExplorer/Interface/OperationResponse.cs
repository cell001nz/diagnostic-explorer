using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace DiagnosticExplorer
{
    [ServiceContract(Namespace = "http://diagnosticexplorer.com/2010")]
	public class OperationResponse
	{
		public static OperationResponse Success()
		{
			return new() { IsSuccess = true};
		}

		public static OperationResponse Success(string result)
		{
			return new() { IsSuccess = true, Result = result};
		}

		public static OperationResponse Error(string message)
		{
			return new() { IsSuccess = false, ErrorMessage = message};
		}

		public static OperationResponse Error(string message, string detail)
		{
			return new() {
			       		IsSuccess = false,
			       		ErrorMessage = message,
			       		ErrorDetail = detail
			       	};
		}

        [DataMember]
		public bool IsSuccess { get; set; }

        [DataMember]
		public string Result { get; set; }

        [DataMember]
		public string ErrorMessage { get; set; }

        [DataMember]
		public string ErrorDetail { get; set; }

	}
}
