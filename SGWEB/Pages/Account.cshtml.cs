using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;

using SGDataModel;

namespace SGWEB.Pages
{
	

    public class AccountModel : SGBasePage
    {
		[TempData]
		public string Message { get; set; }

		public string Mode { get; set; }

		public int UserId { get; set; }

		[BindProperty]
		public User CurrentUser { get; set; }

		[BindProperty]
		public List<SelectListItem> RoleNames { get; }

		public List<SelectListItem> ActiveNames { get; }

		public AccountModel(IHttpClientFactory clientFactory, IConfiguration config) : base(clientFactory, config)
		{
			RoleNames = new List<SelectListItem>()
			{
				new SelectListItem { Value = "2", Text = TranslateRole(UserRole.User) },
				new SelectListItem { Value = "3", Text = TranslateRole(UserRole.Admin) }
			};
			ActiveNames = new List<SelectListItem>()
			{
				new SelectListItem { Value = "true", Text = "Active" },
				new SelectListItem { Value = "false", Text = "Disabled" }
			};
		}

        public async Task<IActionResult> OnGetAsync()
        {
			await SetupViewPage();
			return Page();
        }

		private async Task SetupViewPage()
		{
			var token = await GetTokenAsync();

			var mode = getParam("mode");
			if (mode == null)
				mode = "view";

			var userId = getIntParam("userId");
			if (userId == null)
				userId = token.UserId;
			UserId = userId.Value;

			if (UserId == token.UserId)
				CurrentUser = token.User;
			else
			{
				CurrentUser = await Api.GetUser(token.Id, UserId);
			}

			this.Mode = mode;
		}
		

		private bool AdminIsUpdatingADifferentAccount
		{
			get
			{
				var tokenTask = GetTokenAsync();
				tokenTask.Wait();
				var token = tokenTask.Result;
				return token.UserId != CurrentUser.Id;
			}
		}

		private void updatePermittedFields(User orig, User updated)
		{
			orig.Email = updated.Email;
			orig.DisplayName = updated.DisplayName;
			if (AdminIsUpdatingADifferentAccount)
			{
				orig.RawRole = updated.RawRole;
			}
		}

		public async Task<IActionResult> OnPostAsync(string button)
		{
			if (button == "cancel")
			{
				await SetupViewPage();
				return Redirect($"/Account?mode=view&userId={CurrentUser.Id}");;
			}
			else if (button == "edit")
			{
				await SetupViewPage();
				return Redirect($"/Account?mode=edit&userId={CurrentUser.Id}");;
			}

			var token = await GetTokenAsync();
			//var oldUser = token.User;

			User oldUser;
			if (CurrentUser.Id == token.UserId)
				oldUser = token.User;
			else
				oldUser = await Api.GetUser(token.Id, CurrentUser.Id);

			updatePermittedFields(oldUser, CurrentUser);
			try
			{
				await Api.UpdateUser(token.Id, oldUser);
				Message = "Account updated.";
			}
			catch(Exception ex)
			{
				Message = $"Error: Update failed.  The email or display name may already be in user: {ex.Message}";
			}

			return Redirect($"/Account?mode=view&userId={oldUser.Id}");
		}
    }
}