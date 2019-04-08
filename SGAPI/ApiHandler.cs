using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGDataModel;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace SGAPI
{
    public class ApiHandler
    {
		private SGContext _context;

        public ApiHandler(SGContext context)
		{
			_context = context;
		}

		public async Task<ApiResult> processCallAsync(ApiCall apiCall)
		{
			try
			{
				if (apiCall == null)
					throw new Exception("Invalid API call.");
				if (string.IsNullOrEmpty(apiCall.Func))
					throw new Exception("Invalid API call.  Function name null or empty.");

				var func = apiCall.Func.Trim();			
				switch(func)
				{
					case "getDbInfo": return await getDBInfo(apiCall); 
					case "login": return await login(apiCall);
					case "logout": return await logout(apiCall);
					case "getToken": return await getToken(apiCall);

					case "createUser": return await createUser(apiCall);
					case "getUser": return await getUser(apiCall);
					case "getUsers": return await getUsers(apiCall);
					case "updateUser": return await updateUser(apiCall);
					case "updatePassword": return await updatePassword(apiCall);
					case "activateUser": return await activateUser(apiCall);
					case "deactivateUser": return await deactivateUser(apiCall);

					case "getTopic": return await getTopic(apiCall);
					case "getTopics": return await getTopics(apiCall);
					case "getTopicPage": return await getTopicPage(apiCall);
					case "createTopic": return await createTopic(apiCall);
					case "updateTopic": return await updateTopic(apiCall);
					case "deleteTopic": return await deleteTopic(apiCall);

					case "getPosts": return await getPosts(apiCall);
					case "getPostPage": return await getPostPage(apiCall);
					case "getPost": return await getPost(apiCall);
					case "createPost": return await createPost(apiCall);
					case "updatePost": return await updatePost(apiCall);
					case "deletePost": return await deletePost(apiCall);
					case "hidePost": return await hidePost(apiCall);
					case "unhidePost": return await unhidePost(apiCall);
				}

				throw new Exception($"Unknown API function {func}");
			}
			catch(Exception ex)
			{
				var ret = new ApiError(ex.Message);
				return ret;
			}
		}

		public ApiResult processCall(ApiCall apiCall)
		{
			var task = processCallAsync(apiCall);
			task.Wait();
			return task.Result;
		}

		private void Log(string message)
		{
			var now = DateTime.UtcNow;
			var stamp = $"{now.Hour.ToString().PadLeft(2,'0')}:{now.Minute.ToString().PadLeft(2,'0')}";
			System.Console.WriteLine($"SG-LOG(stamp): {message}");
		}

		private void Log(Exception ex)
		{
			Log($"EXCEPTION - {ex.Message}");
		}

		/* login(email, password) -> LoginToken */
		private async Task<ApiResult> login(ApiCall apiCall)
		{
			var email = apiCall.Parameters.OrNull("email");
			var password = apiCall.Parameters.OrNull("password");
			if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
			{
				return new ApiError("Invalid credentials");
			}
			var token = await LoginTokenTasks.LoginAsync(_context, email, password);
			if (token == null)
			{
				return new ApiError("Authentication failed.");
			}
			return new ApiResult<LoginToken>(StdResult.OK, token);
		}

		/* logout(tokenId) -> Success */
		/* logout(adminTokenId, email) -> Success */
		private async Task<ApiResult> logout(ApiCall apiCall)
		{
			if (apiCall.Parameters.ContainsKey("email"))
			{
				Int64 tokenId = -1;
				string email = apiCall.Parameters.OrNull("email");
				if (string.IsNullOrWhiteSpace(email))
					return new ApiError("Invalid email");
				if (Int64.TryParse( apiCall.Parameters.OrNull("adminTokenId"), out tokenId))
				{
					var token = await LoginTokenTasks.GetLoginTokenAsync(_context, tokenId);
					if (token != null)
					{
						await LoginTokenTasks.LogoutAsync(_context, token);
						return new ApiResult(StdResult.OK);
					}
				}
			}
			else
			{
				Int64 tokenId = 0;
				if (Int64.TryParse( apiCall.Parameters.OrNull("tokenId"), out tokenId))
				{
					var token = await LoginTokenTasks.GetLoginTokenAsync(_context, tokenId);
					if (token != null)
					{
						await LoginTokenTasks.LogoutAsync(_context, token);
						return new ApiResult(StdResult.OK);
					}
				}
			}
			return new ApiError("Invalid tokenId");
		}

		/* getToken(tokenId) -> LoginToken */
		private async Task<ApiResult> getToken(ApiCall apiCall)
		{
			if (!apiCall.Parameters.ContainsKey("tokenId"))
				throw new Exception("getToken missing parameter tokenId");

			Int64 tokenId;
			if (Int64.TryParse(apiCall.Parameters.OrNull("tokenId"), out tokenId))
			{
				LoginToken token;
				if (tokenId == LoginToken.AnonymousLoginId)
					token = LoginTokenTasks.GetAnonmymousToken();
				else
					token = await LoginTokenTasks.GetLoginTokenAsync(_context, tokenId);

				if (token == null)
					return new ApiError("No such token.");

				var result = new ApiResult<LoginToken>(StdResult.OK, token);
				return result;
			}
			return new ApiError("Invalid token id.");
		}

		/* getDbInfo() -> DatabaseInfo */
		private async Task<ApiResult> getDBInfo(ApiCall apiCall)
		{
			var info = await _context.databaseInfo.FirstAsync();
			if (info != null)
				return new ApiResult<DatabaseInfo>(StdResult.OK, info);
			return new ApiError("Failed to retrieve the database info object.");
		}

		private async Task<LoginToken> quickGetToken(ApiCall apiCall, bool allowAnon = false)
		{
			Int64 tokenId;
			if (!Int64.TryParse(apiCall.Parameters.OrNull("tokenId"), out tokenId))
				return null;
			if (tokenId == 0 && allowAnon)
				return LoginTokenTasks.GetAnonmymousToken();
			var token = await LoginTokenTasks.GetLoginTokenAsync(_context, tokenId);
			if (token.User.Active)
				return token;
			return null;
		}
		/* createUser(tokenId, email, displayName, password, role) -> User */
		private async Task<ApiResult> createUser(ApiCall apiCall)
		{
			var token = await quickGetToken(apiCall);
			if (token == null)
				return new ApiError("Invalid token");
			var email = apiCall.Parameters.OrNull("email");
			if (string.IsNullOrWhiteSpace(email))
				return new ApiError("Invalid parameter: email");
			var displayname = apiCall.Parameters.OrNull("displayName") ?? "";
			var password = apiCall.Parameters["password"];
			if (string.IsNullOrWhiteSpace(password))
				return new ApiError("Invalid parameter: password");
			int roleRaw = 0;
			if (!int.TryParse(apiCall.Parameters.OrNull("role"), out roleRaw) ||
				!UserRole.RoleIsValid(roleRaw))
				return new ApiError("Invalid role");
			var user = await UserTasks.CreateUserAsync(_context, token, email, displayname, password, roleRaw);
			if (user == null)
				return new ApiError("Create user failed.");
			return new ApiResult<User>(StdResult.OK, user);
		}

		/* getUser(tokenId, userId) */
		private async Task<ApiResult> getUser(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return new ApiError("Invalid token");

				int userId = 0;
				if (!int.TryParse(apiCall.Parameters.OrNull("userId"), out userId))
					return new ApiError("Invalid Parameter: userId"); 

				UserRole role = new UserRole( token.User.RawRole );
				if (!(role.IsAdmin || token.UserId == userId))
					return new ApiError("Unauthorized"); 

				var user = await (from u in _context.users where u.Id == userId select u).FirstOrDefaultAsync();
				if (user == null)
					return new ApiError("Not found");
				
				user = user.CloneForExport();
				return new ApiResult<User>(StdResult.OK, user);
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("Internal Server Error");
			}
		}

		/* getUsers(tokenId) */
		private async Task<ApiResult> getUsers(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return new ApiError("Invalid token");
				var role = new UserRole(token.User.RawRole);
				if (!role.IsAdmin)
					return new ApiError("Unauthorised");
				var users = await (from u in _context.users select u).ToListAsync();
				for(int i = 0; i < users.Count; ++i)
				{
					users[i] = users[i].CloneForExport();
				}
				return new ApiResult<List<User>>(StdResult.OK, users);
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("Internal Server Error");
			}
		}

		/* updateUser(tokenId, User) -> Success */
		private async Task<ApiResult> updateUser(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return new ApiError("Invalid token");
				var userJson = apiCall.Parameters.OrNull("User");
				if (string.IsNullOrWhiteSpace(userJson))
					return new ApiError("userJson parameter missing or empty");
				User user;
				try
				{
					user = JsonConvert.DeserializeObject<User>(userJson);
				}
				catch
				{
					return new ApiError("Invalid User object");
				}
				await UserTasks.UpdateUserAsync(_context, token, user);
				return new ApiResult(StdResult.OK);
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("Internal Server Error");
			}
		}

		/* updatePassword(email, oldPassword, newPassword) -> Success */
		private async Task<ApiResult> updatePassword(ApiCall apiCall)
		{
			try
			{
				var email = apiCall.Parameters.OrNull("email");
				if (string.IsNullOrWhiteSpace(email))
					return new ApiError("Invalid parameter: email");
				var oldPassword = apiCall.Parameters.OrNull("oldPassword");
				if (string.IsNullOrWhiteSpace(oldPassword))
					return new ApiError("Invalid parameter: old password");
				var newPassword = apiCall.Parameters.OrNull("newPassword");
				if (string.IsNullOrWhiteSpace(newPassword))
					return new ApiError("Invalid parameter: new password");

				await UserTasks.UpdatePasswordAsync(_context, email, oldPassword, newPassword);

				return new ApiResult(StdResult.OK);
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("Password update failed.");
			}
		}

		private async Task<ApiResult> activateUser_Impl(ApiCall apiCall, bool active)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return new ApiError("Invalid token");
				var email = apiCall.Parameters.OrNull("email");
				if (string.IsNullOrWhiteSpace(email))
					return new ApiError("Invalid parameter: email");

				await UserTasks.SetUserActiveAsync(_context, token, email, active);
				return new ApiError(StdResult.OK);
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("Password update failed.");
			}
		}

		/* activateUser(tokenId, email) */
		private async Task<ApiResult> activateUser(ApiCall apiCall)
		{
			return await activateUser_Impl(apiCall, true);
		}

		/* deactivateUser(tokenId, email) */
		private async Task<ApiResult> deactivateUser(ApiCall apiCall)
		{
			return await activateUser_Impl(apiCall, false);
		}

		/* getTopics(tokenId, topicId) -> Topic */
		private async Task<ApiResult> getTopic(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall, true);
				if (token == null)
					return new ApiError("Invalid token");
				var topicId = apiCall.Parameters.IntOrNull("topicId");
				if (topicId == null)
					return new ApiError("Invalid parameter: topicId");

				Topic topic = await (from t in _context.topics
					where t.Id == topicId.Value
					select t).FirstOrDefaultAsync();

				if (topic != null)
				{
					int tokenRole = token.User.RawRole;
					if (topic.RoleToRead > tokenRole)
						return new ApiError("Unauthorised");

					return new ApiResult<Topic>(StdResult.OK, topic.CloneForExport());
				}

				return new ApiError("Not found");
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("getTopics failed.");
			}
		}

		/* getTopics(tokenId) */
		/* getTopics(tokenId, parentTopicId) */
		private async Task<ApiResult> getTopics(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall, true);
				if (token == null)
					return new ApiError("Invalid token");
				List<Topic> topics;
				int tokenRole = token.User.RawRole;
				var parentId = apiCall.Parameters.IntOrNull("parentTopicId");
				if (parentId != null)
				{
					topics = await (from t in _context.topics 
						where t.ParentId == parentId && t.IsRootEntry == false && t.RoleToRead <= tokenRole 
						select t).ToListAsync();
				}
				else
				{
					topics = await (from t in _context.topics 
						where t.ParentId == null && t.IsRootEntry == true && t.RoleToRead <= tokenRole 
						select t).ToListAsync();
				}
				TopicResult ret = new TopicResult();

				for(int i = 0; i < topics.Count; ++i)
				{
					ret.Topics.Add( topics[i].CloneForExport() );
				}

				ret.TopicCount = ret.Topics.Count;

				if (parentId != null)
					ret.ParentList = await TopicTasks.CreateParentList(_context, parentId.Value);
				return new ApiResult<TopicResult>(StdResult.OK, ret);
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("getTopics failed.");
			}
		}

		/* 
			getTopicPage(tokenId, parentTopicId, page, pageSize) -> TokenResult
			getTopicPage(tokenId, parentTopicId, page, pageSize) -> TokenResult
			Note: page -1 defaults to 0, order is descending...
		 */
		private async Task<ApiResult> getTopicPage(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall, true);
				if (token == null)
					return new ApiError("Invalid token");

				var parentId = apiCall.Parameters.IntOrNull("parentTopicId");
				var page = apiCall.Parameters.IntOrNull("page");
				var pageSize = apiCall.Parameters.IntOrNull("pageSize");
				if (page == null)
					return new ApiError("Invalid parameter: page");
				if (pageSize == null)
					return new ApiError("Invalid parameter: pageSize");

				if (page.Value == -1)
					page = 0;

				int count;
				if (parentId == null)
					count = await (from t in _context.topics 
						where t.ParentId == null && t.RoleToRead <= token.User.RawRole 
						select t).CountAsync();
				else
					count = await (from t in _context.topics
						where t.ParentId == parentId.Value && t.RoleToRead <= token.User.RawRole
						select t).CountAsync();
				var pageCount = count / pageSize.Value;
				if (count % pageSize != 0)
					++pageCount;
				if (page.Value >= pageCount)
					page = Math.Max(0, pageCount - 1);

				int begin = (page.Value) * pageSize.Value;

				IQueryable<Topic> query;
				if (parentId == null)
					query = (from t in _context.topics
						where t.ParentId == null && t.RoleToRead <= token.User.RawRole
						orderby t.Modified descending
						select t);
				else
					query = (from t in _context.topics
						where t.ParentId == parentId.Value && t.RoleToRead <= token.User.RawRole
						orderby t.Modified descending
						select t);
				var topics = await query.Skip(begin).Take(pageSize.Value).ToListAsync();

				var topicData = new TopicResult();
				if (parentId != null)
					topicData.ParentList = await TopicTasks.CreateParentList(_context, parentId.Value);
				foreach(var topic in topics)
				{
					topicData.Topics.Add(topic.CloneForExport());
				}
				topicData.TopicCount = count;
				topicData.CurrentPage = page.Value;
				topicData.PageCount = pageCount;
				return new ApiResult<TopicResult>(StdResult.OK, topicData);
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("getTopics failed.");
			}
		}

		/* createTopic(loginId, title, roleToEdit, roleToRead) */
		/* createTopic(loginId, parentTopicId, title, roleToEdit, roleToRead) */
		private async Task<ApiResult> createTopic(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return new ApiError("Invalid token.");
				var parentTopicId = apiCall.Parameters.IntOrNull("parentTopicId");
				var title = apiCall.Parameters.OrNull("title");
				var roleToEdit = apiCall.Parameters.IntOrNull("roleToEdit");
				var roleToRead = apiCall.Parameters.IntOrNull("roleToRead");
				if (title == null)
					return new ApiError("Invalid parameter: title.");
				if (roleToEdit == null)
					return new ApiError("Invalid parameter: roleToEdit.");
				if (roleToRead == null)
					return new ApiError("Invalid parameter: roleToRead.");
				var topic = new Topic();
				topic.Title = title;
				topic.RoleToEdit = roleToEdit.Value;
				topic.RoleToRead = roleToRead.Value;
				topic.ParentId = parentTopicId;
				topic.IsRootEntry = parentTopicId == null;
				topic.OwnerId = token.UserId;
				topic.Modified = DateTime.UtcNow;
				_context.topics.Add(topic);
				await _context.SaveChangesAsync();

				return new ApiResult<Topic>(StdResult.OK, topic.CloneForExport());
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("createTopic failed.");
			}
		}

		/* updateTopic(tokenId, Topic) */
		private async Task<ApiResult> updateTopic(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return new ApiError("Invalid token.");
				var topic = apiCall.Parameters.FromJson<Topic>("Topic");

				var originalTopic = await (from t in _context.topics
					where t.Id == topic.Id
					select t).FirstOrDefaultAsync();
				if (originalTopic == null)
					return new ApiError("Topic not found");
				var role = new UserRole(token.User.RawRole);
				var canUpdate = (role.IsAdmin || (originalTopic.OwnerId != null && originalTopic.OwnerId.Value == token.UserId));
				if (!canUpdate)
					return new ApiError("Unauthorised");
				originalTopic.Title = topic.Title;
				originalTopic.RoleToEdit = topic.RoleToEdit;
				originalTopic.RoleToRead = topic.RoleToRead;
				_context.topics.Update(originalTopic);
				await _context.SaveChangesAsync();
				return new ApiResult(StdResult.OK);
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("updateTopic failed.");
			}
		}

		private async Task<ApiResult> deleteTopic(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return new ApiError("Invalid token.");

				var topicId = apiCall.Parameters.IntOrNull("topicId");
				if (topicId == null)
					return new ApiError("Invalid parameter: Topic");

				var userRole = new UserRole(token.User.RawRole);
				if (!userRole.IsAdmin)
					return new ApiError("Unauthorised");

				var topic = await (from t in _context.topics
					where t.Id == topicId
					select t).FirstOrDefaultAsync();
				
				if (topic == null)
					return new ApiError("Not Found");
				topic = null;

				/* We use this approach because EF Core doesn't support self-referential cascading. */

				try
				{
					// Delete the topic itself
					await _context.Database.BeginTransactionAsync();
					string sql = "delete from topics where Id = @p0";
					int rows = await _context.Database.ExecuteSqlCommandAsync(sql, new object[] { topicId });

					// Delete all its children for whom ParentId has now become null, recursively
					sql = "delete from topics where ParentId is NULL and IsRootEntry = 0";
					do
					{
						rows = await _context.Database.ExecuteSqlCommandAsync(sql, new object[] { topicId });
					} while(rows > 0);
					
					// Delete all the posts that have been orphaned as a result of the above.
					sql = "delete from posts where ParentId is NULL";
					do
					{
						rows = await _context.Database.ExecuteSqlCommandAsync(sql, new object[] { topicId });
					} while(rows > 0);
					_context.Database.CommitTransaction();
				}
				catch(Exception ex)
				{
					Log(ex.Message);
					_context.Database.RollbackTransaction();
				}
				return new ApiResult(StdResult.OK);
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("deleteTopic failed.");
			}
		}

		private Post createGenericStandinPost(int postId, string message)
		{
			var post = new Post();
			post.Title = message;
			post.Body = message;
			post.UserId = null;
			post.RoleToRead = UserRole.Guest;
			post.RoleToEdit = UserRole.Admin;
			post.UserDisplayName_NotMapped = "Restricted";
			post.Id = postId;
			return post;
		}
		private Post createGenericHiddenPost(int postId)
		{
			return createGenericStandinPost(postId, "This post has been hidden");
		}

		private Post createGenericRestrictedPost(int postId)
		{
			return createGenericStandinPost(postId, "You don't have the privileges to required to view this post.");
		}

		/* getPosts(tokenId, parentTopicId) */
		private async Task<ApiResult> getPosts(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall, true);
				if (token == null)
					token = LoginTokenTasks.GetAnonmymousToken();
				int tokenRole = token.User.RawRole;
				var topicId = apiCall.Parameters.IntOrNull("parentTopicId");
				if (topicId == null)
					return new ApiError("Invalid parameter: parentTopicId");
				var posts = await (from p in _context.posts.Include(p => p.User)
					where p.ParentId == topicId.Value
					orderby p.Id descending
					select p).Include(p => p.User).ToListAsync();

				var role = new UserRole(tokenRole);
				var ret = new List<Post>();
				for (int i = 0; i < posts.Count; ++i)
				{
					var post = posts[i];
					if (post.Hidden && !role.IsAdmin)
						ret.Add(createGenericHiddenPost(post.Id));
					else if (post.RoleToRead > token.User.RawRole || post.User.Active == false)
						ret.Add(createGenericRestrictedPost(post.Id));
					else
						ret.Add(post.CloneForExport());
				}

				return new ApiResult<List<Post>>(StdResult.OK, ret);
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("getPosts failed.");
			}
		}

		private void CreatePostExportList(int tokenRole, IEnumerable<Post> posts, List<Post> output)
		{
			var role = new UserRole(tokenRole);
			foreach (var p in posts)
			{
				if (p.Hidden && !role.IsAdmin)
					output.Add(createGenericHiddenPost(p.Id));
				else if (p.RoleToRead > tokenRole || p.User.Active == false)
					output.Add(createGenericRestrictedPost(p.Id));
				else
					output.Add(p.CloneForExport());
			}
		}

		/* 
			getPostPage(tokenId, parentTopicId, page, pageSize) -> TokenResult
				page = -1 means last page
		 */
		private async Task<ApiResult> getPostPage(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall, true);
				if (token == null)
					token = LoginTokenTasks.GetAnonmymousToken();
				int tokenRole = token.User.RawRole;
				var topicId = apiCall.Parameters.IntOrNull("parentTopicId");
				if (topicId == null)
					return new ApiError("Invalid parameter: parentTopicId");

				var parentId = apiCall.Parameters.IntOrNull("parentTopicId");
				if (parentId == null)
					return new ApiError("Invalid parameter: parentId");
				var page = apiCall.Parameters.IntOrNull("page");
				var pageSize = apiCall.Parameters.IntOrNull("pageSize");
				if (page == null)
					return new ApiError("Invalid parameter: page");
				if (pageSize == null)
					return new ApiError("Invalid parameter: pageSize");

				int count = await (from p in _context.posts
					where p.ParentId == parentId.Value
					select p).CountAsync();
				var pageCount = count / pageSize.Value;
				if (count % pageSize.Value != 0)
					++pageCount;

				if (page.Value == -1 || page.Value >= pageCount)
					page = Math.Max(0, pageCount - 1);

				int begin = (page.Value) * pageSize.Value;

				IQueryable<Post> query = (from p in _context.posts
					where p.ParentId == parentId.Value
					orderby p.Created ascending
					select p);
				
				var posts = await query.Include(p => p.User).Skip(begin).Take(pageSize.Value).ToListAsync();

				var postsResult = new PostResult();
				postsResult.Count = count;
				postsResult.CurrentPage = page.Value;

				CreatePostExportList(tokenRole, posts, postsResult.Posts);

				return new ApiResult<PostResult>(StdResult.OK, postsResult);
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("getPosts failed.");
			}
		}

		/* createPost(tokenId, Post) -> Success */
		private async Task<ApiResult> createPost(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return new ApiError("Invalid token.");
				var postJson = apiCall.Parameters.OrNull("Post");
				if (string.IsNullOrWhiteSpace(postJson))
					return new ApiError("Invalid parameter: Post.");
				var post = JsonConvert.DeserializeObject<Post>(postJson);
				post.Id = 0;
				if (post.UserId != token.UserId)
					return new ApiError("Unauthorised.  The logged in user can't create posts in another user's name.");
				var topicId = post.ParentId;
				if (topicId == null)
					return new ApiError("Invalid parameter: Post.ParentId may not be null.");
				var topic = await (from t in _context.topics
					where t.Id == topicId.Value
					select t).FirstOrDefaultAsync();
				if(token.User.RawRole < topic.RoleToEdit)
					return new ApiError("Unauthorised.");
				var now = DateTime.UtcNow;
				post.Created = now;
				post.Modified = now;
				_context.posts.Add(post);
				await _context.SaveChangesAsync();
				return new ApiResult<Post>(StdResult.OK, post.CloneForExport());
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("createPost failed.");
			}
		}

		private delegate void ChangePostDelegate(Post post, bool isAdmin);
		private async Task<ApiResult> ChangePostAsync(ApiCall apiCall, ChangePostDelegate callback)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return new ApiError("Invalid token.");
				var role = new UserRole( token.User.RawRole );
				var postId = apiCall.Parameters.IntOrNull("postId");
				if (postId == null)
					return new ApiError("Invalid Parameter: postId.");
				var post = await (from p in _context.posts
					where p.Id == postId.Value
					select p).FirstOrDefaultAsync();
				if (post == null)
					return new ApiError("Invalid Parameter: Post not found.");
				bool mayChange = (post.UserId != null && post.UserId == token.UserId) ||
					role.IsAdmin;
				if (!mayChange)
					return new ApiError("Unathorised.");

				try
				{
					callback(post, role.IsAdmin);
				}
				catch(Exception ex)
				{
					return new ApiError(ex.Message);
				}
				post.Modified = DateTime.Now;
				_context.Update(post);
				await _context.SaveChangesAsync();

				return new ApiResult<Post>(StdResult.OK, post.CloneForExport());
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("deletePost failed.");
			}
		}

		/* getPost(tokenId, Post) -> Success */
		private async Task<ApiResult> getPost(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return new ApiError("Invalid token.");
				var tokenRole = token.User.RawRole;

				var postId = apiCall.Parameters.IntOrNull("postId");
				if (postId == null)
					return new ApiError("Invalid parameter: postId");

				var post = await (from p in _context.posts
					where p.Id == postId.Value
					select p).FirstOrDefaultAsync();
				
				if (tokenRole < post.RoleToRead)
					return new ApiError("Authorisation failed.");

				if (post == null)
					return new ApiError("Not found");
				
				return new ApiResult<Post>(StdResult.OK, post.CloneForExport());
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("getPost failed.");
			}
		}

		/* updatePost(tokenId, Post) -> Success */
		private async Task<ApiResult> updatePost(ApiCall apiCall)
		{
			try
			{
				var post = apiCall.Parameters.FromJson<Post>("Post");
				if (post == null)
					return new ApiError("Invalid parameter: Post");
				apiCall.Parameters.Add("postId", post.Id.ToString());
				return await ChangePostAsync(apiCall, 
					(originalPost, isAdmin) =>
					{
						if ((originalPost.RoleToEdit != post.RoleToEdit ||
							originalPost.RoleToRead != post.RoleToRead) &&
							!isAdmin)
						{
							throw new Exception("Unauthorised");
						}
						else
						{
							originalPost.RoleToEdit = post.RoleToEdit;
							originalPost.RoleToEdit = post.RoleToRead;
						}
						originalPost.Title = post.Title;
						originalPost.Body = post.Body;
						originalPost.Modified = DateTime.UtcNow;
					});
			}
			catch(Exception ex)
			{
				Log(ex);
				return new ApiError("updatePost failed.");
			}
		}

		/* deletePost(tokenId, postId) -> Success */
		private async Task<ApiResult> deletePost(ApiCall apiCall)
		{
			return await ChangePostAsync(apiCall, (post, isAdmin) => 
			{
				post.Title = "This post has been deleted";
				post.Body = "This post has beed deleted";
				post.UserId = null;
				post.RoleToEdit = UserRole.Admin;
			});
		}

		/* hidePost(tokenId, postId) -> Success */
		private async Task<ApiResult> hidePost(ApiCall apiCall)
		{
			return await ChangePostAsync(apiCall, (post, isAdmin) => 
			{
				if (!isAdmin)
					throw new Exception("Unauthorised");
				post.Hidden = true;
			});
		}

		/* unhidePost(tokenId, postId) -> Success */
		private async Task<ApiResult> unhidePost(ApiCall apiCall)
		{
			return await ChangePostAsync(apiCall, (post, isAdmin) => 
			{
				if (!isAdmin)
					throw new Exception("Unauthorised");
				post.Hidden = false;
			});
		}
    }

	public static class DictionaryExtension
	{
		public static string OrNull(this Dictionary<string, string> dict, string key)
		{
			string ret;
			if (dict.TryGetValue(key, out ret))
				return ret;
			return null;
		}

		public static int? IntOrNull(this Dictionary<string, string> dict, string key)
		{
			string s;
			if (dict.TryGetValue(key, out s))
			{
				int i;
				if(int.TryParse(s, out i))
				{
					return i;
				}
			}
			return null;
		}

		public static T FromJson<T>(this Dictionary<string, string> dict, string key) where T : class
		{
			try
			{
				string json;
				if (dict.TryGetValue(key, out json))
					return JsonConvert.DeserializeObject<T>( json );
				return null;
			}
			catch
			{
				return null;
			}
		}
	}
}