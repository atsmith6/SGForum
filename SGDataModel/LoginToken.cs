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
    public class LoginToken
    {
		public static Int64 AnonymousLoginId = 0;

    	[Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
		[Required]
		public Int64 Id { get; set; }
		public int UserId { get; set; }
		public virtual User User { get; set; }
		public DateTime Expires { get; set; }

		public LoginToken CloneForExport()
		{
			var ret = new LoginToken();
			ret.Id = Id;
			ret.UserId = UserId;
			ret.User = User.CloneForExport();
			ret.Expires = Expires;
			return ret;
		}
    }

	public static class LoginTokenExtension
	{
		public static bool IsAnonymous(this LoginToken token)
		{
			return token.Id == LoginToken.AnonymousLoginId;
		}

		public async static Task<bool> IsAdmin(this LoginToken token, SGContext context)
		{
			if (context == null || token.Expires < DateTime.UtcNow)
				return false;
			token = await LoginTokenTasks.GetLoginTokenAsync(context, token.Id);
			if (token != null)
			{
				var role = new UserRole(token.User.RawRole);
				return role.IsAdmin;
			}
			return false;
		}
	}
}