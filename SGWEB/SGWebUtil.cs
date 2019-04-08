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

namespace SGWEB
{
    public static class SGWebUtil
    {
		public static string TranslateRole(int rawRole)
		{
			var userRole = new UserRole(rawRole);
			if (userRole.IsAdmin)
				return "Administrator";
			else if (userRole.IsUser)
				return "User";
			else if (userRole.IsGuest)
				return "Guest";
			return "Unknown";
		}

        public static List<SelectListItem> CreateRoleNameList(bool includeGuest = true)
		{
			var ret = new List<SelectListItem>();

			if (includeGuest)
				ret.Add(new SelectListItem{ Value = "1", Text = TranslateRole(UserRole.Guest) });
			ret.Add(new SelectListItem{ Value = "2", Text = TranslateRole(UserRole.User) });
			ret.Add(new SelectListItem{ Value = "3", Text = TranslateRole(UserRole.Admin) });
			
			return ret;
		}
    }
}