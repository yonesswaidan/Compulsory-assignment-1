using Microsoft.EntityFrameworkCore;

namespace CommentService.Data
{
    public class CommentDbContext : DbContext
    {
        public CommentDbContext(DbContextOptions<CommentDbContext> options)
            : base(options)
        {
        }

        public DbSet<Comment> Comments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Comment>(entity =>
            {
                entity.ToTable("comments");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ArticleId).HasColumnName("articleid");
                entity.Property(e => e.Author).HasColumnName("author");
                entity.Property(e => e.Text).HasColumnName("content");
                entity.Property(e => e.CreatedUtc).HasColumnName("createdutc");
            });
        }
    }

    public class Comment
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string Author { get; set; }
        public string Text { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
