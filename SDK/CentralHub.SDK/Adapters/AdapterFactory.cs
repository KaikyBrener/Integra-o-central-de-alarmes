namespace CentralHub.SDK.Adapters;

public enum FabricanteTipo
{
    Fake,
    Intelbras,
    Jfl
}

public static class AdapterFactory
{
    /// <summary>
    /// Cria um adapter explicitamente, a partir de um fabricante já conhecido
    /// (por exemplo, uma Central já salva com Fabricante identificado).
    /// </summary>
    public static ICentralAdapter Criar(FabricanteTipo fabricante)
    {
        return fabricante switch
        {
            FabricanteTipo.Intelbras => new IntelbrasAdapter(),
            FabricanteTipo.Jfl => new JflAdapter(),
            FabricanteTipo.Fake => new FakeAdapter(),
            _ => new FakeAdapter()
        };
    }

    /// <summary>
    /// Resolve o enum de fabricante a partir do texto salvo na Central
    /// (ex.: "Intelbras", "JFL"). Usado quando o fabricante já foi
    /// identificado anteriormente em um teste de conexão.
    /// </summary>
    public static FabricanteTipo ResolverPorNome(string? fabricante)
    {
        return fabricante?.Trim().ToLowerInvariant() switch
        {
            "intelbras" => FabricanteTipo.Intelbras,
            "jfl" => FabricanteTipo.Jfl,
            _ => FabricanteTipo.Fake
        };
    }

    /// <summary>
    /// Tenta identificar automaticamente o fabricante da central, testando
    /// primeiro o adapter cuja porta padrão de fábrica corresponde à porta
    /// informada (heurística baseada na documentação de cada fabricante),
    /// depois os demais adapters, na ordem, até obter uma conexão bem-sucedida.
    /// Caso nenhum adapter real consiga conectar, retorna o resultado do
    /// FakeAdapter (usado em ambientes de desenvolvimento/teste).
    /// </summary>
    public static async Task<ConexaoResult> DetectarEConectar(string ip, int porta, string usuario, string senha)
    {
        var ordemTentativa = new List<FabricanteTipo>();

        if (porta == JflAdapter.PortaPadrao)
        {
            ordemTentativa.Add(FabricanteTipo.Jfl);
        }
        else if (porta == IntelbrasAdapter.PortaPadrao)
        {
            ordemTentativa.Add(FabricanteTipo.Intelbras);
        }

        foreach (var tipo in new[] { FabricanteTipo.Intelbras, FabricanteTipo.Jfl })
        {
            if (!ordemTentativa.Contains(tipo))
            {
                ordemTentativa.Add(tipo);
            }
        }

        ConexaoResult? ultimoResultado = null;

        foreach (var tipo in ordemTentativa)
        {
            var adapter = Criar(tipo);
            var resultado = await adapter.VerificarConectividade(ip, porta, usuario, senha);

            if (resultado.Sucesso)
            {
                return resultado;
            }

            ultimoResultado = resultado;
        }

        // Nenhum adapter real conseguiu conectar.
        // Retorna o ultimo resultado real (com erro) em vez de mascarar com o FakeAdapter.
        // O FakeAdapter so deve ser usado quando explicitamente solicitado via Criar().
        return ultimoResultado ?? new ConexaoResult
        {
            Sucesso = false,
            Status = "Offline",
            Erro = "Nenhum adapter conseguiu conectar na central informada."
        };
    }
}
