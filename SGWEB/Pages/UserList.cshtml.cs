using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

using SGDataModel;

namespace SGWEB.Pages
{
    public class UserListModel : SGBasePage
    {

		public List<User> Users { get; private set; }

		public UserListModel(IHttpClientFactory clientFactory, IConfiguration config) : base(clientFactory, config)
		{

		}

		private async Task<LoginToken> GetAdminTokenAsync()
		{
			var token = await GetTokenAsync();
			if (token != null)
			{
				var role = new UserRole(token.User.RawRole);
				if (role.IsAdmin)
					return token;
			}
			return null;
		}

        public async Task<IActionResult> OnGetAsync()
        {
			var token = await GetAdminTokenAsync();
			if (token == null)
				return Redirect("/");
			var users = await Api.GetUsers(token.Id);
			users.Sort((u1,u2)=>string.Compare(u1.Email, u2.Email, true));
			Users = users;
			return Page();
        }

		// public async Task<IActionResult> OnPostAsync()
		// {
		// 	var token = await GetAdminTokenAsync();
		// 	if (token == null)
		// 		return Redirect("/");
		// 	return Page();
		// }
    }
}