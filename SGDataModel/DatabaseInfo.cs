using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Extensions;


namespace SGDataModel
{
    public class DatabaseInfo
    {
        public int Id { get; set; }
		[Required]
        public int MajVersion { get; set; }
		[Required]
        public int MinVersion { get; set; }
		[Required]
		public string ForumTitle { get; set; }
		public string ForumLogo { get; set; }

		[Required]
		public int RootTopicId { get; set; }

		public DatabaseInfo CloneForExport()
		{
			var ret = new DatabaseInfo();
			ret.Id = Id;
			ret.MajVersion = MajVersion;
			ret.MinVersion = MinVersion;
			ret.ForumTitle = ForumTitle;
			ret.ForumLogo = ForumLogo;
			ret.RootTopicId = RootTopicId;
			return ret;
		}
    }
}
