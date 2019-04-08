using System;
using Microsoft.AspNetCore.Mvc;

namespace SGAPI.Utils
{
    public static class Result
    {
		private class Error
		{
			public string type;
			public string category;
			public string message;
		}

        public static object Fail(string message)
		{
			var error = new Error() { type = "System Message", category = "Error", message = message };
			return error;
		}
    }
}