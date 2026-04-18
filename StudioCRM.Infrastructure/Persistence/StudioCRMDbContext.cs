using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
namespace StudioCRM.Infrastructure.Persistence;

public class StudioCRMDbContext : DbContext
{
    public StudioCRMDbContext(DbContextOptions<StudioCRMDbContext> options)
        : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Trainer> Trainers => Set<Trainer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);

        modelBuilder.Entity<Trainer>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId);
        
        modelBuilder.Entity<Client>()
            .HasOne(c => c.Trainer)
            .WithMany(t => t.Clients)
            .HasForeignKey(c => c.TrainerId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Trainer>()
            .Property(t => t.HourlyRate)
            .HasPrecision(10, 2);
    }
}