using System;
using System.Security.Cryptography;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Extensions;

namespace SGDataModel
{
    public class LoginTokenTasks
    {
        public static async Task<LoginToken> LoginAsync(SGContext context, string email, string password, Int64 forceId = 0)
		{
			var usersQuery = from u in context.users where u.Email == email select u;
			var users = await usersQuery.ToListAsync();
			if (users.Count() > 0)
			{
				var user = users.First();
				bool passwordGood = CryptoUtils.CheckPassword(password, user.PasswordHash, user.PasswordSalt);
				if (passwordGood)
				{
					var token = await(CreateTokenAsync(context, user, forceId));
					token = token.CloneForExport();
					return token;
				}
			}
			return null;
		}

		public static async Task LogoutAsync(SGContext context, LoginToken token)
		{
			string sql = "delete from tokens where UserId = @p0";
			await context.Database.ExecuteSqlCommandAsync(sql, new object[] { token.User.Id });
			await context.SaveChangesAsync();
		}

		public static async Task LogoutAsync(SGContext context, LoginToken adminToken, string email)
		{
			if (adminToken == null || !(await adminToken.IsAdmin(context)) || string.IsNullOrWhiteSpace(email))
				return;
			email = email.ToLower();
			var user = await (from u in context.users where u.Email == email select u).FirstOrDefaultAsync();
			if (user != null)
			{
				string sql = "delete from tokens where UserId = @p0";
				await context.Database.ExecuteSqlCommandAsync(sql, new object[] { user.Id });
				await context.SaveChangesAsync();
			}
		}

		private static async Task<LoginToken> CreateTokenAsync(SGContext context, User user, Int64 forceIdForTesting = 0)
		{
			try
			{
				await context.Database.BeginTransactionAsync();

				await RemoveExistingTokenForUser(context, user.Id); 

				Int64 tokenId;
				if (forceIdForTesting == 0)
				{
					var rng = RandomNumberGenerator.Create();
					while(true)
					{
						byte[] buf = new byte[8];
						rng.GetBytes(buf);
						tokenId = BitConverter.ToInt64(buf, 0);

						if (tokenId != LoginToken.AnonymousLoginId)
							break;
					}
				}
				else
				{
					tokenId = forceIdForTesting;
				}
				var token = new LoginToken { Id = tokenId, User = user, Expires = DateTime.UtcNow.AddDays(1.0) };
				
				context.tokens.Add(token);
				await context.SaveChangesAsync();
				context.Database.CommitTransaction();

				return token;
			}
			catch
			{
				return null;
			}
		}

		public static async Task<LoginToken> GetLoginTokenAsync(SGContext context, Int64 tokenId)
		{
			try
			{
				var tokens = await (from t in context.tokens where t.Id == tokenId select t).Include(t => t.User).ToListAsync();

				//var tokenB = await context.tokens.Where(t => t.Id == tokenId).Include(t => t.User).Select(t => t).ToListAsync();

				if (tokens.Count > 0)
				{
					var now = DateTime.UtcNow;
					var token = tokens[0];
					if (token.Expires <= now || token.User.Active == false)
					{
						await RemoveExistingTokenForUser(context, token.UserId);
					}
					else
					{
						token.Expires = now.AddYears(1);
						await context.SaveChangesAsync();
						token = token.CloneForExport();
						return token;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		public static LoginToken GetAnonmymousToken()
		{
			LoginToken token = new LoginToken()
			{
				Id = LoginToken.AnonymousLoginId,
				Expires = DateTime.UtcNow.AddYears(1),
				UserId = -1,
				User = new User() 
				{
					Id = -1,
					Email = "",
					DisplayName = "Guest",
					RawRole = UserRole.Guest,
					Active = true
				}
			};
			return token;
		}

		// public static void CheckTokenValidOrThrow(SGContext context, LoginToken token)
		// {
		// 	var t2 = (from t in context.tokens where t.Id == token.Id select t).FirstOrDefault();
		// 	if (t2 == null || t2.Expires <= DateTime.UtcNow)
		// 		throw new Exception("Invalid token");
		// }

		public static async Task RemoveExistingTokenForUser(SGContext context, int userId)
		{
			var tokens = await (from t in context.tokens where t.UserId == userId select t).ToListAsync();
			if (tokens.Count > 0)
			{
				foreach (var token in tokens)
				{
					context.tokens.Remove(token);
				}
				await context.SaveChangesAsync();
			}
		}
    }
}