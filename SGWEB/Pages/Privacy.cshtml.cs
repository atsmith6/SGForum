using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Configuration;

namespace SGWEB.Pages
{
    public class PrivacyModel : SGBasePage
    {
		public PrivacyModel(IHttpClientFactory clientFactory, IConfiguration config) : base(clientFactory, config)
		{

		}
        public void OnGet()
        {
        }
    }
}