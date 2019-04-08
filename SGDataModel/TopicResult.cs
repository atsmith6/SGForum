using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace SGDataModel
{
	/* This is a wrapper class to support the web API */
    public class TopicResult
    {
        public List<Topic> ParentList;
		public List<Topic> Topics = new List<Topic>();

		public int TopicCount { get; set; }

		public int CurrentPage { get; set; }
		public int PageCount { get; set; }
    }
}