using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SGDataModel
{
    public class ApiCall
    {
		private string _func = null;
        public string Func 
		{ 
			get
			{
				return _func;
			}
			set
			{
				_func = value != null ? value.Trim() : null;
			} 
		}
		public Int64 LoginTokenId { get; set; } = 0;
		public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

		public ApiCall()
		{
			
		}
		public ApiCall(string func)
		{
			Func = func;
		}

		public string ToJson()
		{
			return JsonConvert.SerializeObject(this);
		}

		public static ApiCall FromJson(string json)
		{
			var call = JsonConvert.DeserializeObject<ApiCall>(json);
			return call;
		}

    }
}