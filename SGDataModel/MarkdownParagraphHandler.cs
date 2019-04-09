using System;
using System.Text;
using System.Collections.Generic;

namespace SGMarkdown
{
    public class MarkdownParagraphHandler : MarkdownTextParser, IMarkdownLineHandler
    {
        public bool HandleLine(MarkdownNode context, string line)
		{
			line = line.Trim();
			var para = new MarkdownNode()
			{
				Type = MarkdownType.Paragraph
			};
			context.AddChild(para);

			AppendParsedTextTo(para, line);

			return true;
		}


    }
}