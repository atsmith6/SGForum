using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using SGDataModel;
using Newtonsoft.Json;

namespace SGWEB
{
    public class Api
    {
		public string SiteTitle 
		{
			get
			{
				var title = _config["siteTitle"];
				if(!string.IsNullOrWhiteSpace(title))
					return title;
				return "SG Forum";
			}
		}

		public string CopyrightString
		{
			get
			{
				int year = DateTime.UtcNow.Year;
				if (year == 2019)
					return "&copy; 2019";
				else
					return "&copy; 2019 - {year}";
			}
		}

		private LoginToken _token;

		private IHttpClientFactory _clientFactory;
		private IConfiguration _config;

		public Api(IHttpClientFactory clientFactory, IConfiguration config)
		{
			_clientFactory = clientFactory;
			_config = config;
		}

		// --------------- Helper -----------------

		private async Task<ApiResult> makeApiCallAsync<T>(ApiCall call)
		{
			var client = _clientFactory.CreateClient();
			var apiAddress = _config["APIAddress"];
			var response = await client.PostAsync(apiAddress,
				new StringContent(call.ToJson(), System.Text.Encoding.UTF8));

			if (response.IsSuccessStatusCode)
			{
				string jsonString = await response.Content
					.ReadAsStringAsync();
				ApiResult tmp = ApiResult.FromJson(jsonString);
				if (tmp is ApiError)
				{
					throw new Exception($"API Error: {(tmp as ApiError).Value}");
				}
				return ApiResult<T>.FromJson(jsonString);
			}
			else
			{
				throw new Exception($"Unexpected result from Client for {call.Func}: {(int)response.StatusCode}.");
			}
		}

		private async Task<ApiResult> makeApiCallAsync(ApiCall call, bool throwOnRemoteError = true)
		{
			// Note: throwOnRemoteError pertains to status-codes not 200.  Other problems here like the client failing to connect should throw!

			var client = _clientFactory.CreateClient();
			var apiAddress = _config["APIAddress"];
			var response = await client.PostAsync(apiAddress,
				new StringContent(call.ToJson(), System.Text.Encoding.UTF8));

			var statusCode = (int)response.StatusCode;
			if (statusCode == 200 || statusCode == 400 || statusCode == 500)
			{
				string jsonString = await response.Content
					.ReadAsStringAsync();
				ApiResult tmp = ApiResult.FromJson(jsonString);
				if (tmp is ApiError && throwOnRemoteError)
				{
					var error = tmp as ApiError;
					throw new Exception($"API Error ({error.HttpStatusCode}): {error.Value}");
				}
				return tmp;
			}
			else
			{
				if (throwOnRemoteError)
					throw new Exception($"Unexpected result from Client: {(int)response.StatusCode}.");
				return new ApiError($"Unexpected HTTP status code from the server {statusCode}");
			}
		}

		// -------------  TOKENS  ------------------------

		private T CheckRet<T>(ApiResult result)
		{
			var ret = result as ApiResult<T>;
			if (ret != null && ret.Result == StdResult.OK && ret.Value != null)
			{
				return ret.Value;
			}
			else
			{
				throw new Exception($"Unexpected API result: {(result != null ? result.Result : "null")}");
			}

		}

		public async Task<LoginToken> GetTokenAsync(Int64 tokenId)
		{
			if (_token == null)
			{
				var call = new ApiCall("getToken");
				call.Parameters.Add("tokenId", tokenId.ToString());
				var result = await makeApiCallAsync<LoginToken>(call);
				_token = CheckRet<LoginToken>(result);
			}
			return _token;
		}

		public async Task<ApiResult> LogoutAsync(LoginToken token)
		{
			if (token.IsAnonymous())
				return new ApiResult(StdResult.OK);
			var email = token.User.Email;
			var call = new ApiCall("logout");
			call.Parameters.Add("tokenId", token.Id.ToString());
			return await makeApiCallAsync<LoginToken>(call);
		}

		public async Task<LoginToken> LoginAsync(string email, string password)
		{
			var call = new ApiCall("login");
			call.Parameters.Add("email", email.Trim());
			call.Parameters.Add("password", password.Trim());
			var ret = await makeApiCallAsync<LoginToken>(call);
			if (ret is ApiError)
				return null;
			return (ret as ApiResult<LoginToken>).Value;
		}

		// ----------- USERS ------------

		public async Task UpdateUser(Int64 tokenId, User user)
		{
			var call = new ApiCall("updateUser");
			call.Parameters.Add("tokenId", tokenId.ToString());
			call.Parameters.Add("User", JsonConvert.SerializeObject(user));
			await makeApiCallAsync(call);
		}

		public async Task<List<User>> GetUsers(Int64 tokenId)
		{
			var call = new ApiCall("getUsers");
			call.Parameters.Add("tokenId", tokenId.ToString());
			var result = await makeApiCallAsync<List<User>>(call);
			return CheckRet<List<User>>(result);
		}

		public async Task<User> GetUser(Int64 tokenId, int userId)
		{
			var call = new ApiCall("getUser");
			call.Parameters.Add("tokenId", tokenId.ToString());
			call.Parameters.Add("userId", userId.ToString());
			var result = await makeApiCallAsync<User>(call);
			return CheckRet<User>(result);
		}

