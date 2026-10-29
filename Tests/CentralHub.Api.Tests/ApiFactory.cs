using CentralHub.Api.Services;
using CentralHub.SDK.Adapters;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CentralHub.Api.Tests;

/// <summary>
/// Sobe a API em memória com SQLite em memória. Cada instância tem o seu próprio banco
/// (cache compartilhado, mantido vivo por uma conexão aberta) e não roda o KeepAliveService.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "chave-de-teste";
    public const string FabricanteComFalha = "Falha";

    // O modelo do EF é montado uma vez por processo e guarda o protector do primeiro contexto,
    // então todas as factories dos testes precisam compartilhar o mesmo provider.
    private static readonly IDataProtectionProvider DataProtection = new EphemeralDataProtectionProvider();

    private readonly string _ambiente;
    private readonly bool _usarAdapterFake;
    private readonly string _connectionString = $"Data Source=file:centralhub_{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _conexaoMantida;

    public ApiFactory() : this("Testing", usarAdapterFake: false)
    {
    }

    // internal: o xUnit exige um único construtor público para usar a classe como fixture.
    internal ApiFactory(string ambiente, bool usarAdapterFake)
    {
        _ambiente = ambiente;
        _usarAdapterFake = usarAdapterFake;
        _conexaoMantida = new SqliteConnection(_connectionString);
        _conexaoMantida.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_ambiente);
        builder.UseSetting("ApiKey", ApiKey);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("UsarAdapterFake", _usarAdapterFake.ToString());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDataProtectionProvider>();
            services.AddSingleton(DataProtection);

            services.RemoveAll<IAdapterResolver>();
            services.AddSingleton<IAdapterResolver, ResolverDeTeste>();

            var keepAlive = services.Where(s => s.ImplementationType == typeof(KeepAliveService)).ToList();
            foreach (var descriptor in keepAlive)
            {
                services.Remove(descriptor);
            }
        });
    }

    /// <summary>Cria um client que já envia a chave de API (ou outra, se informada).</summary>
    public HttpClient CriarClient(string? chave = ApiKey)
    {
        var client = CreateClient();
        if (chave != null)
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", chave);
        }
        return client;
    }

    /// <summary>Lê a senha exatamente como está gravada no banco, sem passar pelo EF.</summary>
    public string LerSenhaArmazenada(int centralId)
    {
        using var comando = _conexaoMantida.CreateCommand();
        comando.CommandText = "SELECT Senha FROM Centrals WHERE Id = $id";
        comando.Parameters.AddWithValue("$id", centralId);
        return (string)comando.ExecuteScalar()!;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _conexaoMantida.Dispose();
        }
    }

    private class ResolverDeTeste : IAdapterResolver
    {
        public ICentralAdapter Resolver(string? fabricante) =>
            fabricante == FabricanteComFalha ? new AdapterComFalha() : new AdapterResolver().Resolver(fabricante);
    }

    /// <summary>Adapter cuja central sempre recusa os comandos.</summary>
    private class AdapterComFalha : ICentralAdapter
    {
        private static readonly ComandoResult Recusado = new() { Sucesso = false, Resultado = "Central recusou o comando" };

        public Task<bool> Conectar(string ip, int porta, string usuario, string senha) => Task.FromResult(false);

        public Task<ConexaoResult> VerificarConectividade(string ip, int porta, string usuario, string senha) =>
            Task.FromResult(new ConexaoResult { Sucesso = false, Status = "Offline", Erro = "Central recusou a conexão" });

        public Task<ComandoResult> AcionarPGM(string ip, int porta, string usuario, string senha, int pgm) => Task.FromResult(Recusado);

        public Task<ComandoResult> DesligarPGM(string ip, int porta, string usuario, string senha, int pgm) => Task.FromResult(Recusado);

        public Task<ComandoResult> PulsoPGM(string ip, int porta, string usuario, string senha, int pgm, int tempoMs) => Task.FromResult(Recusado);
    }
}
