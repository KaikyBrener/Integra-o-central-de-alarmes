using CentralHub.SDK.Adapters;

namespace CentralHub.Api.Services;

/// <summary>
/// Resolve o adapter de uma Central a partir do fabricante salvo. Existe para
/// que os services não dependam diretamente da factory estática e possam ser
/// testados com adapters que simulam falha.
/// </summary>
public interface IAdapterResolver
{
    ICentralAdapter Resolver(string? fabricante);
}

public class AdapterResolver : IAdapterResolver
{
    public ICentralAdapter Resolver(string? fabricante) =>
        AdapterFactory.Criar(AdapterFactory.ResolverPorNome(fabricante));
}
