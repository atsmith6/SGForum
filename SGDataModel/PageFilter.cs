using System;
using System.Collections;
using System.Collections.Generic;

namespace SGDataModel
{
    public class PageFilter<T> : IReadOnlyCollection<T>
    {
		private IList<T> _collection;
		private List<T> _filtered = new List<T>();
		private int _itemsPerPage;
		private int _currentPage;
		public int CurrentPage
		{
			get 
			{
				return _currentPage;
			}
			set 
			{
				int p = value;
				int pageCount = PageCount;
				if (p >= 0 && p < pageCount)
				{
					_currentPage = value;
					populate();
				}
				else
					throw new Exception($"Page out of range: {p}");
			}
		}
		
		public int PageCount 
		{
			get
			{
				int count = _collection.Count;
				int pageCount = (count / _itemsPerPage);
				if (count %_itemsPerPage != 0)
					++pageCount;
				return pageCount;
			}
		}

		public PageFilter(int itemsPerPage, IList<T> collection)
		{
			if (collection == null)
				throw new Exception($"Invalid parameter: collection null");
			if (itemsPerPage < 1)
				throw new Exception($"Invalid parameter: itemsPerPage={itemsPerPage}");
			_collection = collection;
			_itemsPerPage = itemsPerPage;
			_currentPage = 0;
			populate();
		}

		private void populate()
		{
			if (_filtered.Count > 0)
				_filtered.Clear();
			int begin = _itemsPerPage * _currentPage;
			int end = begin + _itemsPerPage;
			end = Math.Min(end, _collection.Count);
			for (int i = begin; i < end; ++i)
			{
				_filtered.Add(_collection[i]);
			}
		}

		// IReadOnlyCollection<T>

		public int Count 
		{
			get
			{
				return _filtered.Count;
			}
		}
        public IEnumerator<T> GetEnumerator()
		{
			return _filtered.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator()
		{
			return _filtered.GetEnumerator();
		}
    }
}