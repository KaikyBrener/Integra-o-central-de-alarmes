using CentralHub.SDK.Adapters;

namespace CentralHub.Api.Services;

/// <summary>
/// Resolve o adapter de uma Central a partir do fabricante salvo. Existe para
/// que os services não dependam diretamente da factory estática e possam ser
/// testados com adapters que simulam falha.
/// </summary>
public interface IAdapterResolver
{
    /// <summary>
    /// Indica se o FakeAdapter pode ser usado: só em Development e com UsarAdapterFake verdadeiro.
    /// </summary>
    bool PermitirFakeAdapter { get; }

    /// <summary>
    /// Devolve o adapter do fabricante salvo na Central, ou null quando o fabricante não é
    /// reconhecido (vazio, "Fake" ou desconhecido) e o FakeAdapter não é permitido.
    /// </summary>
    ICentralAdapter? Resolver(string? fabricante);
}

public class AdapterResolver : IAdapterResolver
{
    public AdapterResolver(IConfiguration configuration, IHostEnvironment environment)
    {
        // O FakeAdapter só pode ser usado em desenvolvimento e quando habilitado
        // explicitamente (UsarAdapterFake, padrão false).
        PermitirFakeAdapter = environment.IsDevelopment() && configuration.GetValue<bool>("UsarAdapterFake");
    }

    public bool PermitirFakeAdapter { get; }

    public virtual ICentralAdapter? Resolver(string? fabricante)
    {
        var tipo = AdapterFactory.ResolverPorNome(fabricante);

        if (tipo is FabricanteTipo.Intelbras or FabricanteTipo.Jfl)
        {
            return AdapterFactory.Criar(tipo.Value);
        }

        return PermitirFakeAdapter ? AdapterFactory.Criar(FabricanteTipo.Fake) : null;
    }
}