		// ----------- TOPICS -----------------

		public async Task<TopicResult> GetTopicsAsync(Int64 tokenId, int? parentTopic)
		{
			var call = new ApiCall("getTopics");
			call.Parameters.Add("tokenId", tokenId.ToString());
			if (parentTopic != null)
				call.Parameters.Add("parentTopicId", parentTopic.Value.ToString());
			var result = await makeApiCallAsync<TopicResult>(call);
			return CheckRet<TopicResult>(result);
		}

		public async Task<TopicResult> GetTopicsPageAsync(Int64 tokenId, int? parentTopic, int page, int topicPageSize)
		{
			var call = new ApiCall("getTopicPage");
			call.Parameters.Add("tokenId", tokenId.ToString());
			if (parentTopic != null)
				call.Parameters.Add("parentTopicId", parentTopic.Value.ToString());
			call.Parameters.Add("page", page.ToString());
			call.Parameters.Add("pageSize", topicPageSize.ToString());
			var result = await makeApiCallAsync<TopicResult>(call);
			return CheckRet<TopicResult>(result);
		}

		public async Task<Topic> GetTopicAsync(Int64 tokenId, int topicId)
		{
			var call = new ApiCall("getTopic");
			call.Parameters.Add("tokenId", tokenId.ToString());
			call.Parameters.Add("topicId", topicId.ToString());
			var result = await makeApiCallAsync<Topic>(call);
			return CheckRet<Topic>(result);
		}

		public async Task<Topic> CreateTopicAsync(Int64 tokenId, int? parentTopicId, Topic topic)
		{
			var call = new ApiCall("createTopic");
			call.Parameters.Add("tokenId", tokenId.ToString());
			call.Parameters.Add("title", topic.Title);
			call.Parameters.Add("roleToEdit", topic.RoleToEdit.ToString());
			call.Parameters.Add("roleToRead", topic.RoleToRead.ToString());
			if (parentTopicId != null)
				call.Parameters.Add("parentTopicId", parentTopicId.Value.ToString());
			var result = await makeApiCallAsync<Topic>(call);
			return CheckRet<Topic>(result);
		}

		public async Task<ApiError> UpdateTopicAsync(Int64 tokenId, Topic topic)
		{
			var call = new ApiCall("updateTopic");
			call.Parameters.Add("tokenId", tokenId.ToString());
			call.Parameters.Add("Topic", JsonConvert.SerializeObject(topic));
			var result = await makeApiCallAsync<Topic>(call);
			return result as ApiError;
		}

		public async Task DeleteTopicAsync(Int64 tokenId, int topicId)
		{
			var call = new ApiCall("deleteTopic");
			call.Parameters.Add("tokenId", tokenId.ToString());
			call.Parameters.Add("topicId", topicId.ToString());
			var result = await makeApiCallAsync<Topic>(call);
			if (result is ApiError)
				throw new Exception((result as ApiError).Value);
		}

		// ----------- POSTS -----------------

		public async Task<List<Post>> GetPostsAsync(Int64 tokenId, int parentTopic)
		{
			var call = new ApiCall("getPosts");
			call.Parameters.Add("tokenId", tokenId.ToString());
			call.Parameters.Add("parentTopicId", parentTopic.ToString());
			var result = await makeApiCallAsync<List<Post>>(call);
			return CheckRet<List<Post>>(result);
		}

		/* 
			getPostPage(tokenId, parentTopicId, page, pageSize) -> TokenResult
				page = -1 means last page
		 */

		 public async Task<PostResult> GetPostPageAsync(Int64 tokenId, int parentTopicId, int page, int pageSize)
		 {
			var call = new ApiCall("getPostPage");
			call.Parameters.Add("tokenId", tokenId.ToString());
			call.Parameters.Add("parentTopicId", parentTopicId.ToString());
			call.Parameters.Add("page", page.ToString());
			call.Parameters.Add("pageSize", pageSize.ToString());
			var result = await makeApiCallAsync<PostResult>(call);
			return CheckRet<PostResult>(result);
		 }

		 public async Task<Post> GetPostAsync(Int64 tokenId, int postId)
		 {
			var call = new ApiCall("getPost");
			call.Parameters.Add("tokenId", tokenId.ToString());
			call.Parameters.Add("postId", postId.ToString());
			var result = await makeApiCallAsync<Post>(call);
			return CheckRet<Post>(result);
		 }

		 public async Task<Post> UpdatePostAsync(Int64 tokenId, Post post)
		 {
			var call = new ApiCall("updatePost");
			call.Parameters.Add("tokenId", tokenId.ToString());
			call.Parameters.Add("Post", JsonConvert.SerializeObject(post));
			var result = await makeApiCallAsync<Post>(call);
			return CheckRet<Post>(result);
		 }

		 public async Task<Post> CreatePostAsync(Int64 tokenId, Post post)
		 {
			var call = new ApiCall("createPost");
			call.Parameters.Add("tokenId", tokenId.ToString());
			call.Parameters.Add("Post", JsonConvert.SerializeObject(post));
			var result = await makeApiCallAsync<Post>(call);
			return CheckRet<Post>(result);
		 }
    }
}