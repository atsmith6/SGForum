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
    public class PostModel : SGBasePage
    {
		[TempData]
		public string Message { get; set; }

		[BindProperty]
		public List<SelectListItem> RoleNames { get; } = SGWebUtil.CreateRoleNameList();
		[BindProperty]
		public List<SelectListItem> EditRoleNames { get; } = SGWebUtil.CreateRoleNameList(false);

		/* ====== Hidden Properties ====== */
		[BindProperty]
		public int TopicId { get; set; }

		[BindProperty]
		public int LastTopicPage { get;set; }

		[BindProperty]
		public int LastPostPage { get;set; }

		[BindProperty]
		public int PostId { get; set; }

		[BindProperty]
		public string Mode { get; set; }

		/* ====== Exposed Properties ====== */

		[BindProperty]
		public string Title { get; set; }

		[BindProperty]
		public string BodyText { get; set; }

		[BindProperty]
		public int RoleToRead { get; set;}

		[BindProperty]
		public int RoleToEdit { get; set;}

		/* ====== Instance Properties ====== */

		public PostModel(IHttpClientFactory clientFactory, IConfiguration config) : base(clientFactory, config)
		{
			
		}

		private void GetIdsFromParameters()
		{
			var mode = getParam("mode");
			if (mode == null)
				mode = "new";
			else if (mode != "edit" && mode != "new")
				throw new Exception("Invalid parameter: mode");
			var postId = getIntParam("postId");
			if (mode != "new" && (postId == null || postId.Value < 0))
				throw new Exception("Invalid paramter: postId");
			var topicId = getIntParam("topicId");
			if (topicId == null)
				throw new Exception("Invalid paramter: topicId");
			var topicsPage = getIntParam("topicsPage");
			if (topicsPage == null)
				throw new Exception("Invalid paramter: topicsPage");
			var postsPage = getIntParam("postsPage");
			if (postsPage == null)
				throw new Exception("Invalid paramter: postsPage");

			PostId = postId ?? -1;
			TopicId = topicId.Value;
			LastTopicPage = topicsPage.Value;
			LastPostPage = postsPage.Value;
			Mode = mode;
		}

		public string CreateReturnPath()
		{
			int lastPostPage = Mode == "new" ? -1 : LastPostPage;
			return $"/Index?topicId={TopicId}&topicsPage={LastTopicPage}&postsPage={lastPostPage}";
		}

        public async Task<IActionResult> OnGetAsync()
        {
			var token = await GetTokenAsync();
			GetIdsFromParameters();

			if (Mode == "new")
			{
				var post = new Post();
				Title = post.Title;
				BodyText = post.Body;
				RoleToRead = post.RoleToRead;
				RoleToEdit = Math.Max(token.User.RawRole, UserRole.User);
			}
			else
			{
				var post = await Api.GetPostAsync(token.Id, PostId);

				Title = post.Title;
				BodyText = post.Body;
				RoleToRead = post.RoleToRead;
				RoleToEdit = post.RoleToEdit;
			}

			return Page();
        }

		public async Task<IActionResult> OnPostAsync(string button)
		{
			if (button == "save")
			{
				var token = await GetTokenAsync();

				Post post;
				
				if (Mode == "new")
					post = new Post();
				else
					post = await Api.GetPostAsync(token.Id, PostId);
				
				post.Title = Title;
				post.Body = BodyText;
				post.RoleToRead = RoleToRead;
				post.RoleToEdit = RoleToEdit;

				if (Mode == "new")
				{
					post.UserId = token.UserId;
					post.ParentId = TopicId;
					post = await Api.CreatePostAsync(token.Id, post);
				}
				else
					post = await Api.UpdatePostAsync(token.Id, post);

				return Redirect(CreateReturnPath());
			}
			else
			{
				return Redirect(CreateReturnPath());
			}			
		}
    }
}