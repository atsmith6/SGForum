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

namespace SGDataModel.Tests
{
	[TestFixture]
    public class TestDatabaseBasics
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

        [SetUp]
		public void Setup()
        {
        }

		private IConfiguration getTestConfiguration()
		{
			if (_config == null)
			{
				var dict = new Dictionary<string, string>
				{
					{"connectionString", "server=localhost;database=SGForumTest;user=root;password=pwd;TreatTinyAsBoolean=false"}
				};

				var config = new ConfigurationBuilder()
					.AddInMemoryCollection(dict)
					.Build();
				_config = config;
			}
			return _config;
		}

        [Test]
        public void TestBasicLogin() 
        {
			IConfiguration config = getTestConfiguration();
			using(var context =  new SGContext(config))
			{
				var tokenTask = LoginTokenTasks.LoginAsync(context, "admin@localhost", "password");
				tokenTask.Wait();
				var token = tokenTask.Result;
				Assert.IsTrue(token != null, "Login token returned null");
				Assert.IsFalse(token.UserId == 0, "I've broken the EF key link somehow");
				Assert.AreEqual(token.UserId, token.User.Id, "I've broken the EF key link somehow");

				var token2Task = LoginTokenTasks.GetLoginTokenAsync(context, token.Id);
				token2Task.Wait();
				var token2 = token2Task.Result;
				Assert.AreEqual(token2.UserId, token2.User.Id, "I'ev broken the EF key link somehow");

				Assert.IsNotNull(token2, "A valid login token has come back as null");
				Assert.AreEqual(token.Id, token2.Id, "The token requested is not the token retrieved.");

				LoginTokenTasks.LogoutAsync(context, token2).Wait();

				var token3Task = LoginTokenTasks.GetLoginTokenAsync(context, token.Id);
				token3Task.Wait();
				var token3 = token3Task.Result;

				Assert.IsNull(token3, "After logout the token should return as null.");
			}

            Assert.Pass();
        }

		private LoginToken quickLogin(SGContext context, string username, string password) 
		{
			var tokenTask = LoginTokenTasks.LoginAsync(context, username, password);
				tokenTask.Wait();
			var token = tokenTask.Result;
			return token;
		}

		private LoginToken quickGetToken(SGContext context, Int64 tokenId)
		{
			var task = LoginTokenTasks.GetLoginTokenAsync(context, tokenId);
				task.Wait();
			var token = task.Result;
			return token;
		}

		[Test]
        public void TestDeactivateUser()
        {
			IConfiguration config = getTestConfiguration();
			using(var context =  new SGContext(config))
			{
				var token = quickLogin(context, "admin@localhost", "password");
				Assert.IsTrue(token != null, "Unable to log in as admin user.  Test can't run");

				var testUser = TryCreateUser(context, token, "deactiveUser@localhost", "password2");
				var userToken = quickLogin(context, "deactiveUser@localhost", "password2");
				Assert.IsTrue(userToken != null, "Unable to log in as activation test user.  Test can't run");

				UserTasks.SetUserActiveAsync(context, token, "deactiveUser@localhost", false).Wait();				

				var task = LoginTokenTasks.GetLoginTokenAsync(context, userToken.Id);
				task.Wait();
				var userToken2 = task.Result;
				Assert.IsNull(userToken2, "A user's tokens should not be available after deactivation.");

				userToken = quickLogin(context, "deactiveUser@localhost", "password2");
				Assert.IsNull(userToken2, "A user's should not be able to login after deactivation.");

				UserTasks.SetUserActiveAsync(context, token, "deactiveUser@localhost", true).Wait();

				task = LoginTokenTasks.GetLoginTokenAsync(context, userToken.Id);
				task.Wait();
				var userToken3 = task.Result;
				Assert.IsNotNull(userToken3, "User failed to login after reactivation.");
			}
		}

		private User TryCreateUser(SGContext context, LoginToken token, string email, string password)
		{
			try
			{
				var userTask = UserTasks.CreateUserAsync(context, token, email, "User", password, UserRole.User);
				userTask.Wait();
				var user = userTask.Result;
				return user;
			}
			catch
			{
				return null;
			}
		}

		private void TryChangeUserDisplayEmailAndName(SGContext context, LoginToken token, User user, string email, string displayName)
		{
			string origEmail = user.Email;
			string origDisplay = user.DisplayName;
			try
			{
				user.Email = email;
				user.DisplayName = displayName;
				var userTask = UserTasks.UpdateUserAsync(context, token, user);
				userTask.Wait();
			}
			catch
			{
				Assert.Fail($"Failed to change user ({origEmail}, {origDisplay}) to ({email}, {displayName})");
			}
		}

		[Test]
        public void TestBasicAddRemoveUser()
        {
			IConfiguration config = getTestConfiguration();
			using(var context =  new SGContext(config))
			{
				var adminTask = LoginTokenTasks.LoginAsync(context, Defaults.UserAdmin, Defaults.UserAdminPassword);
				adminTask.Wait();
				var admin = adminTask.Result;

				var userA = TryCreateUser(context, admin, "user@localhost", "pwd");
				Assert.IsNotNull(userA, "Failed to create a valid user user@localhost");
				var userB = TryCreateUser(context, admin, "user@localhost", "pwd");
				Assert.IsNull(userB, "Incorrectly succeeded in creating a user that already exists.");

				var userC = TryCreateUser(context, admin, "user2@localhost", "pwd");
				Assert.IsNotNull(userA, "Failed to create a valid user user2@localhost");

				TryChangeUserDisplayEmailAndName(context, admin, userA, "userA@localhost", "User A new name");

				var userTask = UserTasks.QuickGetUserNoAuthCheckAsync(context, "userA@localhost");
				userTask.Wait();
				var userA_2 = userTask.Result;

				Assert.IsNotNull(userA_2, "User failed to be retrieved after email update");
				Assert.AreEqual(userA_2.Id, userA.Id, "User that came back after email change had a different ID.");
				Assert.AreEqual(userA_2.DisplayName, "User A new name", "Displayname update failed.");
			}
		}

		[Test]
		public void TestGetAnonymousToken()
		{
			IConfiguration config = getTestConfiguration();
			using(var context =  new SGContext(config))
			{
				var loginToken = LoginTokenTasks.GetAnonmymousToken();
				Assert.IsTrue(loginToken.IsAnonymous(), "Anonymous token is not anonymous");
				Assert.AreEqual(loginToken.Id, 0, "The anonymous token ID must be Zero.");
				Assert.AreEqual(loginToken.UserId, -1, "Anonymous user id must be -1");
				Assert.AreEqual(loginToken.User.Id, -1, "Anonymous user id must be -1");
				var role = new UserRole(loginToken.User.RawRole);
				Assert.IsTrue(role.IsGuest, "The guest token must report as role guest");
				Assert.IsFalse(role.IsAdmin, "The guest token must not report as role admin");
				Assert.IsFalse(role.IsUser, "The guest token must not report as role user");
			}
		}
    }
}