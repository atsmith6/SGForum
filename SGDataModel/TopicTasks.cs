using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Extensions;

namespace SGDataModel
{
    public static class TopicTasks
    {
        public static async Task<List<Topic>> CreateParentList(SGContext context, int topicId)
		{
			var parentList = new List<Topic>();

			while(true)
			{
				var topic = await (from t in context.topics
					where t.Id == topicId
					select t).FirstOrDefaultAsync();
				if (topic != null)
					parentList.Add(topic.CloneForExport());
				if (topic.ParentId == null)
					break;
				topicId = topic.ParentId.Value;
			}

			if (parentList.Count > 1)
				parentList.Reverse();

			return parentList;
		}
    }
}