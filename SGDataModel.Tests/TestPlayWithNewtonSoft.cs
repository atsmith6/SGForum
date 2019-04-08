using System;
using NUnit.Framework;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Data.SqlClient;
using SGDataModel;
using MySql.Data.MySqlClient;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SGDataModel.Tests
{
	[TestFixture]
    public class TestPlayWithNewtonSoft
    {
        
		//[Test]
		public void Play1()
		{
			var apiCall = new ApiCall();
			apiCall.Func = "Hello";

			System.Console.WriteLine(apiCall.ToJson());

			apiCall.Parameters.Add("Name", "The name");

			System.Console.WriteLine(apiCall.ToJson());
		}

		//[Test]
		public void Play2()
		{
			var apiResult = new ApiResult();
			apiResult.Result = "OK";
			System.Console.WriteLine(apiResult.ToJson());

			var r2 = new ApiResult<int>();
			r2.Result = "Fail";
			r2.Value = 10;
			System.Console.WriteLine(r2.ToJson());

			var c = new ApiCall();
			c.Func = "MyFunc";
			c.Parameters.Add("p1", "v1");

			var r3 = new ApiResult<ApiCall>();
			r3.Result = "FailX";
			r3.Value = c;
			System.Console.WriteLine(r3.ToJson());

			var r4 = ApiResult<ApiCall>.FromJson(r3.ToJson());
			Assert.IsTrue(r4.Result == "FailX", "");
		}

		[Test]
		public void Play3()
		{
			var error = new ApiError("bad things happened");
			var s = error.ToJson();

			var obj2 = ApiResult.FromJson(s);
			Assert.IsTrue(obj2 is ApiError, "Errors should be automatically down cast");
			Assert.AreEqual((obj2 as ApiError).Value, "bad things happened");
		}
    }
}