
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Extensions;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace SGDataModel
{
    public class Post
    {
		// TODO: We should add some non-persistent field to show the user displayName, 
		// and then populate this from some persistent cache somewhere.

        public int Id { get; set; }
		public int? ParentId { get; set; }
		public virtual Topic Parent { get; set; }
		[Required]
		public DateTime Created { get; set; }
		[Required]
		public DateTime Modified { get; set; }
		[StringLength(100)]
		public string Title { get; set; }
		[Column(TypeName="TEXT")]
		//[StringLength(20480)]
		public string Body { get; set; }
		public virtual User User { get; set; }
		public int? UserId { get; set; }
		[Required]
		public int RoleToRead { get; set; }
		[Required]
		public int RoleToEdit { get; set; }
		[Required]
		public bool Hidden { get; set; }

		[NotMapped]
		public string UserDisplayName_NotMapped;

		public Post CloneForExport()
		{
			var post = new Post();
			post.Id = Id;
			post.Parent = null;
			post.ParentId = ParentId;
			post.Modified = Modified;
			post.Created = Created;
			post.Title = Title;
			post.Body = Body;
			post.User = null;
			if (User != null)
			{
				post.UserDisplayName_NotMapped = User.DisplayName;
				var role = new UserRole(User.RawRole);
				if (role.IsAdmin)
				{
					post.UserDisplayName_NotMapped += " (Admin)";
				}
			}
			post.UserId = UserId;
			post.RoleToRead = RoleToRead;
			post.RoleToEdit = RoleToEdit;
			return post;
		}
    }
}