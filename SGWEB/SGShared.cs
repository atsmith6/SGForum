using System;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using SGDataModel;

namespace SGWEB
{

	// TODO: This class is obsolete
    public class SGShared
    {
		public static IConfiguration config { get; set; }
		public static IHttpClientFactory clientFactory { get; set; }
        
		private static DatabaseInfo _databaseInfo;
		public static DatabaseInfo DatabaseInfo
		{
			get 
			{
				if (_databaseInfo == null )
				{
					var retTask = SGShared.makeApiCallAsync<DatabaseInfo>(new ApiCall("getDbInfo"));
					retTask.Wait();
					var ret = retTask.Result;
					if (ret != null && !(ret is ApiError))
					{
						_databaseInfo = (ret as ApiResult<DatabaseInfo>).Value;
					}
				}
				return _databaseInfo; 
			}
		}
		

		public static string GetVersionString()
		{
			var dbi = SGShared.DatabaseInfo;
			if (dbi != null)
			{
				return $"{dbi.MajVersion}.{dbi.MinVersion}";
			}
			return "Database Version Info Unavailable";
		}

		// private async string populateVersionString()
		// {
		// 	try
		// 	{
		// 		var ret = await SGShared.makeApiCallAsync<DatabaseInfo>(new ApiCall("getDbInfo"));

		// 		var error = ret as ApiError;
		// 		if (error == null)
		// 		{
		// 			var dbi = (ret as ApiResult<DatabaseInfo>).Value;
		// 			return $"API {dbi.MajVersion}.{dbi.MinVersion}";
		// 		}
		// 	}
		// 	catch(Exception ex)
		// 	{
		// 	}
		// 	return "Version information unavailable.";
		// }

		public static async Task<ApiResult> makeApiCallAsync<T>(ApiCall call)
		{
			var client = clientFactory.CreateClient();
			var apiAddress = config["APIAddress"];
			var response = await client.PostAsync(apiAddress,
				new StringContent(call.ToJson(), System.Text.Encoding.UTF8));

			if (response.IsSuccessStatusCode)
			{
				string jsonString = await response.Content
					.ReadAsStringAsync();
				ApiResult tmp = ApiResult.FromJson(jsonString);
				if (tmp is ApiError)
					return tmp;
				return ApiResult<T>.FromJson(jsonString);
			}
			else
			{
				throw new Exception("Unexpected non-200 result from Client.");
			}
		}
    }
}