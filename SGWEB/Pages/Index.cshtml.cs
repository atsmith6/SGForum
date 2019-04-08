using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Configuration;

using SGDataModel;

namespace SGWEB.Pages
{
    public class IndexModel : SGBasePage
    {
		private IHttpClientFactory _clientFactory;

		private int? CurrentParentTopicId { get; set; }

		private List<Topic> _forumTopics;
		public List<Topic> ForumTopics
		{
			get { return _forumTopics; }
		}

		

		private List<Topic> _topicPath;
		public List<Topic> TopicPath
		{
			get { return _topicPath;}
		}

		public PageNumLogic TopicPageLogic;
		public PageNumLogic PostPageLogic;

		private int TopicPageSize { get; set; } = 3;
		private int TotalNumberOfTopics { get; set; }

		public bool CanEditTopics { get; set; }

		public List<Post> Posts { get; set; }

		private int PostPageSize { get; } = 6;
		private int TotalNumberOfPosts { get; set; }
		public bool ShowPosts { get; set; }

		public bool CanEditPosts { get; set; }

		public IndexModel(IHttpClientFactory clientFactory, IConfiguration config) : base(clientFactory, config)
		{
			_clientFactory = clientFactory;
		}

		public string TrimTopic(string title)
		{
			const int len = 15;
			if (title.Length > len)
				return title.Substring(0, len-3) + "...";
			return title;
		}

        public async Task OnGetAsync()
        {
			var topicId = getIntParam("topicId");
			if (topicId != null && topicId == -1)
				topicId = null;
			CurrentParentTopicId = topicId;
			var topicsPage = getIntParam("topicsPage") ?? -1;
			var postsPage = getIntParam("postsPage") ?? -1;

			var token = await GetTokenAsync();
			await HandleTopics(token, topicId, topicsPage);
			await HandlePosts(token, topicId, postsPage);
		}

		private PageTurnLogic _ptl;
		private PageTurnLogic PageTurnLogic
		{
			get
			{
				if (_ptl == null)
				{
					_ptl = new PageTurnLogic();
					_ptl.Add(TopicPageLogic);
					_ptl.Add(PostPageLogic);
					if (CurrentParentTopicId != null)
						_ptl.Add("topicId", CurrentParentTopicId.Value.ToString());
				}
				return _ptl;
			}
		}

		public string CreateTopicPageNavHTML()
		{
			if (ForumTopics.Count == 0)
				return string.Empty;
			var ptl = PageTurnLogic;
			return ptl.quickNavLink("/", "topicsPage");
		}

		public string CreatePostsPageNavHTML()
		{
			if (Posts.Count == 0)
				return string.Empty;
			var ptl = PageTurnLogic;
			return ptl.quickNavLink("/", "postsPage");
		}

		public string CreateEditPostHrefHTML(int postId)
		{
			int topicId = CurrentParentTopicId ?? -1;
			return $"/Post?postId={postId}&mode=edit&topicId={topicId}&topicsPage={TopicPageLogic.CurrentPage}&postsPage={PostPageLogic.CurrentPage}";
		}

		public string CreateNewPostHrefHTML()
		{
			int topicId = CurrentParentTopicId ?? -1;
			return $"/Post?mode=new&topicId={topicId}&topicsPage={TopicPageLogic.CurrentPage}&postsPage={PostPageLogic.CurrentPage}";
		}

		public string CreateNewTopicHrefHTML()
		{
			int topicId = CurrentParentTopicId ?? -1;
			return $"/Topic?topicId=new&parentTopicId={topicId}&topicsPage={TopicPageLogic.CurrentPage}&postsPage={PostPageLogic.CurrentPage}";
		}

		private async Task HandleTopics(LoginToken token, int? topicId, int topicsPage)
		{
			if (topicId != null && topicId.Value != -1)
			{
				var topic = await Api.GetTopicAsync(token.Id, topicId.Value);
				if (topic == null)
				{
					throw new Exception("Invalid topicId");
				}
				CanEditTopics = token.User.RawRole >= topic.RoleToEdit;
				CanEditPosts = token.User.RawRole >= topic.RoleToEdit;
			}
			else
			{
				CanEditPosts = false;
			}

			var topicResult = await Api.GetTopicsPageAsync(token.Id, topicId, topicsPage, TopicPageSize);

			if (topicResult.ParentList == null || topicResult.ParentList.Count == 0)
			{
				_topicPath = null;
			}
			else
			{
				var current = topicResult.ParentList.Last();
				if (current.RoleToRead > token.User.RawRole)
				{
					Response.Redirect("/Index");
					return;
				}

				_topicPath = topicResult.ParentList;
			}

			_forumTopics = topicResult.Topics;

			TotalNumberOfTopics = topicResult.TopicCount;

			TopicPageLogic = new PageNumLogic("topicsPage", topicResult.CurrentPage, topicResult.TopicCount, TopicPageSize);
		}

		private async Task HandlePosts(LoginToken token, int? topicId, int postsPage)
		{
			if (topicId == null)
			{
				ShowPosts = false;
			}
			else
			{
				ShowPosts = true;
				var postResult = await Api.GetPostPageAsync(token.Id, topicId.Value, postsPage, PostPageSize);
				TotalNumberOfPosts = postResult.Count;
				Posts = postResult.Posts;

				PostPageLogic = new PageNumLogic("postsPage", postResult.CurrentPage, TotalNumberOfPosts, PostPageSize);
			}
		}
    }
}
