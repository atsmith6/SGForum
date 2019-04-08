using System;
using System.Collections.Generic;
using System.Linq;

namespace SGDataModel
{
    public class TestDataCreator
    {
		private SGContext _context;
		private int topicIndex = 0;

		private Random _rnd = new Random();

		public TestDataCreator(SGContext context)
		{
			_context = context;
		}

		public void Execute()
		{
			List<User> users = CreateUsers(10);
			CreateTopics(users, 16, 2, null);
		}

		private List<User> CreateUsers(int count)
		{
			var users = new List<User>();
			for (int i = 0; i < count; ++i)
			{
				string email = $"testUser{i+1}@localhost"; 
				string diplayName = $"Test User {i+1}";
				string password = $"pwd";
				var userTask = UserTasks.CreateUserNoAuthCheckAsync(_context, email, diplayName, "pwd", UserRole.User);
				userTask.Wait();
				var user = userTask.Result;
				users.Add(user.CloneForExport());
			}
			return users;
		}

		private Topic quickTopic(List<User> users, int rawRole, int index, int? parentId)
		{
			string roleDesc;
			switch(rawRole)
			{
				case UserRole.Guest: roleDesc = "(Guest)"; break;
				case UserRole.User: roleDesc = "(User)"; break;
				case UserRole.Admin: roleDesc = "(Admin)"; break;
				default: roleDesc = "(Role desc error)"; break;
			}
			var topic = new Topic();
			topic.Title = $"A topic {index}-{topicIndex++} role {roleDesc}";
			topic.RoleToEdit = Math.Max(rawRole, UserRole.User);
			topic.RoleToRead = rawRole;
			topic.ParentId = parentId;
			topic.IsRootEntry = parentId == null;
			topic.Modified = DateTime.UtcNow;
			topic.Owner = randomUserWithRole(users, UserRole.User);
			return topic;
		}

		private void CreateTopics(List<User> users, int count, int depth, int? parentId)
		{
			int third = count / 3;
			int adminCount = third;
			int userCount = third;
			int guestCount = count - (2 * third);
			// Create admin topics
			CreateTopicsForRole(users, count, adminCount, depth, parentId, UserRole.Admin);
			CreateTopicsForRole(users, count, adminCount, depth, parentId, UserRole.User);
			CreateTopicsForRole(users, count, adminCount, depth, parentId, UserRole.Guest);
		}

		private void CreateTopicsForRole(List<User> users, int fullCount, int count, int depth, int? parentId, int rawRole)
		{
			var topics = new List<Topic>();
			for(int i = 0; i < count; ++i)
			{
				var topic = quickTopic(users, rawRole, i, parentId);
				_context.Add(topic);
				topics.Add(topic);
			}
			_context.SaveChanges();
			
			foreach(var topic in topics)
			{
				CreatePostsForTopic(users, topic.Id, 20);
				if (depth > 1)
				{
					CreateTopics(users, fullCount, depth - 1, topic.Id);
				}
			}
		}

		private Post quickPost(User user, int topicId, int indexer)
		{
			var post = new Post();
			post.ParentId = topicId;
			post.RoleToEdit = UserRole.User;
			post.RoleToRead = UserRole.Guest;
			post.Title = $"Post Title {indexer}";
			var now = DateTime.UtcNow;
			post.Modified = now;
			post.Created = now;
			post.Body = @"Ut vitae euismod massa. Aenean egestas luctus bibendum. Nam volutpat dui lobortis, ultrices nisl eu, tempus dolor. Praesent id orci et orci mollis sagittis. Morbi maximus nibh in magna consectetur, at lobortis augue tristique. Nunc mollis ex feugiat faucibus dictum. Ut in faucibus nunc. Phasellus auctor ante id nulla dignissim facilisis. Donec tempus nulla et sagittis ornare. Ut aliquam rutrum ante. Vestibulum aliquet nunc non diam mollis, ut aliquet nisl posuere. Morbi interdum, diam quis suscipit vehicula, risus urna aliquam diam, vitae condimentum magna risus ut nulla. Aliquam vehicula libero lacus, at aliquam massa venenatis nec. Duis mi ligula, scelerisque nec euismod at, efficitur non massa. Integer aliquet nulla ut laoreet porttitor.

Pellentesque lobortis, sapien id fermentum consequat, est nibh suscipit nisl, vitae volutpat enim velit eget justo. Aliquam auctor libero vitae mi mattis feugiat. Curabitur eleifend eu turpis nec dictum. Nullam mattis pellentesque mollis. Pellentesque maximus magna sit amet ultrices pulvinar. Fusce congue nulla in iaculis fringilla. Suspendisse potenti. Sed tincidunt orci ac nisi condimentum mollis. Etiam sit amet placerat nunc. Fusce laoreet quis magna a luctus. Curabitur nisi ligula, laoreet imperdiet euismod in, sagittis quis leo. Aliquam suscipit enim vel mauris mattis, ut commodo nisl rutrum. Fusce placerat maximus semper.";
			post.UserId = user.Id;
			return post;
		}

		private User randomUserWithRole(List<User> users, int role)
		{
			var list = (from u in users where u.RawRole == role select u).ToList();
			int idx = _rnd.Next(list.Count);
			return list[idx];
		}

		private void CreatePostsForTopic(List<User> users, int topicId, int count)
		{
			
			var userCount = users.Count;
			for(int i = 0; i < count; ++i)
			{
				int userIndex = _rnd.Next(userCount);
				var post = quickPost(users[userIndex], topicId, i);
				_context.posts.Add(post);
			}
			_context.SaveChanges();
		}
    }
}