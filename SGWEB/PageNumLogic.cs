using System;

namespace SGWEB
{
    public class PageNumLogic
    {
		public int PageCount { get; private set; }
		public int CurrentPage { get; private set; }
		public int? Previous { get; private set; }
		public int?	Next { get; private set; }

		public string Param { get; private set; }

        public PageNumLogic(string param, int current, int totalItems, int pageSize)
		{
			Param = param;
			int? prev = current <= 0 ? null : new Nullable<int>(current - 1);
			int lastPage = (totalItems / pageSize) - 1;
			if (totalItems % pageSize > 0)
				++lastPage;
			int? next = current < lastPage ? new Nullable<int>(current + 1) : null;

			var count = totalItems / pageSize;
			if (totalItems % pageSize != 0)
				++count;

			PageCount = count;
			CurrentPage = current;
			Previous = prev;
			Next = next;
		}
    }
}