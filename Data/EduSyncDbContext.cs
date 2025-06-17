using Microsoft.EntityFrameworkCore;
using EduSyncAPI.Models;

public class EduSyncDbContext : DbContext
{
    public EduSyncDbContext(DbContextOptions<EduSyncDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Assessment> Assessments { get; set; }
    public DbSet<Result> Results { get; set; }
    public DbSet<CourseMaterial> CourseMaterial { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
} 