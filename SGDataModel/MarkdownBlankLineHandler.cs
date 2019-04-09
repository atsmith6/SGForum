namespace SGMarkdown
{
    public class MarkdownBlankLineHandler : IMarkdownLineHandler
    {
		public bool HandleLine(MarkdownNode context, string line)
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				context.AddChild(new MarkdownNode()
				{
					Type = MarkdownType.BlankLine
				});
				return true;
			}
			return false;
		}
    }
}