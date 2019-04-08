using System;
using Microsoft.EntityFrameworkCore;
using MySql.Data.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Proxies;
using System.Linq;

namespace SGDataModel
{
    public class SGContext : DbContext
    {
		private string _connectionString { get; set; }

        public DbSet<DatabaseInfo> databaseInfo { get; set; }
        public DbSet<User> users { get; set; }
		public DbSet<LoginToken> tokens { get; set; }
		public DbSet<Topic> topics { get; set; }

		public DbSet<Post> posts { get; set; }

        public SGContext(IConfiguration config)
        {
			_connectionString = $"server={Defaults.Server};database={Defaults.DatabaseName};"+
				$"user={Defaults.DBAdmin};"+
				$"password={Defaults.DBPassword};"+
				"TreatTinyAsBoolean=false";
			var cs = config == null ? null : config["connectionString"];
			if(cs != null)
				_connectionString = cs;
        }

		public static SGContext CreateAndInitialise(IConfiguration config)
        {
            SGContext context = new SGContext(config);
			if (context.Database.EnsureCreated())
			{
				context.PopulateWithInitialData();
				string createTestData = config["createTestData"] ?? "no";
				if (createTestData.Equals("yes", StringComparison.OrdinalIgnoreCase))
				{
					var dataCreator = new TestDataCreator(context);
					dataCreator.Execute();
				}
			}
			return context;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLazyLoadingProxies()
				.UseMySQL(_connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
			// This .Property(u => u.Active).HasColumnType("tinyint(1)"); is required for MySQL
			// to work with bool fields.
			modelBuilder.Entity<User>().Property(u => u.Active).HasColumnType("tinyint(1)");
			modelBuilder.Entity<LoginToken>().HasIndex(t => t.UserId).IsUnique();
			modelBuilder.Entity<Topic>().HasIndex(t => t.ParentId).IsUnique(false);
			modelBuilder.Entity<Topic>().Property(u => u.IsRootEntry).HasColumnType("tinyint(1)");
			
			// You can't cascade delete a self referential table so the below won't work!

			modelBuilder.Entity<Topic>().HasOne(t => t.Parent)
			 		.WithMany(t => t.Children).OnDelete(DeleteBehavior.SetNull);
			modelBuilder.Entity<Post>().HasOne(p => p.Parent)
					.WithMany(t => t.Posts).OnDelete(DeleteBehavior.Cascade);
				

			modelBuilder.Entity<Post>().HasIndex(p => p.ParentId).IsUnique(false);
			modelBuilder.Entity<Post>().Property(p => p.Hidden).HasColumnType("tinyint(1)");
            //modelBuilder.Entity<DatabaseInfo>().HasData(new DatabaseInfo { Id = -1, MajVersion = 1, MinVersion = 0 });
        }

        private void PopulateWithInitialData()
        {
			var task = UserTasks.CreateUserNoAuthCheckAsync(this, Defaults.UserAdmin, 
				"Administrator", Defaults.UserAdminPassword, UserRole.Admin);
			task.Wait();
			var adminUser = task.Result;
			SaveChanges();

			var rootTopic = new Topic()
			{
				Title = "Root Topic",
				RoleToRead = UserRole.Guest,
				RoleToEdit = UserRole.Admin,
				IsRootEntry = true
			};

			var now = DateTime.UtcNow;
			var rootPost = new Post()
			{
				Parent = rootTopic,
				Modified = now,
				Created = now,
				Title = Defaults.PostTitle,
				Body = Defaults.PostBody,
				User = adminUser,
				RoleToRead = UserRole.Guest,
				RoleToEdit = UserRole.Admin
			};

			topics.Add(rootTopic);
			posts.Add(rootPost);
			SaveChanges();

			var dbInfo = new DatabaseInfo 
			{ 
				MajVersion = 1,
				MinVersion = 2,
				ForumTitle = "New Forum",
				RootTopicId = rootTopic.Id
			};
			databaseInfo.Add(dbInfo);
			SaveChanges();
        }
    }
}