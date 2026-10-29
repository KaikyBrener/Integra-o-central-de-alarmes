using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CentralHub.Api.Data;

/// <summary>
/// Usada apenas pelas ferramentas do EF (dotnet ef migrations ...). Evita subir a aplicação
/// inteira, que exige ApiKey, e usa um provider efêmero porque a criptografia da senha não
/// altera o esquema do banco.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=centralhub.db")
            .Options;

        return new AppDbContext(options, new EphemeralDataProtectionProvider());
    }
}
