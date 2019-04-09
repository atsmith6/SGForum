using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Web;
using System.Net;

namespace SGWEB
{
    public class PageTurnLogic
    {
		private Dictionary<string, string> _params = new Dictionary<string, string>();
		private List<PageNumLogic> _logics = new List<PageNumLogic>();

        public PageTurnLogic()
		{

		}

		public void Add(PageNumLogic logic)
		{
			if (logic != null)
				_logics.Add(logic);
		}

		public void Add(string param, string value)
		{
			_params.Add(param, value);
		}

		private void createAnchorElement(StringBuilder sb, string url, PageNumLogic logic, int newPage, string label, string hint, IEnumerable<PageNumLogic> others)
		{
			sb.Append("<a href=\"");
			sb.Append(url);
			sb.Append('?');
			foreach(KeyValuePair<string, string> entry in _params)
			{
				sb.Append(WebUtility.HtmlEncode(entry.Key));
				sb.Append('=');
				sb.Append(WebUtility.HtmlEncode(entry.Value));
				sb.Append('&');
			}

			sb.Append(WebUtility.HtmlEncode(logic.Param));
			sb.Append('=');
			sb.Append($"{newPage}");
			foreach (var o in others)
			{
				if (o.CurrentPage != -1)
				{
					sb.Append("&");
					sb.Append(WebUtility.HtmlEncode(o.Param));
					sb.Append('=');
					sb.Append($"{o.CurrentPage}");
				}
			}
			sb.Append("\">");
			if (!string.IsNullOrWhiteSpace(hint))
			{
				sb.Append("<div class=\"SGHasTooltip\">");
			}
			sb.Append(WebUtility.HtmlEncode(label));
			if (!string.IsNullOrWhiteSpace(hint))
			{
				sb.Append($"<div class=\"SGTooltipText\">{WebUtility.HtmlEncode(hint.Trim())}</div></div>");
			}

			sb.Append("</a>");
		}

		public string quickNavLink(string url, string forTag)
		{
			if (_logics.Count == 0)
				return string.Empty;

			var logic = (from l in _logics where l.Param == forTag select l).FirstOrDefault();
			var others = from l in _logics where l.Param != forTag select l;

			StringBuilder sb = new StringBuilder();
			if (logic.Previous != null)
				createAnchorElement(sb, url, logic, logic.Previous.Value, "<<", "Previous Page", others);
			sb.Append($" Page {logic.CurrentPage + 1} of {logic.PageCount} ");
			if (logic.Next != null)
				createAnchorElement(sb, url, logic, logic.Next.Value, ">>", "Next Page", others);

			return sb.ToString();
		}
    }
}