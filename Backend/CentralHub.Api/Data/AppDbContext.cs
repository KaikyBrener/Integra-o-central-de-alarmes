using CentralHub.Api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Data;

public class AppDbContext : DbContext
{
    private readonly IDataProtector _senhaProtector;

    public AppDbContext(DbContextOptions<AppDbContext> options, IDataProtectionProvider dataProtectionProvider)
        : base(options)
    {
        _senhaProtector = dataProtectionProvider.CreateProtector("CentralHub.Central.Senha");
    }

    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Central> Centrals => Set<Central>();
    public DbSet<History> Histories => Set<History>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Building>(entity =>
        {
            entity.Property(b => b.Nome).IsRequired().HasMaxLength(150);
            entity.Property(b => b.Descricao).HasMaxLength(500);
        });

        modelBuilder.Entity<Central>(entity =>
        {
            entity.Property(c => c.Nome).IsRequired().HasMaxLength(150);
            entity.Property(c => c.IP).IsRequired().HasMaxLength(45);
            entity.Property(c => c.Usuario).IsRequired().HasMaxLength(100);
            // O valor armazenado é a senha criptografada (maior que o texto original).
            // O modelo do EF é montado uma vez por processo, então o protector usado aqui
            // é o do primeiro contexto criado; a aplicação tem um único IDataProtectionProvider.
            entity.Property(c => c.Senha)
                  .IsRequired()
                  .HasMaxLength(1000)
                  .HasConversion(new SenhaCriptografadaConverter(_senhaProtector));
            entity.Property(c => c.Fabricante).HasMaxLength(100);
            entity.Property(c => c.Modelo).HasMaxLength(100);
            entity.Property(c => c.Firmware).HasMaxLength(100);
            entity.Property(c => c.Status).HasMaxLength(50);

            entity.HasOne(c => c.Building)
                  .WithMany()
                  .HasForeignKey(c => c.BuildingId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<History>(entity =>
        {
            entity.Property(h => h.Comando).IsRequired().HasMaxLength(50);
            entity.Property(h => h.Resultado).HasMaxLength(500);

            entity.HasOne(h => h.Central)
                  .WithMany()
                  .HasForeignKey(h => h.CentralId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
