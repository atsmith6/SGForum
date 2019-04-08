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

    public class TopicModel : SGBasePage
    {
		[TempData]
		public string Message { get; set; }

		[BindProperty]
		public List<SelectListItem> RoleNames { get; } = SGWebUtil.CreateRoleNameList();
		
		[BindProperty]
		public int ParentTopicId { get; set; }

		[BindProperty]
		public int TopicId { get; set; }

		[BindProperty]
		public int LastTopicPage { get;set; }

		[BindProperty]
		public int LastPostPage { get;set; }

		[BindProperty]
		public string TopicTitle { get; set; }

		[BindProperty]
		public int RoleToRead { get; set;}

		[BindProperty]
		public int RoleToEdit { get; set;}

		[BindProperty]
		public string Mode { get; set; }

		public bool IsReadOnly { get; set; }

		public bool IsDelete { get; set; }


		public TopicModel(IHttpClientFactory clientFactory, IConfiguration config) : base(clientFactory, config)
		{
			
		}

		private int getTopicId()
		{
			string topicIdStr = getParam("topicId");
			int? topicId = topicIdStr == "new" ? -1 : getIntParam("topicId");
			if (topicId == null)
				throw new Exception("Invalid topic Id.  Must be \"new\" or numeric");
			return topicId.Value;
		}

        public async Task<IActionResult> OnGetAsync()
        {
			var token = await GetTokenAsync();

			var mode = getParam("mode");
			IsDelete = false;
			if (mode == "delete")
				IsDelete = true;
			else if (mode != "edit")
				mode = "view";

			int topicId = getTopicId();
			if (topicId == -1)
				mode = "new";
			
			LastTopicPage = getIntParam("topicsPage") ?? -1;
			LastPostPage = getIntParam("postsPage") ?? -1;

			IsReadOnly = (mode != "edit" && mode != "new");



			var topic = mode == "new" ? new Topic() : await Api.GetTopicAsync(token.Id, topicId);
			TopicId = mode == "new" ? -1 : topic.Id;
			if (mode == "new")
			{	
				var parentTopicId = getIntParam("parentTopicId");
				if (parentTopicId == null)
					throw new Exception("Invalid get parameter: parentTopicId");
				ParentTopicId = parentTopicId.Value;
				TopicTitle = "";
				RoleToRead = UserRole.Guest;
				RoleToEdit = token.User.RawRole;
			}
			else
			{
				ParentTopicId = topic.ParentId ?? -1;
				TopicTitle = topic.Title;
				RoleToRead = topic.RoleToRead;
				RoleToEdit = topic.RoleToEdit;
			}
			Mode = mode;
			// SetupViewPage();
			return Page();
        }

		private string CreateRedirectString(string mode)
		{
			return $"/Topic?topicId={TopicId}&topicsPage={LastTopicPage}&postsPage={LastPostPage}&mode={mode}";
		}

		public string CreateReturnPath()
		{
			return $"/Index?topicId={ParentTopicId}&topicsPage={LastTopicPage}&postsPage={LastPostPage}";
		}

		public async Task<IActionResult> OnPostAsync(string button)
		{
			if (button == "return")
			{
				return Redirect(CreateReturnPath());
			}
			else if (button == "delete")
			{
				var token = await GetTokenAsync();
				await Api.DeleteTopicAsync(token.Id, TopicId);
				return Redirect(CreateReturnPath());
			}
			else if (button == "edit")
			{
				return Redirect(CreateRedirectString("edit"));
			}
			else if (button == "save")
			{
				var token = await GetTokenAsync();

				if (Mode == "new")
				{
					var topic = new Topic();
					var parentTopicId = ParentTopicId == -1 ? null : new Nullable<int>( ParentTopicId );
					topic.Title = TopicTitle;
					topic.RoleToRead = RoleToRead;
					topic.RoleToEdit = RoleToEdit;
					topic = await Api.CreateTopicAsync(token.Id, parentTopicId, topic );
					if (topic == null)
						Message = $"Error: Failed to create the topic";
					else
						Message = null;

					LastTopicPage = 0;
					return Redirect(CreateReturnPath());
				}
				else
				{
					var topic = await Api.GetTopicAsync(token.Id, TopicId);
					topic.Title = TopicTitle;
					topic.RoleToRead = RoleToRead;
					topic.RoleToEdit = RoleToEdit;
					var error = await Api.UpdateTopicAsync(token.Id, topic);
					if (error != null)
						Message = $"Error: {error.Value}";
					else
						Message = "Topic updated.";
				}

				return Redirect(CreateRedirectString("view"));
			}
			return Redirect(CreateRedirectString("view"));
		}
    }
}