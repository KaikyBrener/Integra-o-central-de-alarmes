using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CentralHub.Api.Data;

/// <summary>
/// Aplica as migrations do EF Core na inicialização. Também adota bancos criados por versões
/// anteriores, que usavam EnsureCreated e por isso não têm a tabela __EFMigrationsHistory:
/// em vez de recriar as tabelas, a migration inicial é registrada como já aplicada.
/// </summary>
public static class DatabaseMigrator
{
    public static void Aplicar(AppDbContext context)
    {
        if (BancoCriadoSemMigrations(context))
        {
            AdotarBancoExistente(context);
        }

        context.Database.Migrate();
    }

    private static bool BancoCriadoSemMigrations(AppDbContext context) =>
        TabelaExiste(context, "Centrals") && !TabelaExiste(context, "__EFMigrationsHistory");

    private static void AdotarBancoExistente(AppDbContext context)
    {
        // Bancos anteriores ao campo Sucesso não têm a coluna que a migration inicial já considera existente.
        // Os registros antigos eram sempre gravados como bem-sucedidos.
        if (!ColunaExiste(context, "Histories", "Sucesso"))
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Histories ADD COLUMN Sucesso INTEGER NOT NULL DEFAULT 1");
        }

        var migrationInicial = context.Database.GetMigrations().First();
        var historico = context.GetService<IHistoryRepository>();
        context.Database.ExecuteSqlRaw(historico.GetCreateIfNotExistsScript());
        context.Database.ExecuteSqlRaw(historico.GetInsertScript(new HistoryRow(migrationInicial, VersaoDoEfCore())));
    }

    private static string VersaoDoEfCore()
    {
        var versao = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "9.0.0";
        return versao.Split('+')[0];
    }

    private static bool TabelaExiste(AppDbContext context, string tabela) =>
        ContarLinhas(context, $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{tabela}'") > 0;

    private static bool ColunaExiste(AppDbContext context, string tabela, string coluna) =>
        ContarLinhas(context, $"SELECT COUNT(*) FROM pragma_table_info('{tabela}') WHERE name = '{coluna}'") > 0;

    private static int ContarLinhas(AppDbContext context, string sql)
    {
        var conexao = context.Database.GetDbConnection();
        var abriu = conexao.State != System.Data.ConnectionState.Open;
        if (abriu)
        {
            conexao.Open();
        }

        try
        {
            using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            return Convert.ToInt32(comando.ExecuteScalar());
        }
        finally
        {
            if (abriu)
            {
                conexao.Close();
            }
        }
    }
}
