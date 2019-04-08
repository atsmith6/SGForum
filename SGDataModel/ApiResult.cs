using Newtonsoft.Json;

namespace SGDataModel
{

	public static class StdResult
	{
		public const string OK = "OK";
		public const string Failed = "Failed";
	}
    public class ApiResult
    {
		public ApiResult()
		{

		}

		public ApiResult(string result)
		{
			Result = result;
		}

        public string Result { get; set; }

		public virtual string ToJson()
		{
			return JsonConvert.SerializeObject(this);
		}

		public static ApiResult FromJson(string json)
		{
			var ret = JsonConvert.DeserializeObject<ApiResult>(json);
			if (ret.Result == StdResult.Failed)
			{
				return JsonConvert.DeserializeObject<ApiError>(json);
			}
			return ret;
		}
    }

	public class ApiResult<T> : ApiResult
    {
		public ApiResult()
		{

		}

		public ApiResult(string result)
		{
			Result = result;
		}
		public ApiResult(string result, T value)
		{
			Result = result;
			Value = value;
		}
        public T Value { get; set; }

		public override string ToJson()
		{
			return JsonConvert.SerializeObject(this);
		}

		public static new ApiResult FromJson(string json)
		{
			var ret = JsonConvert.DeserializeObject<ApiResult>(json);
			if (ret.Result == StdResult.Failed)
			{
				return JsonConvert.DeserializeObject<ApiError>(json);
			}
			return JsonConvert.DeserializeObject<ApiResult<T>>(json);
		}
    }

	public class ApiError : ApiResult<string>
	{
		public int HttpStatusCode { get; set; }

		public ApiError(string error) : base(StdResult.Failed, error)
		{
			HttpStatusCode = 400;
		}

		public ApiError(string error, int statusCode) : base(StdResult.Failed, error)
		{
			HttpStatusCode = 500;
		}

		public static new ApiError FromJson(string json)
		{
			return JsonConvert.DeserializeObject<ApiError>(json);
		}
	} 
}