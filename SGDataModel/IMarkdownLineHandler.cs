namespace SGMarkdown
{
    public interface IMarkdownLineHandler
	{
		bool HandleLine(MarkdownNode context, string line);
	}
}