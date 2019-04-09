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
				var ret = ApiError.ServerError(ex.Message);
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
		/*
			Requirements:
				Token: No
				User Role: Any
		 */
		private async Task<ApiResult> login(ApiCall apiCall)
		{
			var email = apiCall.Parameters.OrNull("email");
			var password = apiCall.Parameters.OrNull("password");
			if (string.IsNullOrWhiteSpace(email))
				return ApiError.InvalidParam("email");
			if (string.IsNullOrWhiteSpace(password))
				return ApiError.InvalidParam("password");
			var token = await LoginTokenTasks.LoginAsync(_context, email, password);
			if (token == null)
			{
				return ApiError.AuthenticationFailure();
			}
			return new ApiResult<LoginToken>(StdResult.OK, token);
		}

		/* logout(tokenId) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: User | Administrator
		 */
		/* logout(adminTokenId, email) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator
		 */
		private async Task<ApiResult> logout(ApiCall apiCall)
		{
			try
			{
				if (apiCall.Parameters.ContainsKey("email"))
				{
					Int64 adminTokenId = -1;
					string email = apiCall.Parameters.OrNull("email");
					if (string.IsNullOrWhiteSpace(email))
						return ApiError.InvalidParam("email");
					if (Int64.TryParse( apiCall.Parameters.OrNull("adminTokenId"), out adminTokenId))
					{
						var token = await LoginTokenTasks.GetLoginTokenAsync(_context, adminTokenId);
						var userRole = new UserRole(token.User.RawRole);
						if (!userRole.IsAdmin)
						{
							return ApiError.Unauthorised();
						}
						if (token != null)
						{
							await LoginTokenTasks.LogoutAsync(_context, token, email);
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
				return ApiError.InvalidToken();
			}
			catch(Exception ex)
			{
				Log(ex);
				return ApiError.ServerError(ex.Message);
			}
		}

		/* getToken(tokenId) -> LoginToken */
		/*
			Requirements:
				Token: No (only ID)
				User Role: Guest | User | Administrator
		 */
		private async Task<ApiResult> getToken(ApiCall apiCall)
		{
			try
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
						return ApiError.InvalidToken();

					var result = new ApiResult<LoginToken>(StdResult.OK, token);
					return result;
				}
				return ApiError.InvalidToken();
			}
			catch(Exception ex)
			{
				Log(ex);
				return ApiError.ServerError(ex.Message);
			}
		}

		/* getDbInfo() -> DatabaseInfo */
		/*
			Requirements:
				Token: No
				User Role: Any
		 */
		private async Task<ApiResult> getDBInfo(ApiCall apiCall)
		{
			try
			{
				var info = await _context.databaseInfo.FirstAsync();
				if (info != null)
					return new ApiResult<DatabaseInfo>(StdResult.OK, info);
				return ApiError.NotFound();
			}
			catch(Exception ex)
			{
				Log(ex);
				return ApiError.ServerError(ex.Message);
			}
		}

		private async Task<LoginToken> quickGetToken(ApiCall apiCall, bool allowAnon = false)
		{
			Int64 tokenId;
			bool parsed = Int64.TryParse(apiCall.Parameters.OrNull("tokenId"), out tokenId);
			if (!parsed)
			{
				if (allowAnon)
					tokenId = 0;
				else
					return null;
			}
			if (tokenId == 0 && allowAnon)
				return LoginTokenTasks.GetAnonmymousToken();
			var token = await LoginTokenTasks.GetLoginTokenAsync(_context, tokenId);
			if (token.User.Active)
				return token;
			return null;
		}

		/* createUser(tokenId, email, displayName, password, role) -> User */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator
		 */
		private async Task<ApiResult> createUser(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return ApiError.InvalidToken();
				var userRole = new UserRole(token.User.RawRole);
				if (!userRole.IsAdmin)
					return ApiError.Unauthorised();
				var email = apiCall.Parameters.OrNull("email");
				if (string.IsNullOrWhiteSpace(email))
					return ApiError.InvalidParam("email");
				var displayname = apiCall.Parameters.OrNull("displayName") ?? "";
				var password = apiCall.Parameters["password"];
				if (string.IsNullOrWhiteSpace(password))
					return ApiError.InvalidParam("password");
				int roleRaw = 0;
				if (!int.TryParse(apiCall.Parameters.OrNull("role"), out roleRaw) ||
					!UserRole.RoleIsValid(roleRaw))
					return ApiError.InvalidParam("role");
				var user = await UserTasks.CreateUserAsync(_context, token, email, displayname, password, roleRaw);
				if (user == null)
					return new ApiError("Create user failed unexpectedly.");
				return new ApiResult<User>(StdResult.OK, user);
			}
			catch(Exception ex)
			{
				Log(ex);
				return ApiError.ServerError(ex.Message);
			}
		}

		/* getUser(tokenId, userId) */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator if userId != token.User.Id | User
		 */
		private async Task<ApiResult> getUser(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return ApiError.InvalidToken();

				int userId = 0;
				if (!int.TryParse(apiCall.Parameters.OrNull("userId"), out userId))
					return ApiError.InvalidParam("userId"); 

				UserRole role = new UserRole( token.User.RawRole );
				if (!(role.IsAdmin || token.UserId == userId))
					return ApiError.Unauthorised(); 

				var user = await (from u in _context.users where u.Id == userId select u).FirstOrDefaultAsync();
				if (user == null)
					return ApiError.NotFound();
				
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
		/*
			Requirements:
				Token: Yes
				User Role: Administrator
		 */
		private async Task<ApiResult> getUsers(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return ApiError.InvalidToken();
				var role = new UserRole(token.User.RawRole);
				if (!role.IsAdmin)
					return ApiError.Unauthorised();
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
				return ApiError.ServerError(ex.Message);
			}
		}

		/* updateUser(tokenId, User) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator if userId != token.User.Id | User
		 */
		private async Task<ApiResult> updateUser(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return ApiError.InvalidToken();
				var userJson = apiCall.Parameters.OrNull("User");
				if (string.IsNullOrWhiteSpace(userJson))
					return ApiError.InvalidParam("User");
				User user;
				try
				{
					user = JsonConvert.DeserializeObject<User>(userJson);
				}
				catch
				{
					return ApiError.InvalidParam("User");
				}
				if (token.UserId != user.Id)
				{
					var userRole = new UserRole(token.User.RawRole);
					if (!userRole.IsAdmin)
						return ApiError.Unauthorised();
				}
				await UserTasks.UpdateUserAsync(_context, token, user);
				return new ApiResult(StdResult.OK);
			}
			catch(Exception ex)
			{
				Log(ex);
				return ApiError.ServerError(ex.Message);
			}
		}

		/* updatePassword(email, oldPassword, newPassword) -> Success */
		/*
			Requirements:
				Token: No
				User Role: Any
				Password: Yes
		 */
		private async Task<ApiResult> updatePassword(ApiCall apiCall)
		{
			try
			{
				var email = apiCall.Parameters.OrNull("email");
				if (string.IsNullOrWhiteSpace(email))
					return ApiError.InvalidParam("email");
				var oldPassword = apiCall.Parameters.OrNull("oldPassword");
				if (string.IsNullOrWhiteSpace(oldPassword))
					return ApiError.InvalidParam("oldPassword");
				var newPassword = apiCall.Parameters.OrNull("newPassword");
				if (string.IsNullOrWhiteSpace(newPassword))
					return ApiError.InvalidParam("newPassword");

				await UserTasks.UpdatePasswordAsync(_context, email, oldPassword, newPassword);

				return new ApiResult(StdResult.OK);
			}
			catch(Exception ex)
			{
				Log(ex);
				return ApiError.ServerError(ex.Message);
			}
		}

		private async Task<ApiResult> activateUser_Impl(ApiCall apiCall, bool active)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return ApiError.InvalidToken();
				var userRole = new UserRole(token.User.RawRole);
				if (!userRole.IsAdmin)
					return ApiError.Unauthorised();
				var email = apiCall.Parameters.OrNull("email");
				if (string.IsNullOrWhiteSpace(email))
					return ApiError.InvalidParam("email");

				await UserTasks.SetUserActiveAsync(_context, token, email, active);
				return new ApiError(StdResult.OK);
			}
			catch(Exception ex)
			{
				Log(ex);
				return ApiError.ServerError(ex.Message);
			}
		}

		/* activateUser(tokenId, email) */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator
		 */
		private async Task<ApiResult> activateUser(ApiCall apiCall)
		{
			return await activateUser_Impl(apiCall, true);
		}

		/* deactivateUser(tokenId, email) */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator
		 */
		private async Task<ApiResult> deactivateUser(ApiCall apiCall)
		{
			return await activateUser_Impl(apiCall, false);
		}

		/* getTopic(tokenId, topicId) -> Topic */
		/*
			Requirements:
				Token: Yes
				User Role: >= topic.RoleToRead
		 */
		private async Task<ApiResult> getTopic(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall, true);
				if (token == null)
					return ApiError.InvalidToken();
				var topicId = apiCall.Parameters.IntOrNull("topicId");
				if (topicId == null)
					return ApiError.InvalidParam("topicId");

				Topic topic = await (from t in _context.topics
					where t.Id == topicId.Value
					select t).FirstOrDefaultAsync();

				if (topic != null)
				{
					int tokenRole = token.User.RawRole;
					if (topic.RoleToRead > tokenRole)
						return ApiError.Unauthorised();

					return new ApiResult<Topic>(StdResult.OK, topic.CloneForExport());
				}

				return ApiError.NotFound();
			}
			catch(Exception ex)
			{
				Log(ex);
				return ApiError.ServerError(ex.Message);
			}
		}

		/* getTopics(tokenId) */
		/*
			Requirements:
				Token: Yes
				User Role: >= topic.RoleToRead
		 */
		/* getTopics(tokenId, parentTopicId) */
		/*
			Requirements:
				Token: Yes
				User Role: >= topic.RoleToRead
		 */
		private async Task<ApiResult> getTopics(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall, true);
				if (token == null)
					return ApiError.InvalidToken();
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
				return ApiError.ServerError(ex.Message);
			}
		}
			
		/*	getTopicPage(tokenId, page, pageSize) -> TokenResult */
		/*	getTopicPage(tokenId, parentTopicId, page, pageSize) -> TokenResult */
		/*	Note: page -1 defaults to 0, order is descending... */
		/*
			Requirements:
				Token: No
				User Role: >= topic.RoleToRead
		 */	
		private async Task<ApiResult> getTopicPage(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall, true);
				if (token == null)
					return ApiError.InvalidToken();

				var parentId = apiCall.Parameters.IntOrNull("parentTopicId");
				var page = apiCall.Parameters.IntOrNull("page");
				var pageSize = apiCall.Parameters.IntOrNull("pageSize");
				if (page == null)
					return ApiError.InvalidParam("page");
				if (pageSize == null)
					return ApiError.InvalidParam("pageSize");
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
				return ApiError.ServerError(ex.Message);
			}
		}

		/* createTopic(loginId, title, roleToEdit, roleToRead) */
		/* createTopic(loginId, parentTopicId, title, roleToEdit, roleToRead) */
		/*
			Requirements:
				Token: Yes
				User Role: >= topic.Parent.RoleToEdit | Admin for root topics
		 */	

		private async Task<ApiResult> createTopic(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return ApiError.InvalidToken();

				var parentTopicId = apiCall.Parameters.IntOrNull("parentTopicId");
				var userRole = new UserRole(token.User.RawRole);
				if (parentTopicId == null)  //Only administators can edit the root topic.
				{
					if (!userRole.IsAdmin)
						return ApiError.Unauthorised();
				}
				else
				{
					var parentTopic = await(from t in _context.topics
						where t.Id == parentTopicId.Value
						select t).FirstOrDefaultAsync();
					if (parentTopic == null)
						return ApiError.NotFound();
					if (parentTopic.RoleToEdit > token.User.RawRole)
						return ApiError.Unauthorised();
				}

				var title = apiCall.Parameters.OrNull("title");
				var roleToEdit = apiCall.Parameters.IntOrNull("roleToEdit");
				var roleToRead = apiCall.Parameters.IntOrNull("roleToRead");
				
				if (title == null)
					return ApiError.InvalidParam("title.");
				if (roleToEdit == null)
					return ApiError.InvalidParam("roleToEdit.");
				if (roleToRead == null)
					return ApiError.InvalidParam("roleToRead.");
				if (roleToRead.Value > token.User.RawRole)
					return new ApiError("The topic would be unreadable by its creator.");
				var topic = new Topic();
				topic.Title = title;
				topic.RoleToEdit = roleToEdit.Value;
				topic.RoleToRead = roleToRead.Value;
				topic.ParentId = parentTopicId;
				topic.IsRootEntry = parentTopicId == null;
				topic.OwnerId = token.UserId;
				var now = DateTime.UtcNow;
				topic.Modified = now;
				topic.Created = now;
				_context.topics.Add(topic);
				await _context.SaveChangesAsync();

				return new ApiResult<Topic>(StdResult.OK, topic.CloneForExport());
			}
			catch(Exception ex)
			{
				Log(ex);
				return ApiError.ServerError(ex.Message);
			}
		}

		/* updateTopic(tokenId, Topic) */
		/*
			Requirements:
				Token: Yes
				User Role: If User then topic.OwnerId == token.User.Id | Admin
		 */	
		private async Task<ApiResult> updateTopic(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return ApiError.InvalidToken();
				var topic = apiCall.Parameters.FromJson<Topic>("Topic");

				var originalTopic = await (from t in _context.topics
					where t.Id == topic.Id
					select t).FirstOrDefaultAsync();
				if (originalTopic == null)
					return ApiError.NotFound();
				var role = new UserRole(token.User.RawRole);
				var canUpdate = (role.IsAdmin || (originalTopic.OwnerId != null && originalTopic.OwnerId.Value == token.UserId));
				if (!canUpdate)
					return ApiError.Unauthorised();
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
				return ApiError.ServerError(ex.Message);
			}
		}

		/* deleteTopic(tokenId, topicId) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: Admin
		 */	
		private async Task<ApiResult> deleteTopic(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return ApiError.InvalidToken();

				var topicId = apiCall.Parameters.IntOrNull("topicId");
				if (topicId == null)
					return ApiError.InvalidParam("topicId");

				var userRole = new UserRole(token.User.RawRole);
				if (!userRole.IsAdmin)
					return ApiError.Unauthorised();

				var topic = await (from t in _context.topics
					where t.Id == topicId
					select t).FirstOrDefaultAsync();
				
				if (topic == null)
					return ApiError.NotFound();
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
				return ApiError.ServerError(ex.Message);
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
		/*
			Requirements:
				Token: Optional
				User Role: Any
				Notes: Post where token.User.Id < post.RoleToRead, and hidden posts, are blanked out
		 */	
		private async Task<ApiResult> getPosts(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall, true);
				if (token == null)
					return ApiError.InvalidToken();
				int tokenRole = token.User.RawRole;
				var topicId = apiCall.Parameters.IntOrNull("parentTopicId");
				if (topicId == null)
					return ApiError.InvalidParam("parentTopicId");
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
				return ApiError.ServerError(ex.Message);
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
		/*
			Requirements:
				Token: Optional
				User Role: Any
				Notes: Post where token.User.Id < post.RoleToRead, and hidden posts, are blanked out
		 */	
		private async Task<ApiResult> getPostPage(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall, true);
				if (token == null)
					return ApiError.InvalidToken();
				int tokenRole = token.User.RawRole;

				var parentId = apiCall.Parameters.IntOrNull("parentTopicId");
				if (parentId == null)
					return ApiError.InvalidParam("parentTopicId");
				var page = apiCall.Parameters.IntOrNull("page");
				if (page == null)
					return ApiError.InvalidParam("page");
				var pageSize = apiCall.Parameters.IntOrNull("pageSize");
				if (pageSize == null)
					return ApiError.InvalidParam("pageSize");

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
				return ApiError.ServerError(ex.Message);
			}
		}

		/* createPost(tokenId, Post) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: User and topic.RoleToRead <= token.User.RawRole | Admin
		 */	
		private async Task<ApiResult> createPost(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return ApiError.InvalidToken();

				var postJson = apiCall.Parameters.OrNull("Post");
				if (string.IsNullOrWhiteSpace(postJson))
					return ApiError.InvalidParam("Post");

				var post = JsonConvert.DeserializeObject<Post>(postJson);
				post.Id = 0;
				if (post.UserId != token.UserId)
					return ApiError.Unauthorised();

				var topicId = post.ParentId;
				if (topicId == null)
					return ApiError.InvalidParam("Post.ParentId");
				var topic = await (from t in _context.topics
					where t.Id == topicId.Value
					select t).FirstOrDefaultAsync();
				if(token.User.RawRole < topic.RoleToEdit)
					return ApiError.Unauthorised();
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
				return ApiError.ServerError(ex.Message);
			}
		}

		private async Task<ApiResult> ChangePostAsync(ApiCall apiCall, Action<Post, bool> callback)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return ApiError.InvalidToken();
				var role = new UserRole( token.User.RawRole );
				var postId = apiCall.Parameters.IntOrNull("postId");
				if (postId == null)
					return ApiError.InvalidParam("postId");
				var post = await (from p in _context.posts
					where p.Id == postId.Value
					select p).FirstOrDefaultAsync();
				if (post == null)
					return ApiError.NotFound();
				bool mayChange = (post.UserId != null && post.UserId == token.UserId) ||
					role.IsAdmin;
				if (!mayChange)
					return ApiError.Unauthorised();
				try
				{
					callback(post, role.IsAdmin);
				}
				catch(Exception ex)
				{
					return ApiError.ServerError(ex.Message);
				}
				post.Modified = DateTime.Now;
				_context.Update(post);
				await _context.SaveChangesAsync();

				return new ApiResult<Post>(StdResult.OK, post.CloneForExport());
			}
			catch(Exception ex)
			{
				Log(ex);
				return ApiError.ServerError(ex.Message);
			}
		}

		/* getPost(tokenId, Post) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: User | Admin
		 */	
		private async Task<ApiResult> getPost(ApiCall apiCall)
		{
			try
			{
				var token = await quickGetToken(apiCall);
				if (token == null)
					return ApiError.InvalidToken();
				var tokenRole = token.User.RawRole;

				var postId = apiCall.Parameters.IntOrNull("postId");
				if (postId == null)
					return ApiError.InvalidParam("postId");

				var post = await (from p in _context.posts
					where p.Id == postId.Value
					select p).FirstOrDefaultAsync();
				
				if (tokenRole < post.RoleToRead)
					return ApiError.Unauthorised();

				if (post == null)
					return ApiError.NotFound();
				
				return new ApiResult<Post>(StdResult.OK, post.CloneForExport());
			}
			catch(Exception ex)
			{
				Log(ex);
				return ApiError.ServerError(ex.Message);
			}
		}

		/* updatePost(tokenId, Post) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: User and post.OwnerId == token.User.Id | Admin
		 */	
		private async Task<ApiResult> updatePost(ApiCall apiCall)
		{
			try
			{
				var post = apiCall.Parameters.FromJson<Post>("Post");
				if (post == null)
					return ApiError.InvalidParam("Post");
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
				return ApiError.ServerError(ex.Message);
			}
		}

		/* deletePost(tokenId, postId) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: User and post.OwnerId == token.User.Id | Admin
		 */	
		private async Task<ApiResult> deletePost(ApiCall apiCall)
		{
			return await ChangePostAsync(apiCall, (post, isAdmin) => 
			{
				post.Title = "This post has been deleted";
				post.Body = "This post has beed deleted";
				post.UserId = null;
				post.RoleToEdit = UserRole.Admin;
				post.RoleToRead = UserRole.Guest;
			});
		}

		/* hidePost(tokenId, postId) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: Admin
		 */	
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
		/*
			Requirements:
				Token: Yes
				User Role: Admin
		 */	
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