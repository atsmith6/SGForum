using System;
using System.Collections.Generic;

namespace SGDataModel
{
    public class PostResult
    {
        public int Count { get; set; }

		public int CurrentPage { get; set; }
		public int PageCount { get; set; }
		public List<Post> Posts { get; set; } = new List<Post>();
    }
}