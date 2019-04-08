using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using SGDataModel;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace SGAPI.Controllers
{
	[Route("[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {
		private SGContext _context;
		private IConfiguration _config;
 		public ApiController(SGContext context, IConfiguration config)
        {
            _context = context;
			_config = config;
        }

		// TODO: Change this to, when in dev mode, return an HTML API document.
		[HttpGet]
		public ContentResult OnGetAsync()
		{
			var user = new User();
			user.Email = "a@b.c";
			user.DisplayName = "abc";
			return Content(JsonConvert.SerializeObject(user), "application/json");
		}

		[HttpPost]
		public async Task<ContentResult> OnPostAsync()
		{
			try
			{
				if (Request.Body != null)
				{
					using(TextReader reader = new StreamReader(Request.Body, Encoding.UTF8))
					{
						string jsonText = await reader.ReadToEndAsync();
						System.Console.WriteLine($"Read JSON: {jsonText}");
						var apiCall = ApiCall.FromJson(jsonText);

						var handler = new ApiHandler(_context);
						var result = await handler.processCallAsync(apiCall);
						if (result is ApiError)
							return processExit(result, (result as ApiError).HttpStatusCode);
						else
							return processExit(result, 200);
					}
				}
				return processExit(new ApiError("Post body empty"), 400);
			}
			catch(Exception ex)
			{
				return processExit(new ApiError($"Server Exception: {ex.Message}", 500), 500);
			}
		}

		private ContentResult processExit(ApiResult result, int statusCode)
		{
			Response.StatusCode = statusCode;
			return Content(result.ToJson(), "application/json");
		}
    }
}