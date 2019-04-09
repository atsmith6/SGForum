using System;
using System.Text;
using System.Collections.Generic;

namespace SGMarkdown
{
    public abstract class MarkdownTextParser
    {
        protected void AppendParsedTextTo(MarkdownNode parent, string line)
		{
			var ranges = Parse(line);

			Stack<MarkdownNode> stack = new Stack<MarkdownNode>();

			foreach(var range in ranges)
			{
				if (range.IsEmpty)
					throw new Exception("Unexpected empty markdown paragraph range.");
				stack.Clear();
				stack.Push(new MarkdownNode() { Type = MarkdownType.Text, Text = range.ExtractText(line) });
				if (range.Underline)
					stack.Push(new MarkdownNode() { Type = MarkdownType.Underline });
				if (range.Italic)
					stack.Push(new MarkdownNode() { Type = MarkdownType.Italic });
				if (range.Bold)
					stack.Push(new MarkdownNode() { Type = MarkdownType.Bold });
				var node = parent;
				while(stack.Count > 0)
				{
					var child = stack.Pop();
					node.AddChild(child);
					node = child;
				}
			}
		}

		protected class ParaRange
		{
			public ParaRange(int start)
			{
				Begin = start;
				End = start;
			}

			public bool IsEmpty { get { return End <= Begin; } }

			public bool Bold { get; set; }
			public bool Italic { get; set; }
			public bool Underline { get; set; }
			public int Begin { get; set; }
			public int End { get; set; }

			public ParaRange CreateNextInSequence()
			{
				ParaRange rng = new ParaRange(End);
				rng.Bold = Bold;
				rng.Underline = Underline;
				rng.Italic = Italic;
				return rng;
			}

			public string ExtractText(string line)
			{
				if (End <= Begin)
					return "";
				return line.Substring(Begin, End - Begin);
			}
		}

		protected List<ParaRange> Parse(string line)
		{
			var ranges = new List<ParaRange>();
			var rng = new ParaRange(0);

			while(true)
			{
				Collect(line, rng);
				if (rng.IsEmpty)
				{
					break;
				}
				else if (line[rng.Begin] == '*')
				{
					rng.Bold = !rng.Bold;
					rng = rng.CreateNextInSequence();
				}
				else if (line[rng.Begin] == '^')
				{
					rng.Italic = !rng.Italic;
					rng = rng.CreateNextInSequence();
				}
				else if (line[rng.Begin] == '_')
				{
					rng.Underline = !rng.Underline;
					rng = rng.CreateNextInSequence();
				}
				else
				{
					ranges.Add(rng);
					rng = rng.CreateNextInSequence();
				}
			}

			return ranges;
		}

		private void Collect(string line, ParaRange rng)
		{
			int len = line.Length;
			while(rng.End < len)
			{
				if (line[rng.End] == '*' || line[rng.End] == '_' || line[rng.End] == '^')
				{
					if (rng.Begin == rng.End)
						++rng.End;
					break;
				}
				++rng.End;
			}
		}
    }
}