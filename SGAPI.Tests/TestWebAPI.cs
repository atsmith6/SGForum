using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using SGDataModel;
using SGAPI;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Tests
{
	[TestFixture]
    public class TestWebAPI
    {
		private IConfiguration _config;

		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			IConfiguration config = getTestConfiguration();
			using(var context = new SGContext(config))
			{
				context.Database.EnsureDeleted();
			}			
			using(var context = SGContext.CreateAndInitialise(config))
			{
			}
		}

		[OneTimeTearDown]
		public void OneTimeTearDown()
		{
			IConfiguration config = getTestConfiguration();
			using(var context = SGContext.CreateAndInitialise(config))
			{
				context.Database.EnsureDeleted();
			}
		}

        [Test]
        public void TestGetDBInfo()
        {
            var config = getTestConfiguration();
			using(SGContext context = new SGContext(config))
			{
				var handler = new ApiHandler(context);
				var call = new ApiCall("getDbInfo");
				var retTask = handler.processCallAsync(call);
				retTask.Wait();
				var ret = retTask.Result;
				Assert.IsNotNull(ret, "Failed to get a response from the ApiHandler.");
				Assert.IsFalse(ret is ApiError, "The getInfo API call should always work.");
				Assert.IsTrue(ret is ApiResult<DatabaseInfo>, "ApiResult type is wrong");
			}
        }

		[Test]
		public void TestLogin()
		{
			var config = getTestConfiguration();
			using(SGContext context = new SGContext(config))
			{
				var handler = new ApiHandler(context);

				PerformLoginAndTokenTestFor(handler, Defaults.UserAdmin, Defaults.UserAdminPassword);
			}
		}

		[Test]
		public void TestCreateAndActivateUsers()
		{
			using(SGContext context = new SGContext(getTestConfiguration()))
			{
				var handler = new ApiHandler(context);
				var adminToken = quickAdminLogin(handler);
				Assert.IsNotNull(adminToken, "Failed to login.  Test can't run.");

				// createUser(tokenId, email, displayName, password, role) -> User
				var pwd = quickRandomPassword();
				var email = "TestCreateUsers@localhost";
				var displayName = "TestCreateUsers@localhost DN";

				var call = new ApiCall("createUser");
				call.Parameters.Add("tokenId", adminToken.Id.ToString());
				call.Parameters.Add("email", email);
				call.Parameters.Add("password", pwd);
				call.Parameters.Add("displayName", displayName);
				call.Parameters.Add("role", UserRole.User.ToString());

				var result = handler.processCall(call);
				Assert.IsNotNull(result, "API call createUser returned null");
				Assert.IsFalse(result is ApiError, "API call createUser returned an error");
				var userRec = (result as ApiResult<User>).Value;
				Assert.IsTrue(userRec.PasswordHash == "", "Passwords must not be sent back!");
				Assert.IsTrue(userRec.PasswordSalt == "", "Passwords must not be sent back!");
				Assert.IsTrue(userRec.Id != adminToken.User.Id, "UserId sanity check failed.");
				Assert.AreEqual(userRec.Email, email.ToLower(), "The email address of the new user is rubbish");
				Assert.AreEqual(userRec.DisplayName, displayName, "The displayname of the new user is rubbish");
				Assert.AreEqual(userRec.RawRole, UserRole.User, "The user role is incorrect.");

				PerformLoginAndTokenTestFor(handler, email, pwd);

				// LOGIN TO UPDATE
				var userToken = quickLogin(handler, email, pwd);
				Assert.IsNotNull(userToken, "Failed to log in as newly created user.");

				var email2 = "TestCreateUsers_2@localhost";
				var displayName2 = "TestCreateUsers_2@localhost DN";
				userRec.Email = email2;
				userRec.DisplayName = displayName2;
				call = new ApiCall("updateUser");
				call.Parameters.Add("tokenId", userToken.Id.ToString());
				call.Parameters.Add("User", JsonConvert.SerializeObject(userRec));
				result = handler.processCall(call);
				Assert.IsNotNull(result, "API call updateUser returned null");
				Assert.IsFalse(result is ApiError, "API call updateUser returned an error");

				var userB = quickGetUser(handler, userToken, userToken.User.Id);

				Assert.IsNotNull(userB, "getUser appears not to have returned an ApiResult<User>");
				Assert.AreEqual(userB.Id, userRec.Id, "It seems the wrong user was returned");
				Assert.AreEqual(userB.Email, email2, "User update didn't stick (email)");
				Assert.AreEqual(userB.DisplayName, displayName2, "User update didn't stick (displayName)");
				Assert.IsTrue(userB.PasswordHash == "", "Passwords must not be sent back!");
				Assert.IsTrue(userB.PasswordSalt == "", "Passwords must not be sent back!");

				quickSetUserActive(handler, adminToken, email2, false);

				var userTokenB = quickGetToken(handler, userToken.Id);
				Assert.IsNull(userTokenB, "Deactivated user tokens should not be returned from getToken");


			}
		}

		// ====================== PRIVATE ======================

		private User quickGetUser(ApiHandler handler, LoginToken token, int userId)
		{
			var call = new ApiCall("getUser");
			call.Parameters.Add("tokenId", token.Id.ToString());
			call.Parameters.Add("userId", userId.ToString());
			var result = handler.processCall(call);
			if (result == null || (result is ApiError))
				return null;
			return (result as ApiResult<User>).Value;
		}

		private bool quickSetUserActive(ApiHandler handler, LoginToken adminToken, string email, bool active)
		{
			var call = new ApiCall(active ? "activateUser" : "deactivateUser");
			// tokenId, email
			call.Parameters.Add("tokenId", adminToken.Id.ToString());
			call.Parameters.Add("email", email);
			var result = handler.processCall(call);
			if (result == null || (result is ApiError))
				return false;
			return true;
		}

		private LoginToken quickLogin(ApiHandler handler, string email, string password)
		{
			var call = new ApiCall("login");
			call.Parameters.Add("email", email);
			call.Parameters.Add("password", password);
			var result = (handler.processCall(call) as ApiResult<LoginToken>);
			var token = result != null ? result.Value : null;
			return token;
		}

		private LoginToken quickGetToken(ApiHandler handler, Int64 tokenId)
		{
			var call = new ApiCall("getToken");
			call.Parameters.Add("tokenId", tokenId.ToString());
			var result = (handler.processCall(call) as ApiResult<LoginToken>);
			var token = result != null ? result.Value : null;
			return token;
		}

		private void PerformLoginAndTokenTestFor(ApiHandler handler, string email, string password)
		{
			// LOGIN

			var call = new ApiCall("login");
			call.Parameters.Add("email", email);
			call.Parameters.Add("password", password);
			var result = handler.processCall(call);
			Assert.IsNotNull(result, "Login for a known good user failed (returned null).");
			Assert.IsFalse(result is ApiError, "API returned an error trying to login as admin");
			var tokenRet = result as ApiResult<LoginToken>;
			Assert.IsNotNull(tokenRet, "Return type was not ApiResult<LoginToken> as expected");
			var token = tokenRet.Value;
			Assert.AreEqual(token.User.Email, email.ToLower(), "Token came back for the wrong user!");
			Assert.IsTrue(token.User.PasswordHash == "", "Passwords must not be sent back!");
			Assert.IsTrue(token.User.PasswordSalt == "", "Passwords must not be sent back!");

			Assert.IsTrue(token.Id != 0, "Token ID sanity check failed.");
			Assert.IsTrue(token.Expires > DateTime.UtcNow, "Token should not have expired already immediately after logon");

			// GET TOKEN

			call = new ApiCall("getToken");
			call.Parameters.Add("tokenId", token.Id.ToString());
			result = handler.processCall(call);
			Assert.IsNotNull(result, "getToken failed to return a response");
			Assert.IsFalse(result is ApiError, "getToken returned a failed response for a valid token");
			Assert.IsTrue(result is ApiResult<LoginToken>, "Wrong result type returned from the API");
			Assert.IsTrue(result.Result == StdResult.OK, "logout didn't return OK");
			var token2 = (result as ApiResult<LoginToken>).Value;
			Assert.IsNotNull(token2, "getToken returned a null token on a success response.");
			Assert.AreEqual(token.Id, token2.Id, "wrong token returned by get token");
			Assert.IsTrue(token2.User.PasswordHash == "", "Passwords must not be sent back!");
			Assert.IsTrue(token2.User.PasswordSalt == "", "Passwords must not be sent back!");

			// LOGOUT

			call = new ApiCall("logout");
			call.Parameters.Add("tokenId", $"{token.Id}");

			result = handler.processCall(call);
			Assert.IsNotNull(result, "logout failed to return a response");
			Assert.IsFalse(result is ApiError, "logout returned a failed response");
			Assert.IsTrue(result.Result == StdResult.OK, "logout didn't return OK");

			// GET TOKEN

			call = new ApiCall("getToken");
			call.Parameters.Add("tokenId", token.Id.ToString());
			result = handler.processCall(call);
			Assert.IsNotNull(result, "getToken failed to return a response");
			Assert.IsTrue(result is ApiError, "getToken should return an ApiError if the token is invalid.");
		}

		private string quickRandomPassword()
		{
			string ret = string.Empty;
			Random rnd = new Random();
			for(int i = 0; i < 20; ++i)
			{
				int section = rnd.Next(3);
				char ch;
				if (section == 0)
				{
					ch = 'a';
					int inc = rnd.Next(26);
					ch = (Char)(Convert.ToUInt16(ch) + inc);
				}
				else if (section == 1)
				{
					ch = 'A';
					int inc = rnd.Next(26);
					ch = (Char)(Convert.ToUInt16(ch) + inc);
				}
				else// if (section == 2)
				{
					ch = '0';
					int inc = rnd.Next(10);
					ch = (Char)(Convert.ToUInt16(ch) + inc);
				}
				ret += ch;
			}
			return ret;
		}
		private LoginToken quickAdminLogin(ApiHandler handler)
		{
			var call = new ApiCall("login");
			call.Parameters.Add("email", Defaults.UserAdmin);
			call.Parameters.Add("password", Defaults.UserAdminPassword);
			var result = (handler.processCall(call) as ApiResult<LoginToken>);
			var token = result != null ? result.Value : null;
			return token;
		}

		private IConfiguration getTestConfiguration()
		{
			if (_config == null)
			{
				var dict = new Dictionary<string, string>
				{
					{"connectionString", "server=localhost;database=SGForumTest;user=root;password=pwd;TreatTinyAsBoolean=false"},
					{"createTestData", "yes"}
				};

				var config = new ConfigurationBuilder()
					.AddInMemoryCollection(dict)
					.Build();
				_config = config;
			}
			return _config;
		}
    }
}