using System;
using System.Text;

namespace SGMarkdown
{
    public class MarkdownBulletsHandler : MarkdownTextParser, IMarkdownLineHandler
    {
        public bool HandleLine(MarkdownNode context, string line)
		{
			line = line.Trim();
			bool isBullet;
			int skip;
			if (line.StartsWith("-"))
			{
				isBullet = true;
				skip = 1;
			}
			else if (line.StartsWith("#."))
			{
				isBullet = false;
				skip = 2;
			}
			else
				return false;

			var bulletNode = new MarkdownNode()
			{
				Type = isBullet ? MarkdownType.Bulleted : MarkdownType.Numbered,
			};

			context.AddChild(bulletNode);

			AppendParsedTextTo(bulletNode, line.Substring(skip).Trim());

			return true;
		}
    }
}