namespace SGDataModel
{
    public class Defaults
    {
        public static string Server { get; } = "localhost";
		public static string DatabaseName { get; } = "SGForum";
		public static string DBAdmin { get; } = "root";
		public static string DBPassword { get; } = "pwd";
		public static string UserAdmin { get; } = "admin@localhost";
		public static string UserAdminPassword { get; } = "password";
		public static string TopicTitle { get; } = "Root Topic";
		public static string PostTitle { get; } = "Welcome to the Forum";
		public static string PostBody { get; } = "Welcome to the Forum, longer text";

    }
}