using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fel.Infrastructure.Data;

public sealed class FelDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FelDbContext>
{
    public FelDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FEL_DESIGNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "FEL_DESIGNTIME_CONNECTION debe contener la cadena de conexión para crear o actualizar migraciones.");
        }

        var options = new DbContextOptionsBuilder<FelDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(FelDbContext).Assembly.FullName))
            .Options;

        return new FelDbContext(options);
    }
}
