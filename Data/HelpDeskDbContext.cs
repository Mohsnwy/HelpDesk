using HelpDesk.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Data;

public class HelpDeskDbContext(DbContextOptions<HelpDeskDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Department> Departments => Set<Department>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.FullName).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(user => user.Department).WithMany(department => department.Users)
                .HasForeignKey(user => user.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.Property(department => department.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(department => department.Name).IsUnique();
            entity.HasData(
                new Department { Id = 1, Name = "Engineering" },
                new Department { Id = 2, Name = "Finance" },
                new Department { Id = 3, Name = "Human Resources" },
                new Department { Id = 4, Name = "Operations" });
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(category => category.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(category => category.Name).IsUnique();
            entity.HasData(
                new Category { Id = 1, Name = "IT Support" },
                new Category { Id = 2, Name = "Access Request" },
                new Category { Id = 3, Name = "Facilities" },
                new Category { Id = 4, Name = "Human Resources" });
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.Property(ticket => ticket.Title).HasMaxLength(200).IsRequired();
            entity.Property(ticket => ticket.Description).HasMaxLength(4000).IsRequired();
            entity.Property(ticket => ticket.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(ticket => ticket.Priority).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(ticket => ticket.Status);
            entity.HasIndex(ticket => ticket.AssigneeId);
            entity.HasIndex(ticket => ticket.RequesterId);
            entity.HasIndex(ticket => ticket.CreatedAt);
            entity.HasOne(ticket => ticket.Requester).WithMany(user => user.RequestedTickets)
                .HasForeignKey(ticket => ticket.RequesterId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(ticket => ticket.Assignee).WithMany(user => user.AssignedTickets)
                .HasForeignKey(ticket => ticket.AssigneeId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(ticket => ticket.Category).WithMany(category => category.Tickets)
                .HasForeignKey(ticket => ticket.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TicketComment>(entity =>
        {
            entity.Property(comment => comment.Body).HasMaxLength(4000).IsRequired();
            entity.HasIndex(comment => new { comment.TicketId, comment.CreatedAt });
            entity.HasOne(comment => comment.Ticket).WithMany(ticket => ticket.Comments)
                .HasForeignKey(comment => comment.TicketId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(comment => comment.Author).WithMany(user => user.Comments)
                .HasForeignKey(comment => comment.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
