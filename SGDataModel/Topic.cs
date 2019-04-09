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
    public class Topic
    {
		public int Id { get; set; }
		
		[Required]
		public string Title { get; set; }

		[Required]
		public DateTime Modified { get; set; }
		[Required]
		public DateTime Created { get; set; }

		// This is used to handle the limitation against self-referential cascading deletes.
		[Required]
		public bool IsRootEntry { get; set; }

		[Required]
		public int RoleToRead { get; set; }

		[Required]
		public int RoleToEdit { get; set; }

		//public Topic Parent { get; set; }
		public int? ParentId { get; set; }
		public virtual Topic Parent { get; set; }
		public virtual ICollection<Topic> Children { get; set; }

		public virtual ICollection<Post> Posts { get; set; }

		public virtual User Owner { get; set; }
		public int? OwnerId { get; set; }

		public Topic CloneForExport()
		{
			var topic = new Topic();
			topic.Id = Id;
			topic.Title = Title;
			topic.IsRootEntry = IsRootEntry;
			topic.RoleToRead = RoleToRead;
			topic.RoleToEdit = RoleToEdit;
			//topic.Parent = null;
			topic.ParentId = ParentId;
			topic.Modified = Modified;
			topic.Created = Created;
			return topic;
		}
    }
}