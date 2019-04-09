using System;
using NUnit.Framework;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Data.SqlClient;
using SGMarkdown;
using MySql.Data.MySqlClient;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

namespace SGDataModel.Tests
{
	[TestFixture]
    public class TestSGMarkdown
    {
        [Test]
		public void DetectsHeaders()
		{
			string markdown = @"
				#Heading No Space
				# Heading With Space
				##Heading level 2
				###		Heading level 3	
				";

			Markdown md = new Markdown(markdown);
			MDTester tester = new MDTester(md);
			Func<MarkdownNode, bool> isHeading = (n) => n.Type == MarkdownType.Heading1 || n.Type == MarkdownType.Heading2 || n.Type == MarkdownType.Heading3;

			tester.Next(n => isHeading(n)).Test((node) =>
			{
				Assert.IsTrue(node.Type == MarkdownType.Heading1, "Heading 1 not correct");
				Assert.IsTrue(node.Text == "Heading No Space", "Heading 1 not correct");
			});

			tester.Next(n => isHeading(n)).Test((node) =>
			{
				Assert.IsTrue(node.Type == MarkdownType.Heading1, "Heading 2 not correct");
				Assert.IsTrue(node.Text == "Heading With Space", "Heading 2 not correct");
			});

			tester.Next(n => isHeading(n)).Test((node) =>
			{
				Assert.IsTrue(node.Type == MarkdownType.Heading2, "Heading 3 not correct");
				Assert.IsTrue(node.Text == "Heading level 2", "Heading 3 not correct");
			});

			tester.Next(n => isHeading(n)).Test((node) =>
			{
				Assert.IsTrue(node.Type == MarkdownType.Heading3, "Heading 4 not correct");
				Assert.IsTrue(node.Text == "Heading level 3", "Heading 4 not correct");
			});
		}

		[Test]
		public void DetectsBullets()
		{
			string markdown = @"
				- Item 1
				- Item 2
				- Item 3

				#. Enum 1
				#. Enum 2
				#. Enum 3
				";

			Markdown md = new Markdown(markdown);
			MDTester tester = new MDTester(md);

			tester.Next(n => n.Type == MarkdownType.Bulleted).Test((node) =>
			{
				Assert.IsTrue(node.Text == "Item 1", "Bullet item was not found");
			});

			tester.Next(n => n.Type == MarkdownType.Bulleted).Test((node) =>
			{
				Assert.IsTrue(node.Text == "Item 2", "Bullet item was not found");
			});

			tester.Next(n => n.Type == MarkdownType.Bulleted).Test((node) =>
			{
				Assert.IsTrue(node.Text == "Item 3", "Bullet item was not found");
			});

			tester.Next(n => n.Type == MarkdownType.Numbered).Test((node) =>
			{
				Assert.IsTrue(node.Text == "Enum 1", "Bullet item was not found");
			});

			tester.Next(n => n.Type == MarkdownType.Numbered).Test((node) =>
			{
				Assert.IsTrue(node.Text == "Enum 2", "Bullet item was not found");
			});

			tester.Next(n => n.Type == MarkdownType.Numbered).Test((node) =>
			{
				Assert.IsTrue(node.Text == "Enum 3", "Bullet item was not found");
			});
		}

		[Test]
		public void DetectsParasAndLineBreaks()
		{
			string markdown = @"
				para 1
				para 2
				
				para 3
";

			Markdown md = new Markdown(markdown);
			MDTester tester = new MDTester(md);

			tester.Next(n => n.Type == MarkdownType.Paragraph || n.Type == MarkdownType.BlankLine).Test((node) =>
			{
				Assert.IsTrue(node.Type == MarkdownType.BlankLine);
				Assert.IsTrue(node.Text == null);
			});

			tester.Next(n => n.Type == MarkdownType.Paragraph || n.Type == MarkdownType.BlankLine).Test((node) =>
			{
				Assert.IsTrue(node.Type == MarkdownType.Paragraph);
				Assert.IsTrue(node.Text == null);
				Assert.IsTrue(node.HasChildren);
				Assert.IsTrue(node.Children[0].Text == "para 1");
				Assert.IsTrue(node.Children[0].Type == MarkdownType.Text);
			});

			tester.Next(n => n.Type == MarkdownType.Paragraph || n.Type == MarkdownType.BlankLine).Test((node) =>
			{
				Assert.IsTrue(node.Type == MarkdownType.Paragraph);
				Assert.IsTrue(node.Text == null);
				Assert.IsTrue(node.HasChildren);
				Assert.IsTrue(node.Children[0].Text == "para 2");
				Assert.IsTrue(node.Children[0].Type == MarkdownType.Text);
			});

			tester.Next(n => n.Type == MarkdownType.Paragraph || n.Type == MarkdownType.BlankLine).Test((node) =>
			{
				Assert.IsTrue(node.Type == MarkdownType.BlankLine);
				Assert.IsTrue(node.Text == null);
			});

			tester.Next(n => n.Type == MarkdownType.Paragraph || n.Type == MarkdownType.BlankLine).Test((node) =>
			{
				Assert.IsTrue(node.Type == MarkdownType.Paragraph);
				Assert.IsTrue(node.Text == null);
				Assert.IsTrue(node.HasChildren);
				Assert.IsTrue(node.Children[0].Text == "para 3");
				Assert.IsTrue(node.Children[0].Type == MarkdownType.Text);
			});

			tester.Next(n => n.Type == MarkdownType.Paragraph || n.Type == MarkdownType.BlankLine).Test((node) =>
			{
				Assert.IsTrue(node.Type == MarkdownType.BlankLine);
				Assert.IsTrue(node.Text == null);
			});
		}

