using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace SGMarkdown
{

	

    public class Markdown
    {
		private MarkdownNode _root;
		private MarkdownNode _current;

		private List<IMarkdownLineHandler> _handlers = new List<IMarkdownLineHandler>()
		{
			new MarkdownHeadingHandler(),
			new MarkdownBulletsHandler(),
			new MarkdownBlankLineHandler(),
			new MarkdownParagraphHandler()
		};

		public MarkdownNode Root { get { return _root; } }

        public Markdown(string rawMarkdown)
		{
			_root = Parse(rawMarkdown);
		}

		public string RenderRawHTML()
		{
			StringBuilder sb = new StringBuilder();
			RenderNode(_root, sb);
			return sb.ToString();
		}

		private bool previousSatisfies(int i, MarkdownNode parent, Func<MarkdownNode, bool> predicate)
		{
			i -= 1;
			if (i < 0 || i >= parent.Children.Count)
				return false;
			var node = parent.Children[i];
			return predicate(node);
		}

		private bool nextSatisfies(int i, MarkdownNode parent, Func<MarkdownNode, bool> predicate)
		{
			i += 1;
			if (i < 0 || i >= parent.Children.Count)
				return false;
			var node = parent.Children[i];
			return predicate(node);
		}

		private void RenderNode(MarkdownNode node, StringBuilder builder)
		{
			string openTag = null;
			string closeTag = null;
			switch(node.Type)
			{
				case MarkdownType.Heading1: openTag = "<span style=\"font-size: 140%; font-weight: bold\">"; closeTag = "</span>"; break;
				case MarkdownType.Heading2: openTag = "<span style=\"font-size: 120%; font-weight: bold; font-style=italic;\">"; closeTag = "</span>"; break;
				case MarkdownType.Heading3: openTag = "<span style=\"font-size: 105%; font-weight: bold\">"; closeTag = "</span>"; break;
				// case MarkdownType.Bulleted: openTag = "<ul>"; closeTag = "</ul>"; break;
				// case MarkdownType.Numbered: openTag = "<ol>"; closeTag = "</ol>"; break;
				case MarkdownType.Bold: openTag = "<b>"; closeTag = "</b>"; break;
				case MarkdownType.Italic: openTag = "<i>"; closeTag = "</i>"; break;
				case MarkdownType.Underline: openTag = "<u>"; closeTag = "</u>"; break;
				//case MarkdownType.BlankLine: openTag = null; closeTag = "<br>"; break;
				default: break;
			}
			if (openTag != null)
				builder.Append(openTag);
			if (node.Text != null)
				builder.Append(WebUtility.HtmlEncode(node.Text));
			
			for(int i = 0; i < node.Children.Count; ++i)
			{
				var child = node.Children[i];
				if (child.Type == MarkdownType.Bulleted)
				{
					if (!previousSatisfies(i, node, n => n.Type == MarkdownType.Bulleted))
					{
						builder.AppendLine();
						builder.AppendLine("<ul>");
					}
					
					builder.Append("<li>");
					RenderNode(child, builder);
					builder.AppendLine("</li>");

					if (!nextSatisfies(i, node, n => n.Type == MarkdownType.Bulleted))
					{
						builder.AppendLine();
						builder.AppendLine("</ul>");
					}
				}
				else if (child.Type == MarkdownType.Numbered)
				{
					if (!previousSatisfies(i, node, n => n.Type == MarkdownType.Numbered))
					{
						builder.AppendLine();
						builder.AppendLine("<ol>");
					}

					builder.Append("<li>");
					RenderNode(child, builder);
					builder.AppendLine("</li>");

					if (!nextSatisfies(i, node, n => n.Type == MarkdownType.Numbered))
					{
						builder.AppendLine();
						builder.AppendLine("</ol>");
					}
				}
				else if (child.Type == MarkdownType.Paragraph)
				{
					if (!previousSatisfies(i, node, n => n.Type == MarkdownType.Paragraph))
					{
						builder.AppendLine();
						builder.AppendLine("<p>");
					}
					else
					{
						builder.AppendLine("<br>");
					}

					RenderNode(child, builder);

					if (!nextSatisfies(i, node, n => n.Type == MarkdownType.Paragraph))
					{
						builder.AppendLine();
						builder.AppendLine("</p>");
					}
				}
				else
				{
					RenderNode(child, builder);
				}
			}

			if (closeTag != null)
			{
				if (node.IsInlineModifier)
					builder.Append(closeTag);
				else
					builder.AppendLine(closeTag);
			}
		}

		private MarkdownNode Parse(string rawMarkdown)
		{
			MarkdownNode root = new MarkdownNode() { Type = MarkdownType.Root };
			_current = root;

			string[] lines = rawMarkdown.Split(new[] { "\r\n", "\r", "\n" },
    				StringSplitOptions.None );

			foreach (var line in lines)
			{
				foreach(var handler in _handlers)
				{
					if (handler.HandleLine(_current, line))
						break;
				}
			}

			return root;
		}
    }
}