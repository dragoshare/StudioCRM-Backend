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
}