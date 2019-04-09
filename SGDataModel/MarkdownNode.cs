using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace SGMarkdown
{
    	public enum MarkdownType
	{
		Root,
		Heading1,
		Heading2,
		Heading3,
		Bulleted,
		Numbered,
		BlankLine,
		Paragraph,
		Text,
		Bold,
		Italic,
		Underline
	};

	public class MarkdownNode
	{
		private static List<MarkdownNode> _emptyList = new List<MarkdownNode>();
		public string Text { get; set; }
		public MarkdownType Type { get; set; }

		public MarkdownNode Parent { get; internal set; }

		private List<MarkdownNode> _children;
		public IReadOnlyList<MarkdownNode> Children 
		{
			get
			{
				if (_children == null)
					return _emptyList;
				return _children;
			}
		}

		public bool HasChildren
		{
			get
			{
				return _children != null;
			}
		}

		public bool IsInlineModifier
		{
			get
			{
				return Type == MarkdownType.Bold || Type == MarkdownType.Italic || Type == MarkdownType.Underline;
			}
		}

		public void AddChild(MarkdownNode child)
		{
			if (child.Parent != null)
				child.Parent.RemoveChild(child);
			child.Parent = this;
			if (_children == null)
				_children = new List<MarkdownNode>();
			_children.Add(child);
		}

		public void RemoveChild(MarkdownNode child)
		{
			if (child.Parent == this)
			{
				_children.Remove(child);
				child.Parent = null;
				if (_children.Count == 0)
					_children = null;
			}
		}
	}

}