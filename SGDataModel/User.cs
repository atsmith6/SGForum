using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Extensions;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace SGDataModel
{
    public class User
    {
		// TODO: We should ensure that user display names are unique.

		public int Id { get; set; }
		[Required]
		public int RawRole { get; set; }
		[StringLength(100)]
		[Required]
		public string Email { get; set; }
		[StringLength(100)]
		public string DisplayName { get; set; }
		[Required]
		[StringLength(64)]
		public string PasswordHash { get; set; }
		[Required]
		[StringLength(32)]
		public string PasswordSalt { get; set; }
		[Required]
		public bool Active { get; set; }

		public User CloneForExport()
		{
			var ret = new User();
			ret.Id = Id;
			ret.RawRole = RawRole;
			ret.Email = Email;
			ret.DisplayName = DisplayName;
			ret.PasswordHash = "";
			ret.PasswordSalt = "";
			ret.Active = Active;
			return ret;
		}
	}

	public static class UserTasks
	{
		public static async Task<User> CreateUserNoAuthCheckAsync(SGContext context, string email, string displayname, string password, int roles)
		{
			var (salt, hash) = CryptoUtils.CreateHashAndSalt(password);
			var user = new User { 
				RawRole = roles,
				Email = email.ToLower(),
				DisplayName = displayname,
				PasswordHash = hash,
				PasswordSalt = salt,
				Active = true };
			context.users.Add(user);
			await context.SaveChangesAsync();
			return user;
		}

		public static async Task<User> CreateUserAsync(SGContext context, LoginToken token, string email, string displayname, string password, int roles)
		{
			var role = new UserRole(token.User.RawRole);
			if (role.IsAdmin)
			{
				var user = await CreateUserNoAuthCheckAsync(context, email, displayname, password, roles);
				user = user.CloneForExport();	
				return user;
			}
			else
				throw new Exception("Unauthorised");
		}

		public static async Task UpdatePasswordAsync(SGContext context, string email, string oldPassword, string newPassword)
		{
			var emailLower = email.ToLower();
			var userList = await (from u in context.users
								where u.Email == emailLower
								select u).ToListAsync();
			if (userList.Count == 0)
				throw new Exception($"User {email} not found.");

			var user = userList[0];

			var passwordOk = CryptoUtils.CheckPassword(oldPassword, user.PasswordHash, user.PasswordSalt);
			if (!passwordOk)
				throw new Exception($"Authorization for {email} failed.");

			var (salt, hash) = CryptoUtils.CreateHashAndSalt(newPassword);
			user.PasswordSalt = salt;
			user.PasswordHash = hash;
			context.users.Update(user);
			await context.SaveChangesAsync();
		}

		public static async Task<User> QuickGetUserNoAuthCheckAsync(SGContext context, string email, int Id = -1)
		{
			List<User> userList;
			if (Id == -1)
			{
				var emailLower = email.ToLower();
				userList = await (from u in context.users
							where u.Email == emailLower
							select u).ToListAsync();
			}
			else
			{
				userList = await (from u in context.users
							where u.Id == Id
							select u).ToListAsync();
			}
			if (userList.Count == 0)
				return null;
			var user = userList[0];
			return user;
		}

		public static async Task SetUserActiveAsync(SGContext context, LoginToken token, string email, bool active)
		{
			var role = new UserRole(token.User.RawRole);
			if (role.IsAdmin)
			{
				var user = await QuickGetUserNoAuthCheckAsync(context, email);
				if (user != null)
				{
					user.Active = active;
					context.users.Update(user);
					await context.SaveChangesAsync();
					if (!active)
						await LoginTokenTasks.RemoveExistingTokenForUser(context, user.Id);
				}
				else
					throw new Exception($"User {email} not found");
			}
			else
				throw new Exception("Unauthorised");
		}

		public static async Task UpdateUserAsync(SGContext context, LoginToken token, User user)
		{
			var role = new UserRole(token.User.RawRole);
			if (role.IsAdmin || user.Id == token.UserId)
			{
				var existingRecord = await QuickGetUserNoAuthCheckAsync(context, null, user.Id);
				if (existingRecord != null)
				{
					if(user.RawRole != existingRecord.RawRole && !role.IsAdmin)
						throw new Exception("Only administrators may change the user's role");
					if(!UserRole.RoleIsValid(user.RawRole))
						throw new Exception("The new user role is invalid.");
					if(user.Active != existingRecord.Active && !role.IsAdmin)
						throw new Exception("Only administrators may change the user's active status");
					if(user.Id != existingRecord.Id)
						throw new Exception("User ID updates are not allowed");

					existingRecord.Email = user.Email;
					existingRecord.DisplayName = user.DisplayName;
					existingRecord.RawRole = user.RawRole;

					context.users.Update(existingRecord);
					await context.SaveChangesAsync();
				}	
			}
		}
    }

}