namespace CentralHub.SDK.Adapters.Jfl;

/// <summary>
/// Perfil de um modelo JFL, identificado pelo byte de modelo (deslocamento 41 do quadro 0x21).
///
/// Cada modelo/firmware e tratado separadamente: o protocolo da Active 32 Duo NAO e presumido nas
/// demais centrais. Um perfil informa tetos (particoes, zonas, PGMs) de fonte oficial quando ha e se
/// os comandos foram validados em hardware real.
///
/// Fontes dos tetos:
///  - Active 100 Bus (PCI-350): datasheet JFL "Datasheet Active 100 Bus" — 99 zonas, 16 particoes reais,
///    1 PGM com rele expansivel ate 16, buffer de 1024 eventos.
///  - Active 20 (PCI-325): datasheet JFL "Datasheet Active 20" — 32 zonas, 2 particoes reais + 1 de
///    eletrificador, 1 PGM expansivel ate 4, buffer de 256 eventos, Ethernet via modulo ME-05 WB.
///  - O mapeamento byte de modelo -> nome vem do projeto independente jmceara/pyjfl (a partir do PDF
///    da JFL); a JFL nao publica a tabela. Nenhum dos dois modelos acima teve o byte confirmado em
///    hardware.
/// </summary>
public sealed record JflModelProfile(
    byte ModelByte,
    string Name,
    string Board,
    int Partitions,
    int Zones,
    int Pgms,
    bool HasFence,
    bool CommandsEnabled,
    bool HardwareValidated,
    string Evidence)
{
    public const byte ByteActive100Bus = 0xA4;
    public const byte ByteActive20 = 0xA7;
    public const byte ByteActive32Duo = 0xA0;

    private static readonly Dictionary<byte, JflModelProfile> Known = Build();

    /// <summary>Resolve o perfil de um byte de modelo. Nunca lanca: byte desconhecido nao habilita comandos.</summary>
    public static JflModelProfile Resolve(byte modelByte) =>
        Known.TryGetValue(modelByte, out var profile)
            ? profile
            : new JflModelProfile(modelByte, $"Desconhecido (0x{modelByte:X2})", "?", 0, 0, 0, false,
                CommandsEnabled: false, HardwareValidated: false,
                Evidence: "Byte de modelo nao catalogado; eventos sao registrados, comandos ficam desabilitados.");

    public static IReadOnlyCollection<JflModelProfile> All => Known.Values;

    public string Description =>
        $"{Name} ({Board})" + (HardwareValidated ? "" : " [nao validado em hardware]");

    private static Dictionary<byte, JflModelProfile> Build()
    {
        const string outOfScope =
            "Modelo reconhecido pelo byte, mas fora do escopo implementado: eventos sao registrados, comandos ficam desabilitados.";

        var list = new[]
        {
            new JflModelProfile(ByteActive100Bus, "Active 100 Bus", "PCI-350", 16, 99, 16, true,
                CommandsEnabled: true, HardwareValidated: false,
                Evidence: "Tetos: datasheet oficial JFL. Quadros: mesmo PDF de comandos da familia (Active 100/32/20 Bus/20/8/20 Eth) e " +
                          "changelog JFL (novo protocolo de reporte no padrao Active 20 Bus/32 Duo/100 Bus). Byte 0xA4: pyjfl. " +
                          "Sem captura real; PGM por software exige funcao 'acionada pelo usuario' e, antes da fw 7.0, so 12/13."),
            new JflModelProfile(ByteActive20, "Active 20", "PCI-325", 2, 32, 4, true,
                CommandsEnabled: true, HardwareValidated: false,
                Evidence: "Tetos: datasheet oficial JFL. Quadros: mesmo PDF de comandos da familia. Byte 0xA7: pyjfl. " +
                          "Ethernet pelo modulo ME-05 WB. A central nao esta na frase do changelog sobre o 'novo protocolo de reporte'. Sem captura real."),
            new JflModelProfile(ByteActive32Duo, "Active 32 Duo", "PCI-356", 4, 32, 4, true,
                CommandsEnabled: true, HardwareValidated: true,
                Evidence: "Unico modelo com capturas reais (firmware 7.60, via jmceara/pyjfl). Usado como regressao de bytes, nao como prova para os demais."),
            new JflModelProfile(0xA1, "Active 20 Ultra / 20 GPRS", "PCI-327", 2, 22, 4, true, false, false, outOfScope),
            new JflModelProfile(0xA2, "Active 8 Ultra", "PCI-327", 2, 12, 0, false, false, false, outOfScope),
            new JflModelProfile(0xA3, "Active 20 Ethernet", "PCI-340", 2, 22, 4, true, false, false, outOfScope),
            new JflModelProfile(0xA5, "Active 20 Bus", "PCI-358/366", 2, 32, 16, true, false, false, outOfScope),
            new JflModelProfile(0xA6, "Active Full 32", "PCI-444/448", 4, 32, 16, false, false, false, outOfScope),
            new JflModelProfile(0xA8, "Active 8W", "PCI-443", 2, 32, 4, true, false, false,
                outOfScope + " Possivel geracao de protocolo diferente (0x7A)."),
            new JflModelProfile(0x4B, "M-300+", "-", 0, 0, 4, false, false, false, outOfScope),
            new JflModelProfile(0x5D, "M-300 Flex", "-", 0, 0, 2, false, false, false, outOfScope),
        };

        return list.ToDictionary(p => p.ModelByte);
    }
}
