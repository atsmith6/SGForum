using System;
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
	

    public class LoginModel : SGBasePage
    {

		[BindProperty]
		public string Email { get; set; }
		[BindProperty]
		public string Password { get; set; }

		[TempData]
		public string Message { get; set; }

		public LoginModel(IHttpClientFactory clientFactory, IConfiguration config) : base(clientFactory, config)
		{

		}

        public async Task OnGetAsync()
        {
			var action = getParam("action");
			if (action == "logout")
			{
				await LogoutAsync();
				Response.Redirect("/Index");
			}
        }

		public async Task<IActionResult> OnPostAsync()
		{
			string email = Email.Trim();
			string password = Password.Trim();
			if (string.IsNullOrEmpty(email))
			{
				Message = "Invalid email address.";
				return Page();
			}
			if (string.IsNullOrEmpty(password))
			{
				Message = "Invalid password.";
				return Page();
			}
			var token = await Api.LoginAsync(email, password);
			if (token == null || token.IsAnonymous())
			{
				Message = "Authorisation failed.";
				return Page();
			}

			HttpContext.Session.SetString("token", token.Id.ToString());

			return Redirect("/");
		}

		private async Task LogoutAsync()
		{
			var token = await GetTokenAsync();
			if (token != null && !token.IsAnonymous())
			{
				HttpContext.Session.Remove("token");
				var error = await Api.LogoutAsync(token) as ApiError;
				if (error != null)
				{
					//throw new Exception($"Failed to logout: {error.Value}");
				}
			}
		}
    }
}