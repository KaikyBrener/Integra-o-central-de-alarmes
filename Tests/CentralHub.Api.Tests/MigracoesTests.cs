using CentralHub.Api.Data;
using CentralHub.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CentralHub.Api.Tests;

/// <summary>
/// Cobre a inicialização do banco por migrations, inclusive a adoção de bancos criados
/// por versões anteriores com EnsureCreated (sem a tabela __EFMigrationsHistory).
/// </summary>
public class MigracoesTests
{
    private const string MigrationInicial = "InitialCreate";

    /// <summary>Banco SQLite em memória, mantido vivo por uma conexão aberta enquanto o teste usa.</summary>
    private sealed class BancoDeTeste : IDisposable
    {
        private readonly string _connectionString = $"Data Source=file:migracao_{Guid.NewGuid():N}?mode=memory&cache=shared";
        private readonly SqliteConnection _conexaoMantida;

        public BancoDeTeste()
        {
            _conexaoMantida = new SqliteConnection(_connectionString);
            _conexaoMantida.Open();
        }

        public AppDbContext NovoContexto() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connectionString).Options, ApiFactory.DataProtection);

        public void Executar(string sql)
        {
            using var comando = _conexaoMantida.CreateCommand();
            comando.CommandText = sql;
            comando.ExecuteNonQuery();
        }

        public long Contar(string sql)
        {
            using var comando = _conexaoMantida.CreateCommand();
            comando.CommandText = sql;
            return Convert.ToInt64(comando.ExecuteScalar());
        }

        /// <summary>Reproduz um banco criado por EnsureCreated, que não tem __EFMigrationsHistory.</summary>
        public void CriarBancoLegado(bool comColunaSucesso)
        {
            using (var contexto = NovoContexto())
            {
                contexto.Database.EnsureCreated();
            }

            if (!comColunaSucesso)
            {
                Executar("ALTER TABLE Histories DROP COLUMN Sucesso");
            }

            Executar("INSERT INTO Buildings (Id, Nome) VALUES (1, 'Prédio Antigo')");
            Executar("INSERT INTO Centrals (Id, Nome, IP, Porta, Usuario, Senha, BuildingId, Fabricante) " +
                     "VALUES (7, 'Central Antiga', '10.0.0.7', 9009, 'admin', 'senha-em-texto-simples', 1, 'Intelbras')");
            Executar(comColunaSucesso
                ? "INSERT INTO Histories (Id, Data, CentralId, PGM, Comando, Sucesso, Resultado) VALUES (3, '2025-01-02 03:04:05', 7, 2, 'Ligar', 1, 'PGM 2 ligado')"
                : "INSERT INTO Histories (Id, Data, CentralId, PGM, Comando, Resultado) VALUES (3, '2025-01-02 03:04:05', 7, 2, 'Ligar', 'PGM 2 ligado')");
        }

        public void Dispose() => _conexaoMantida.Dispose();
    }

    private static void AplicarMigrations(BancoDeTeste banco)
    {
        using var contexto = banco.NovoContexto();
        DatabaseMigrator.Aplicar(contexto);
    }

    [Fact]
    public void BancoNovo_CriaOEsquemaPelasMigrations()
    {
        using var banco = new BancoDeTeste();

        AplicarMigrations(banco);

        Assert.Equal(1, banco.Contar($"SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%{MigrationInicial}'"));
        Assert.Equal(1, banco.Contar("SELECT COUNT(*) FROM pragma_table_info('Histories') WHERE name = 'Sucesso'"));

        using var contexto = banco.NovoContexto();
        contexto.Buildings.Add(new Building { Nome = "Novo" });
        contexto.SaveChanges();
        Assert.Single(contexto.Buildings);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BancoCriadoComEnsureCreated_EhAdotadoSemRecriarAsTabelas(bool comColunaSucesso)
    {
        using var banco = new BancoDeTeste();
        banco.CriarBancoLegado(comColunaSucesso);

        AplicarMigrations(banco);

        // A migration inicial foi registrada como aplicada, uma única vez.
        Assert.Equal(1, banco.Contar($"SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%{MigrationInicial}'"));

        // Os dados existentes continuam lá: as tabelas não foram recriadas.
        Assert.Equal(1, banco.Contar("SELECT COUNT(*) FROM Buildings WHERE Nome = 'Prédio Antigo'"));
        Assert.Equal(1, banco.Contar("SELECT COUNT(*) FROM Centrals WHERE Id = 7"));
        Assert.Equal(1, banco.Contar("SELECT COUNT(*) FROM Histories WHERE Id = 3"));

        // A coluna Sucesso existe e o histórico antigo continua marcado como bem-sucedido.
        Assert.Equal(1, banco.Contar("SELECT COUNT(*) FROM pragma_table_info('Histories') WHERE name = 'Sucesso'"));
        Assert.Equal(1, banco.Contar("SELECT Sucesso FROM Histories WHERE Id = 3"));
    }

    [Fact]
    public void BancoLegado_ContinuaLegivelPeloEF_ESenhaAntigaEmTextoSimplesFuncionaAteSerRegravada()
    {
        using var banco = new BancoDeTeste();
        banco.CriarBancoLegado(comColunaSucesso: false);
        AplicarMigrations(banco);

        using var contexto = banco.NovoContexto();
        var central = contexto.Centrals.Single();
        Assert.Equal("senha-em-texto-simples", central.Senha);
        Assert.True(contexto.Histories.Single().Sucesso);

        central.Senha = "senha-nova";
        contexto.SaveChanges();
        Assert.StartsWith("enc:v1:", (string)ValorBruto(banco, "SELECT Senha FROM Centrals WHERE Id = 7"));
    }

    [Fact]
    public void BancoLegadoAdotado_AceitaNovosRegistros()
    {
        using var banco = new BancoDeTeste();
        banco.CriarBancoLegado(comColunaSucesso: false);
        AplicarMigrations(banco);

        using var contexto = banco.NovoContexto();
        contexto.Histories.Add(new History { Data = DateTime.UtcNow, CentralId = 7, PGM = 1, Comando = "Desligar", Sucesso = false, Resultado = "falhou" });
        contexto.SaveChanges();

        Assert.Equal(1, banco.Contar("SELECT COUNT(*) FROM Histories WHERE Sucesso = 0"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AplicarDuasVezes_NaoFalhaNemDuplicaOHistorico(bool bancoLegado)
    {
        using var banco = new BancoDeTeste();
        if (bancoLegado)
        {
            banco.CriarBancoLegado(comColunaSucesso: true);
        }

        AplicarMigrations(banco);
        AplicarMigrations(banco);

        Assert.Equal(1, banco.Contar("SELECT COUNT(*) FROM __EFMigrationsHistory"));
    }

    [Fact]
    public void ModeloAtualCorrespondeAoSnapshotDasMigrations()
    {
        // Se alguém mudar um model sem criar a migration, este teste falha.
        using var banco = new BancoDeTeste();
        using var contexto = banco.NovoContexto();

        Assert.False(contexto.Database.HasPendingModelChanges());
    }

    private static object ValorBruto(BancoDeTeste banco, string sql)
    {
        using var contexto = banco.NovoContexto();
        var conexao = contexto.Database.GetDbConnection();
        conexao.Open();
        try
        {
            using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            return comando.ExecuteScalar()!;
        }
        finally
        {
            conexao.Close();
        }
    }
}
