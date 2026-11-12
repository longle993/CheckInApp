using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace CheckInApp.Infrastructure;

public class DesignFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=localhost;Database=CheckInApp;Trusted_Connection=True;TrustServerCertificate=True")
        .Options);
}
