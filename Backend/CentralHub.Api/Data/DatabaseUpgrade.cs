using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Data;

/// <summary>
/// O projeto usa EnsureCreated (sem migrations), que não altera tabelas já
/// existentes. Este passo adiciona colunas novas a bancos criados por versões
/// anteriores, para que o upgrade não quebre o banco local de quem já usava o app.
/// </summary>
public static class DatabaseUpgrade
{
    public static void Aplicar(AppDbContext context)
    {
        if (!ColunaExiste(context, "Histories", "Sucesso"))
        {
            // Registros antigos eram sempre gravados como bem-sucedidos.
            context.Database.ExecuteSqlRaw("ALTER TABLE Histories ADD COLUMN Sucesso INTEGER NOT NULL DEFAULT 1");
        }
    }

    private static bool ColunaExiste(AppDbContext context, string tabela, string coluna)
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
            comando.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tabela}') WHERE name = '{coluna}'";
            return Convert.ToInt32(comando.ExecuteScalar()) > 0;
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
