using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGDataModel;
using Newtonsoft.Json;

namespace SGAPI.Controllers
{
    [Route("[controller]")]
	[Produces("application/json")]
    [ApiController]
	// Note: Changed the base class from ControllerBase to Controller.
    public class InfoController : Controller
    {
        private SGContext _context;

		private class ApiInfo
		{
			public string schemaVersion { get; set; }
			public string appVersion { get; set; }
		}

        public InfoController(SGContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<string> GetAsync()
        {
			var data = await _context.databaseInfo.ToListAsync();
			if (data.Count > 0)
			{
				var info = data[0];

				var apiInfo = new ApiInfo();
				apiInfo.schemaVersion = $"{info.MajVersion}.{info.MinVersion}";
				apiInfo.appVersion = $"{Constants.MajVersion}.{Constants.MinVersion}";
				if (Constants.InDevelopment)
				{
					apiInfo.appVersion += $" (In development)";
				}

				return JsonConvert.SerializeObject(apiInfo);
			}
			return new ApiResult<string>("Error", "No database info available.").ToJson();

			
        }
    }
}