		[Test]
		public void ParaFormatting()
		{
			string markdown = @"
				para 1 *bold * ^italic^ _underline_
";

			Markdown md = new Markdown(markdown);
			MDTester tester = new MDTester(md);

			tester.Next().TestIsBlankLine();
			tester.Next().Test((para) => 
			{
				Assert.IsTrue(para.Type == MarkdownType.Paragraph);

				MDTester ptest = new MDTester(para);

				ptest.Next().Test((node) =>
				{
					Assert.IsTrue(node.Type == MarkdownType.Text);
					Assert.IsTrue(node.Text == "para 1 ");
				});

				ptest.Next().Test((node) =>
				{
					Assert.IsTrue(node.Type == MarkdownType.Bold);
					Assert.IsTrue(node.HasChildren);
					Assert.IsTrue(node.Children[0].Type == MarkdownType.Text);
					Assert.IsTrue(node.Children[0].Text == "bold ");
				});
				
				ptest.Next().Test((node) =>
				{
					Assert.IsTrue(node.Type == MarkdownType.Text);
					Assert.IsTrue(node.Text == " ");
				});

				ptest.Next().Test((node) =>
				{
					Assert.IsTrue(node.Type == MarkdownType.Italic);
					Assert.IsTrue(node.HasChildren);
					Assert.IsTrue(node.Children[0].Type == MarkdownType.Text);
					Assert.IsTrue(node.Children[0].Text == "italic");
				});

				ptest.Next().Test((node) =>
				{
					Assert.IsTrue(node.Type == MarkdownType.Text);
					Assert.IsTrue(node.Text == " ");
				});

				ptest.Next().Test((node) =>
				{
					Assert.IsTrue(node.Type == MarkdownType.Underline);
					Assert.IsTrue(node.HasChildren);
					Assert.IsTrue(node.Children[0].Type == MarkdownType.Text);
					Assert.IsTrue(node.Children[0].Text == "underline");
				});
			});
			tester.Next().TestIsBlankLine();
		}

		[Test]
		public void TestConvertToHTML()
		{
			string markdown = @"
			# My Heading

			Welcome to my *awesome _super*_ toggle style markdown
			This is just so cool

			Or maybe it's ^rubbish^

			- Who *knows*?
			- Who *c^a_r_e^s*?
			- Blah blah

			#. Item 1
			#. Item 2
			";

			Markdown md = new Markdown(markdown);

			string HTML = md.RenderRawHTML();

			System.Console.WriteLine(HTML);
		}
    }

	public class MDTester
	{
		class PosInfo
		{
			public MarkdownNode parent;
			public int currentIndex;
		}

		Stack<PosInfo> _stack = new Stack<PosInfo>();
		
		public MDTester(Markdown md)
		{
			var posInfo = new PosInfo()
			{
				parent = md.Root,
				currentIndex = -1
			};
			_stack.Push(posInfo);
		}

		public MDTester(MarkdownNode root)
		{
			var posInfo = new PosInfo()
			{
				parent = root,
				currentIndex = -1
			};
			_stack.Push(posInfo);
		}

		private MarkdownNode tryGetCurrent()
		{
			var posInfo = _stack.Peek();
			var currentParent = posInfo.parent;
			if (posInfo.currentIndex < 0 || 
				!currentParent.HasChildren || 
				posInfo.currentIndex >= currentParent.Children.Count)
			{
				return null;
			}
			return currentParent.Children[posInfo.currentIndex];
		}

		public void Push()
		{
			var node = tryGetCurrent();
			if (node != null)
			{
				var posInfo = new PosInfo()
				{
					parent = node,
					currentIndex = -1
				};
				_stack.Push(posInfo);
			}
		}

		public void Pop()
		{
			_stack.Pop();
		}

		public MarkdownNode Next()
		{
			var posInfo = _stack.Peek();
			if (posInfo.currentIndex < posInfo.parent.Children.Count)
				++posInfo.currentIndex;
			return tryGetCurrent();
		}

		public MarkdownNode Next(Func<MarkdownNode, bool> predicate)
		{
			var posInfo = _stack.Peek();
			int lastItem = posInfo.parent.Children.Count - 1;
			while (posInfo.currentIndex < lastItem)
			{
				++posInfo.currentIndex;
				var candidate = posInfo.parent.Children[posInfo.currentIndex];
				if (predicate(candidate))
					break;
			}
			return tryGetCurrent();
		}
	}

	public static class MarkdownNodeTestExtension
	{
		public static void Test(this MarkdownNode node, Action<MarkdownNode> theTest)
		{
			if (node == null)
				Assert.Fail("Markdown node was null when it shouldn't have been");
			theTest(node);
		}

		public static void TestIsBlankLine(this MarkdownNode node)
		{
			if (node == null)
				Assert.Fail("Null node.  Expected blank line");
			else if (node.Type != MarkdownType.BlankLine)
				Assert.Fail("Node not a blank line as expected");
		}
	}
}