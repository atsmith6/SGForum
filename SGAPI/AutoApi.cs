using System;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using SGDataModel;

namespace SGAPI
{

	[System.AttributeUsage(System.AttributeTargets.Method)]  
	public class SGAutoApi : System.Attribute  
	{  
		public SGAutoApi()  
		{  
		}  
	}  

	public class AutoApiError : Exception
	{
		public AutoApiError(string message) : base(message)
		{

		}

		//public static ApiError ApiFunctionNotFound(string funcName) { return new ApiError($"Api Function {funcName} Not Found", 400); }
		public static AutoApiError AuthenticationFailure() { return new AutoApiError("Authentication failure"); }
		public static AutoApiError InvalidToken() { return new AutoApiError("Invalid login token"); }
		public static AutoApiError InvalidParam(string param) { return new AutoApiError($"Invalid parameter: {param}"); }
		public static AutoApiError InvalidRole(string desc) { return new AutoApiError($"Invalid role: {desc}"); }
		public static AutoApiError NotFound() { return new AutoApiError("Not found"); }
		public static AutoApiError ServerError(string message) { return new AutoApiError($"Server exception: {message}"); }
		public static AutoApiError Unauthorised() { return new AutoApiError("Unauthorised"); }
	}

	public class AutoApiException : Exception
	{
		public int HttpStatusCode;
		public AutoApiException(string message, int statusCode = 500) : base(message)
		{
			HttpStatusCode = statusCode;
		}

		public ApiError ToApiResult()
		{
			return new ApiError(this.Message, this.HttpStatusCode);
		}

		public static AutoApiException ValueTypeParamNull(string param)
		{
			return new AutoApiException($"Value type parameter {param} cannot be null or missing.", 400);
		}

		public static AutoApiException ParameterConvertException<T>(string param)
		{
			return new AutoApiException($"Unable to convert parameter {param} to type {typeof(T).Name}.", 400);
		}

		public static AutoApiException UnsupportedParameterType(string name, Type type)
		{
			return new AutoApiException($"Unsupported parameter type {type.Name} for parameter {name}.", 400);
		}
	}

    public class AutoApi
    {
		private Dictionary<string, MethodInfo> _api = new Dictionary<string, MethodInfo>();
		private MethodInfo _deserialiseMethod;
        public AutoApi()
		{
			var deserialiseMethod = typeof(JsonConvert).GetMethods().Where(m => m.Name == "DeserializeObject" && m.IsGenericMethod == true && m.GetParameters().Length == 1).FirstOrDefault();
			if (deserialiseMethod == null)
				throw new Exception("Failed to retrieve the JSON deserialisation method needed.");
			_deserialiseMethod = deserialiseMethod;

			ScanForMethods();
		}

		public async Task<ApiResult> AutoProcessAsync(ApiCall apiCall)
		{
			try
			{
				MethodInfo info;
				if (!_api.TryGetValue(apiCall.Func, out info))
					return ApiError.ApiFunctionNotFound(apiCall.Func);
				
				Task task;
				object[] parameters = MapParameters(info, apiCall);
				try
				{
					task = (Task) info.Invoke(this, parameters);
					await task;
				}
				catch(AutoApiError error)
				{
					return new ApiError(error.Message, 400);
				}

				var taskType = typeof(Task);
				var retType = info.ReturnParameter.ParameterType;
				if (retType == taskType)
				{
					return new ApiResult(StdResult.OK);
				}
				else if (retType.IsSubclassOf(taskType) && retType.GenericTypeArguments.Length == 1)
				{
					// First check whether the function has already returned an ApiResult
					var apiResultType = typeof(ApiResult);
					var genericParamType = retType.GenericTypeArguments[0];
					if (genericParamType == apiResultType || genericParamType.IsSubclassOf(apiResultType))
					{
						var apiResultObj = task.GetType().GetProperty("Result").GetValue(task);
						return (ApiResult) apiResultObj;
					}
					// Ok, so some other object.  Lets wrap it in an ApiResult<T>.
					var genericApiResult = typeof(ApiResult<>);
					Type[] typeArgs = { genericParamType };
					var contructedApiResultType = genericApiResult.MakeGenericType(typeArgs);
					var returnValue = task.GetType().GetProperty("Result").GetValue(task);
					var created = Activator.CreateInstance(contructedApiResultType, new object[] { StdResult.OK, returnValue } );
					return (ApiResult)created;
				}
				else
				{
					return ApiError.BadServerApiImplementation(apiCall.Func);
				}
			}
			catch(Exception ex)
			{
				return ApiError.ServerError(ex.Message);
			}
		}

		private object[] MapParameters(MethodInfo info, ApiCall call)
		{
			if (info.Name == "getTopicPage")
			{
				int i = 10;
				int j = i;
			}

			var p = new List<object>();
			var parameterInfo = info.GetParameters();
			if (parameterInfo != null)
			{
				foreach (var parameter in parameterInfo)
				{
					string strVal;
					if (!call.Parameters.TryGetValue(parameter.Name, out strVal))
					{
						// Parameter not found!
						bool isNullable = Nullable.GetUnderlyingType(parameter.ParameterType) != null;
						if (isNullable)
							p.Add(null);
						else if (parameter.ParameterType.IsValueType)
							throw AutoApiException.ValueTypeParamNull(parameter.Name);
						else
							p.Add(null);
					}
					else
					{
						if (parameter.ParameterType.IsClass)
						{
							if (parameter.ParameterType == typeof(string))
							{
								p.Add(strVal);
							}
							else
							{
								string json = strVal;
								var genericDeserialse = _deserialiseMethod.MakeGenericMethod(parameter.ParameterType);
								var obj = genericDeserialse.Invoke(null, new object[] { json });
								p.Add(obj);
							}
						}
						else
						{
							if (parameter.ParameterType == typeof(int))
							{
								int iVal;
								if (!int.TryParse(strVal, out iVal))
									throw AutoApiException.ParameterConvertException<int>(parameter.Name);
								else
								{
									p.Add(iVal);
								}
							}
							else if (parameter.ParameterType == typeof(Nullable<int>))
							{
								int iVal;
								if (!int.TryParse(strVal, out iVal))
									throw AutoApiException.ParameterConvertException<int>(parameter.Name);
								else
								{
									p.Add(iVal);
								}
							}
							else if (parameter.ParameterType == typeof(Int64))
							{
								Int64 iVal;
								if (!Int64.TryParse(strVal, out iVal))
									throw AutoApiException.ParameterConvertException<Int64>(parameter.Name);
								p.Add(iVal);
							}
							else if (parameter.ParameterType == typeof(float))
							{
								float fVal;
								if (!float.TryParse(strVal, out fVal))
									throw AutoApiException.ParameterConvertException<float>(parameter.Name);
							}
							else if (parameter.ParameterType == typeof(double))
							{
								double fVal;
								if (!double.TryParse(strVal, out fVal))
									throw AutoApiException.ParameterConvertException<double>(parameter.Name);
							} 
							else
							{
								throw AutoApiException.UnsupportedParameterType(parameter.Name, parameter.ParameterType);	
							}
						}
					}
				}
			}
			return p.ToArray();
		}

		private void ScanForMethods()
		{
			var type = this.GetType();
			var methods = type.GetMethods();
			
			foreach(var method in methods)
			{
				if (method.IsStatic)
					continue;
				var attributes = method.GetCustomAttributes();
				foreach(var attrObj in attributes)
				{
					if (attrObj is SGAutoApi)
					{
						_api.Add(method.Name, method);
					}
				}
			}
		}
    }
}