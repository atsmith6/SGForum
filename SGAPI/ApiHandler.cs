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
    public class ApiHandler : AutoApi
    {
		private SGContext _context;

        public ApiHandler(SGContext context) : base()
		{
			_context = context;
		}

		public async Task<ApiResult> processCallAsync(ApiCall apiCall)
		{
			return await AutoProcessAsync(apiCall);
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
		[SGAutoApi]
		public async Task<LoginToken> login(string email, string password)
		{
			var token = await LoginTokenTasks.LoginAsync(_context, email, password);
			if (token == null)
				throw AutoApiError.AuthenticationFailure();
			return token;
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
		[SGAutoApi]
		public async Task logout(Int64 tokenId, string email)
		{
			var token = await LoginTokenTasks.GetLoginTokenAsync(_context, tokenId);
			if (token == null)
					throw AutoApiError.NotFound();
			if (email != null)
			{
				if (string.IsNullOrWhiteSpace(email))
					throw AutoApiError.InvalidParam("email");
				var userRole = new UserRole(token.User.RawRole);
				if (!userRole.IsAdmin)
					throw AutoApiError.Unauthorised();
				await LoginTokenTasks.LogoutAsync(_context, token, email);
			}
			else
			{
				await LoginTokenTasks.LogoutAsync(_context, token);
			}
		}

		/* getToken(tokenId) -> LoginToken */
		/*
			Requirements:
				Token: No (only ID)
				User Role: Guest | User | Administrator
		 */
		[SGAutoApi]
		public async Task<LoginToken> getToken(Int64 tokenId)
		{
			LoginToken token;
			if (tokenId == LoginToken.AnonymousLoginId)
				token = LoginTokenTasks.GetAnonmymousToken();
			else
				token = await LoginTokenTasks.GetLoginTokenAsync(_context, tokenId);

			if (token == null)
				throw AutoApiError.InvalidToken();

			return token.CloneForExport();
		}

		/* getDbInfo() -> DatabaseInfo */
		/*
			Requirements:
				Token: No
				User Role: Any
		 */
		[SGAutoApi]
		public async Task<DatabaseInfo> getDBInfo()
		{
			var info = await _context.databaseInfo.FirstAsync();
			if (info == null)
				throw new AutoApiError("Failed to retrieve the database info");
			return info.CloneForExport();
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

		private async Task<LoginToken> quickGetToken(Int64 tokenId, bool allowAnon = false)
		{
			if (tokenId == LoginToken.AnonymousLoginId && allowAnon)
				return LoginTokenTasks.GetAnonmymousToken();
			var token = await LoginTokenTasks.GetLoginTokenAsync(_context, tokenId);
			if (token.User.Active)
				return token;
			throw AutoApiError.InvalidToken();
		}

		/* createUser(tokenId, email, displayName, password, role) -> User */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator
		 */
		[SGAutoApi]
		public async Task<User> createUser(Int64 tokenId, string email, string displayName, string password, int role)
		{
			var token = await quickGetToken(tokenId);

			var userRole = new UserRole(token.User.RawRole);
			if (!userRole.IsAdmin)
				throw AutoApiError.Unauthorised();
			
			if (string.IsNullOrWhiteSpace(email))
				throw AutoApiError.InvalidParam("email");
			if (string.IsNullOrWhiteSpace(password))
				throw AutoApiError.InvalidParam("password");
			
			if (!UserRole.RoleIsValid(role))
				throw AutoApiError.InvalidParam("role");
			try
			{
				var user = await UserTasks.CreateUserAsync(_context, token, email, displayName, password, role);
				if (user == null)
					throw AutoApiError.ServerError("Create user failed unexpectedly.");
				return user.CloneForExport();
			}
			catch (Exception ex)
			{
				if (ex.Message == "Unauthorised")
					throw AutoApiError.Unauthorised();
				throw;
			}
		}

		/* getUser(tokenId, userId) */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator if userId != token.User.Id | User
		 */
		[SGAutoApi]
		public async Task<User> getUser(Int64 tokenId, int userId)
		{
			var token = await quickGetToken(tokenId);

			UserRole role = new UserRole(token.User.RawRole);
			if (!(role.IsAdmin || token.UserId == userId))
				throw AutoApiError.Unauthorised(); 

			var user = await (from u in _context.users where u.Id == userId select u).FirstOrDefaultAsync();
			if (user == null)
				throw AutoApiError.NotFound();

			user = user.CloneForExport();
			return user;
		}

		/* getUsers(tokenId) */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator
		 */
		[SGAutoApi]
		public async Task<List<User>> getUsers(Int64 tokenId)
		{
			var token = await quickGetToken(tokenId);
			
			var role = new UserRole(token.User.RawRole);
			if (!role.IsAdmin)
				throw AutoApiError.Unauthorised();
			var users = await (from u in _context.users select u).ToListAsync();
			for(int i = 0; i < users.Count; ++i)
			{
				users[i] = users[i].CloneForExport();
			}
			return users;
		}

		/* updateUser(tokenId, User) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator if userId != token.User.Id | User
		 */
		[SGAutoApi]
		public async Task updateUser(Int64 tokenId, User user)
		{
			var token = await quickGetToken(tokenId);

			if (token.UserId != user.Id)
			{
				var userRole = new UserRole(token.User.RawRole);
				if (!userRole.IsAdmin)
					throw AutoApiError.Unauthorised();
			}
			await UserTasks.UpdateUserAsync(_context, token, user);
		}

		/* updatePassword(email, oldPassword, newPassword) -> Success */
		/*
			Requirements:
				Token: No
				User Role: Any
				Password: Yes
		 */
		[SGAutoApi]
		public async Task updatePassword(string email, string oldPassword, string newPassword)
		{
			if (string.IsNullOrWhiteSpace(email))
				throw AutoApiError.InvalidParam("email");
			
			if (string.IsNullOrWhiteSpace(oldPassword))
				throw AutoApiError.InvalidParam("oldPassword");
			
			if (string.IsNullOrWhiteSpace(newPassword))
				throw AutoApiError.InvalidParam("newPassword");

			await UserTasks.UpdatePasswordAsync(_context, email, oldPassword, newPassword);
		}

		private async Task<ApiResult> activateUser_Impl(Int64 tokenId, string email, bool active)
		{
			var token = await quickGetToken(tokenId);

			var userRole = new UserRole(token.User.RawRole);
			if (!userRole.IsAdmin)
				throw AutoApiError.Unauthorised();

			if (string.IsNullOrWhiteSpace(email))
				throw AutoApiError.InvalidParam("email");

			await UserTasks.SetUserActiveAsync(_context, token, email, active);
			return new ApiResult(StdResult.OK);
		}

		/* activateUser(tokenId, email) */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator
		 */
		[SGAutoApi]
		public async Task<ApiResult> activateUser(Int64 tokenId, string email)
		{
			return await activateUser_Impl(tokenId, email, true);
		}

		/* deactivateUser(tokenId, email) */
		/*
			Requirements:
				Token: Yes
				User Role: Administrator
		 */
		[SGAutoApi]
		public async Task<ApiResult> deactivateUser(Int64 tokenId, string email)
		{
			return await activateUser_Impl(tokenId, email, false);
		}

		/* getTopic(tokenId, topicId) -> Topic */
		/*
			Requirements:
				Token: Yes
				User Role: >= topic.RoleToRead
		 */
		[SGAutoApi]
		public async Task<Topic> getTopic(Int64 tokenId, int topicId)
		{
				var token = await quickGetToken(tokenId, true);

				Topic topic = await (from t in _context.topics
					where t.Id == topicId
					select t).FirstOrDefaultAsync();

				if (topic != null)
				{
					int tokenRole = token.User.RawRole;
					if (topic.RoleToRead > tokenRole)
						throw AutoApiError.Unauthorised();

					return topic.CloneForExport();
				}

				throw AutoApiError.NotFound();
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
		[SGAutoApi]
		public async Task<TopicResult> getTopics(Int64 tokenId, int? parentTopicId)
		{
			var token = await quickGetToken(tokenId, true);
			
			List<Topic> topics;
			int tokenRole = token.User.RawRole;
			var parentId = parentTopicId;
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
			return ret;
		}
			
		/*	getTopicPage(tokenId, parentTopicId?, page, pageSize) -> TokenResult */
		/*	Note: page -1 defaults to 0, order is descending... */
		/*
			Requirements:
				Token: No
				User Role: >= topic.RoleToRead
		 */	
		[SGAutoApi]
		public async Task<TopicResult> getTopicPage(Int64 tokenId, int? parentTopicId, int page, int pageSize)
		{
			var token = await quickGetToken(tokenId, true);

			var parentId = parentTopicId;
			
			if (page == -1)
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
			var pageCount = count / pageSize;
			if (count % pageSize != 0)
				++pageCount;
			if (page >= pageCount)
				page = Math.Max(0, pageCount - 1);

			int begin = (page) * pageSize;

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
			var topics = await query.Skip(begin).Take(pageSize).ToListAsync();

			var topicData = new TopicResult();
			if (parentId != null)
				topicData.ParentList = await TopicTasks.CreateParentList(_context, parentId.Value);
			foreach(var topic in topics)
			{
				topicData.Topics.Add(topic.CloneForExport());
			}
			topicData.TopicCount = count;
			topicData.CurrentPage = page;
			topicData.PageCount = pageCount;
			return topicData;
		}

		/* createTopic(tokenId, title, roleToEdit, roleToRead) */
		/* createTopic(tokenId, parentTopicId, title, roleToEdit, roleToRead) */
		/*
			Requirements:
				Token: Yes
				User Role: >= topic.Parent.RoleToEdit | Admin for root topics
		 */	
		[SGAutoApi]
		public async Task<Topic> createTopic(Int64 tokenId, int? parentTopicId, string title, int roleToEdit, int roleToRead)
		{
			var token = await quickGetToken(tokenId);

			var userRole = new UserRole(token.User.RawRole);
			if (parentTopicId == null)  //Only administators can edit the root topic level.
			{
				if (!userRole.IsAdmin)
					throw AutoApiError.Unauthorised();
			}
			else
			{
				var parentTopic = await(from t in _context.topics
					where t.Id == parentTopicId.Value
					select t).FirstOrDefaultAsync();
				if (parentTopic == null)
					throw AutoApiError.NotFound();
				if (parentTopic.RoleToEdit > token.User.RawRole)
					throw AutoApiError.Unauthorised();
			}

			if (title == null)
				throw AutoApiError.InvalidParam("title.");
			if (!UserRole.RoleIsValid(roleToEdit))
				throw AutoApiError.InvalidRole("roleToEdit.");
			if (!UserRole.RoleIsValid(roleToRead))
				throw AutoApiError.InvalidParam("roleToRead.");
			if (roleToRead > token.User.RawRole)
				throw new AutoApiError("The topic would be unreadable by its creator.");
			var topic = new Topic();
			topic.Title = title;
			topic.RoleToEdit = roleToEdit;
			topic.RoleToRead = roleToRead;
			topic.ParentId = parentTopicId;
			topic.IsRootEntry = parentTopicId == null;
			topic.OwnerId = token.UserId;
			var now = DateTime.UtcNow;
			topic.Modified = now;
			topic.Created = now;
			_context.topics.Add(topic);
			await _context.SaveChangesAsync();

			return topic.CloneForExport();
		}

		/* updateTopic(tokenId, Topic) */
		/*
			Requirements:
				Token: Yes
				User Role: If User then topic.OwnerId == token.User.Id | Admin
		 */
		[SGAutoApi]
		public async Task updateTopic(Int64 tokenId, Topic Topic)
		{
			var token = await quickGetToken(tokenId);

			var topic = Topic;

			var originalTopic = await (from t in _context.topics
				where t.Id == topic.Id
				select t).FirstOrDefaultAsync();
			if (originalTopic == null)
				throw AutoApiError.NotFound();
			var role = new UserRole(token.User.RawRole);
			var canUpdate = (role.IsAdmin || (originalTopic.OwnerId != null && originalTopic.OwnerId.Value == token.UserId));
			if (!canUpdate)
				throw AutoApiError.Unauthorised();
			originalTopic.Title = topic.Title;
			originalTopic.RoleToEdit = topic.RoleToEdit;
			originalTopic.RoleToRead = topic.RoleToRead;
			_context.topics.Update(originalTopic);
			await _context.SaveChangesAsync();
		}

		/* deleteTopic(tokenId, topicId) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: Admin
		 */	
		[SGAutoApi]
		public async Task deleteTopic(Int64 tokenId, int topicId)
		{
			var token = await quickGetToken(tokenId);

			var userRole = new UserRole(token.User.RawRole);
			if (!userRole.IsAdmin)
				throw AutoApiError.Unauthorised();

			var topic = await (from t in _context.topics
				where t.Id == topicId
				select t).FirstOrDefaultAsync();
			
			if (topic == null)
				throw AutoApiError.NotFound();
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
		[SGAutoApi]
		private async Task<List<Post>> getPosts(Int64 tokenId, int parentTopicId)
		{
			var token = await quickGetToken(tokenId, true);

			int tokenRole = token.User.RawRole;
			var topicId = parentTopicId;

			var posts = await (from p in _context.posts.Include(p => p.User)
				where p.ParentId == topicId
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

			return ret;
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
		[SGAutoApi]
		public async Task<PostResult> getPostPage(Int64 tokenId, int parentTopicId, int page, int pageSize)
		{
				var token = await quickGetToken(tokenId, true);

				int tokenRole = token.User.RawRole;
				var parentId = parentTopicId;

				int count = await (from p in _context.posts
					where p.ParentId == parentId
					select p).CountAsync();
				var pageCount = count / pageSize;
				if (count % pageSize != 0)
					++pageCount;

				if (page == -1 || page >= pageCount)
					page = Math.Max(0, pageCount - 1);

				int begin = (page) * pageSize;

				IQueryable<Post> query = (from p in _context.posts
					where p.ParentId == parentId
					orderby p.Created ascending
					select p);

				var posts = await query.Include(p => p.User).Skip(begin).Take(pageSize).ToListAsync();

				var postsResult = new PostResult();
				postsResult.Count = count;
				postsResult.CurrentPage = page;

				CreatePostExportList(tokenRole, posts, postsResult.Posts);

				return postsResult;
		}

		/* createPost(tokenId, Post) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: User and topic.RoleToRead <= token.User.RawRole | Admin
		 */	
		[SGAutoApi]
		public async Task<Post> createPost(Int64 tokenId, Post Post)
		{
			var token = await quickGetToken(tokenId);

			var post = Post;
			post.Id = 0;
			if (post.UserId != token.UserId)
				throw AutoApiError.Unauthorised();

			var topicId = post.ParentId;
			if (topicId == null)
				throw AutoApiError.InvalidParam("Post.ParentId");
			var topic = await (from t in _context.topics
				where t.Id == topicId.Value
				select t).FirstOrDefaultAsync();
			if(token.User.RawRole < topic.RoleToEdit)
				throw AutoApiError.Unauthorised();
			var now = DateTime.UtcNow;
			post.Created = now;
			post.Modified = now;
			_context.posts.Add(post);
			await _context.SaveChangesAsync();
			return post.CloneForExport();
		}

		private async Task<Post> ChangePostAsync(Int64 tokenId, int postId, Action<Post, bool> callback)
		{
				var token = await quickGetToken(tokenId);

				var role = new UserRole( token.User.RawRole );

				var post = await (from p in _context.posts
					where p.Id == postId
					select p).FirstOrDefaultAsync();
				if (post == null)
					throw AutoApiError.NotFound();
				bool mayChange = (post.UserId != null && post.UserId == token.UserId) ||
					role.IsAdmin;
				if (!mayChange)
					throw AutoApiError.Unauthorised();
				try
				{
					callback(post, role.IsAdmin);
				}
				catch(Exception ex)
				{
					if (ex is AutoApiError)
						throw;
					throw AutoApiError.ServerError(ex.Message);
				}
				post.Modified = DateTime.Now;
				_context.Update(post);
				await _context.SaveChangesAsync();

				return post.CloneForExport();
		}

		/* getPost(tokenId, Post) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: User | Admin
		 */	
		[SGAutoApi]
		public async Task<Post> getPost(Int64 tokenId, int postId)
		{
			var token = await quickGetToken(tokenId);
			var tokenRole = token.User.RawRole;

			var post = await (from p in _context.posts
				where p.Id == postId
				select p).FirstOrDefaultAsync();

			if (tokenRole < post.RoleToRead)
				throw AutoApiError.Unauthorised();

			if (post == null)
				throw AutoApiError.NotFound();
			
			return post.CloneForExport();
		}

		/* updatePost(tokenId, Post) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: User and post.OwnerId == token.User.Id | Admin
		 */
		[SGAutoApi]
		public async Task<Post> updatePost(Int64 tokenId, Post Post)
		{
			var post = Post;
			
			return await ChangePostAsync(tokenId, post.Id, 
				(originalPost, isAdmin) =>
				{
					if ((originalPost.RoleToEdit != post.RoleToEdit ||
						originalPost.RoleToRead != post.RoleToRead) &&
						!isAdmin)
					{
						throw AutoApiError.NotFound();
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

		/* deletePost(tokenId, postId) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: User and post.OwnerId == token.User.Id | Admin
		 */
		[SGAutoApi]
		public async Task<Post> deletePost(Int64 tokenId, int postId)
		{
			return await ChangePostAsync(tokenId, postId, (post, isAdmin) => 
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
		[SGAutoApi]
		public async Task<Post> hidePost(Int64 tokenId, int postId)
		{
			return await ChangePostAsync(tokenId, postId,
				(post, isAdmin) => 
				{
					if (!isAdmin)
						throw AutoApiError.Unauthorised();
					post.Hidden = true;
				});
		}

		/* unhidePost(tokenId, postId) -> Success */
		/*
			Requirements:
				Token: Yes
				User Role: Admin
		 */	
		[SGAutoApi]
		public async Task<Post> unhidePost(Int64 tokenId, int postId)
		{
			return await ChangePostAsync(tokenId, postId, (post, isAdmin) => 
			{
				if (!isAdmin)
					throw AutoApiError.Unauthorised();
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