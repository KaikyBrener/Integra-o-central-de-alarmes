using CentralHub.SDK.Adapters.Jfl;

namespace CentralHub.SDK.Adapters;

public enum FabricanteTipo
{
    Fake,
    Intelbras,
    Jfl,

    /// <summary>
    /// Fabricante ainda nao identificado (central nunca testada ou nome desconhecido). Nunca e
    /// substituido pelo FakeAdapter: qualquer operacao falha de forma explicita.
    /// </summary>
    NaoIdentificado
}

public static class AdapterFactory
{
    /// <summary>
    /// Cria um adapter explicitamente, a partir de um fabricante já conhecido
    /// (por exemplo, uma Central já salva com Fabricante identificado).
    /// O FakeAdapter só é devolvido quando explicitamente solicitado (FabricanteTipo.Fake).
    /// </summary>
    public static ICentralAdapter Criar(FabricanteTipo fabricante)
    {
        return fabricante switch
        {
            FabricanteTipo.Intelbras => new IntelbrasAdapter(),
            FabricanteTipo.Jfl => new JflAdapter(),
            FabricanteTipo.Fake => new FakeAdapter(),
            _ => new NaoIdentificadoAdapter()
        };
    }

    /// <summary>
    /// Resolve o enum de fabricante a partir do texto salvo na Central
    /// (ex.: "Intelbras", "JFL"). Texto vazio ou desconhecido resulta em
    /// <see cref="FabricanteTipo.NaoIdentificado"/>, nunca em simulação.
    /// </summary>
    public static FabricanteTipo ResolverPorNome(string? fabricante)
    {
        return fabricante?.Trim().ToLowerInvariant() switch
        {
            "intelbras" => FabricanteTipo.Intelbras,
            "jfl" => FabricanteTipo.Jfl,
            "fake" => FabricanteTipo.Fake,
            _ => FabricanteTipo.NaoIdentificado
        };
    }

    /// <summary>
    /// Tenta identificar o fabricante da central.
    ///
    ///  - JFL: a central é quem conecta no servidor do CentralHub, então não há conexão TCP a testar.
    ///    A central é reconhecida se já houver uma sessão JFL ativa cujo número de série seja o
    ///    informado no campo <c>usuario</c>.
    ///  - Intelbras: conexão TCP direta na porta informada (heurística: 9009) seguida da identificação
    ///    ISECNet.
    ///
    /// Se nada identificar a central, devolve o último resultado real (com erro) — nunca o FakeAdapter.
    /// </summary>
    public static async Task<ConexaoResult> DetectarEConectar(
        string ip, int porta, string usuario, string senha, bool permitirFallbackFake = false)
    {
        ConexaoResult? ultimoResultado = null;

        if (JflSessionRegistry.Default.Find(usuario) is not null)
        {
            var jfl = await Criar(FabricanteTipo.Jfl).VerificarConectividade(ip, porta, usuario, senha);
            if (jfl.Sucesso)
            {
                return jfl;
            }
            ultimoResultado = jfl;
        }

        var intelbras = await Criar(FabricanteTipo.Intelbras).VerificarConectividade(ip, porta, usuario, senha);
        if (intelbras.Sucesso)
        {
            return intelbras;
        }

        if (permitirFallbackFake)
        {
            // Somente desenvolvimento: simula uma central quando nenhum adapter real conectou.
            return await Criar(FabricanteTipo.Fake).VerificarConectividade(ip, porta, usuario, senha);
        }

        return ultimoResultado ?? new ConexaoResult
        {
            Sucesso = false,
            Status = "Offline",
            LatenciaMs = intelbras.LatenciaMs,
            Erro = $"Nenhum fabricante identificou a central. Intelbras (TCP {ip}:{porta}): {intelbras.Erro}. " +
                   $"JFL: nenhuma central com número de série '{usuario}' está conectada ao servidor do CentralHub."
        };
    }
}
