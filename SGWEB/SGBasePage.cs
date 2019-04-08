using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Configuration;
using SGDataModel;

namespace SGWEB
{
    public class SGBasePage : PageModel
    {
		public IConfiguration config { get; private set; }
		public IHttpClientFactory clientFactory { get; private set; }
		public Api Api { get; private set; }

		private LoginToken _token;
		
		public (string, string) LoginPrompt 
		{
			get
			{
				bool anon = _token == null || _token.IsAnonymous();
				if (anon)
					return ("Login", "login");
				var label = $"Logout {_token.User.Email}";
				return (label, "logout");
			}
		}

		public LoginToken Token { get { return _token; } }

		public bool IsLoggedIn
		{
			get
			{
				return _token != null && !_token.IsAnonymous();
			}
		}

		public bool IsAdmin
		{
			get
			{
				var role = new UserRole(_token == null ? UserRole.Guest : _token.User.RawRole);
				return role.IsAdmin;
			}
		}

		public string TranslateRole(int rawRole)
		{
			return SGWebUtil.TranslateRole(rawRole);
		}

        public SGBasePage(IHttpClientFactory clientFactory, IConfiguration config)
		{
			this.config = config;
			this.clientFactory = clientFactory;
			this.Api = new Api(clientFactory, config);
		}

		public int? getIntParam(string indexer)
		{
			var v = Request.Query[indexer];
			if (v.Count > 0)
			{
				var s = v[0];
				int ret;
				if (int.TryParse(s, out ret))
					return ret;
			}
			return null;
		}

		public string getParam(string indexer)
		{
			var v = Request.Query[indexer];
			if (v.Count > 0)
				return v[0];
			return null;
		}

		public async Task<LoginToken> GetTokenAsync()
		{
			if (_token == null)
			{
				Int64 tokenId; 
				if (!Int64.TryParse(HttpContext.Session.GetString("token"), out tokenId))
					tokenId = 0;
				_token = await Api.GetTokenAsync(tokenId);
			}
			return _token;
		}

		private string MonthToString(int month)
		{
			switch(month)
			{
				case 1: return "Jan";
				case 2: return "Feb";
				case 3: return "Mar";
				case 4: return "Apr";
				case 5: return "May";
				case 6: return "Jun";
				case 7: return "Jul";
				case 8: return "Aug";
				case 9: return "Sep";
				case 10: return "Oct";
				case 11: return "Nov";
				case 12: return "Dec";
				default: return $"<<month>>";
			}
		}
		public string FormatDate(DateTime dt)
		{
			// TODO: Add a conversion to user local time if logged in

			return $"{dt.Day}-{MonthToString(dt.Month)}-{dt.Year} {dt.Hour.ToString().PadLeft(2, '0')}:{dt.Minute.ToString().PadLeft(2, '0')} UTC";
		}
    }
}