using System;
using System.Text;

namespace SGMarkdown
{
	public class MarkdownHeadingHandler: IMarkdownLineHandler
	{
		private string GetHeadingTag(string line)
		{
			if (line == null || line.Length == 0 || line[0] != '#')
				return null;
			if (line.StartsWith("#."))
				return null;
			var pos = 0;
			var len = line.Length;
			while (pos < len && line[pos] == '#')
				++pos;
			return line.Substring(0, pos);
		}

		public bool HandleLine(MarkdownNode context, string line)
		{
			line = line.Trim();
			var headingTag = GetHeadingTag(line);
			if (headingTag == null)
				return false;

			MarkdownType type;
			switch(headingTag)
			{
				case "#": type = MarkdownType.Heading1; break;
				case "##": type = MarkdownType.Heading2; break;
				case "###": type = MarkdownType.Heading3; break;
				default: return false;
			}

			string headingText = line.Substring(headingTag.Length).Trim();
			var node = new MarkdownNode()
			{
				Type = type,
				Text = headingText
			};

			context.AddChild(node);
			return true;
		}
	}
}