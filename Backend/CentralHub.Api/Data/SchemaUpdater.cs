using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Data;

/// <summary>
/// O projeto usa <c>EnsureCreated</c> (sem migrations), que NÃO adiciona tabelas a um banco já existente.
/// Este atualizador cria, de forma idempotente, as tabelas acrescentadas depois do MVP, para que bancos
/// antigos (centralhub.db) continuem funcionando sem precisar ser apagados.
/// </summary>
public static class SchemaUpdater
{
    public static void Aplicar(AppDbContext context)
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Events" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Events" PRIMARY KEY AUTOINCREMENT,
                "DataHora" TEXT NOT NULL,
                "CentralId" INTEGER NULL,
                "Fabricante" TEXT NULL,
                "Serial" TEXT NULL,
                "Conta" TEXT NULL,
                "Codigo" TEXT NULL,
                "Tipo" TEXT NOT NULL,
                "Restauracao" INTEGER NOT NULL,
                "Particao" INTEGER NULL,
                "Zona" INTEGER NULL,
                "Usuario" INTEGER NULL,
                "Pgm" INTEGER NULL,
                "Descricao" TEXT NULL,
                "Bruto" TEXT NULL,
                CONSTRAINT "FK_Events_Centrals_CentralId" FOREIGN KEY ("CentralId") REFERENCES "Centrals" ("Id") ON DELETE SET NULL
            );
            """);
        context.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_Events_DataHora" ON "Events" ("DataHora");""");
        context.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_Events_CentralId" ON "Events" ("CentralId");""");
    }
}
