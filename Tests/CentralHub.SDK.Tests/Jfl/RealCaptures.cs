namespace CentralHub.SDK.Tests.Jfl;

/// <summary>
/// Quadros REAIS capturados de uma JFL Active 32 Duo (firmware 7.60) pelo projeto jmceara/pyjfl
/// (https://github.com/jmceara/pyjfl, Apache-2.0, tests/fixtures/*.hex; serial e dados pessoais ja
/// anonimizados pelo autor). Sao a unica fonte de bytes reais disponivel: servem para provar que o
/// codec do CentralHub reproduz byte a byte o que o Active NET/central trocaram.
///
/// ATENCAO: sao de UMA Active 32 Duo. Nao provam nada sobre Active 100 Bus ou Active 20.
/// Para esses modelos, os testes usam o simulador (Simulation/JflPanelSimulator), que e apenas uma
/// ferramenta de teste de protocolo e NAO uma captura.
/// </summary>
public static class RealCaptures
{
    // Quadro 0x21 (102 bytes): serie "0000000000", modelo 0xA0, firmware "760", particao 1 desarmada.
    public const string Connection =
        "7B66F92130303030303030303030FFFFFFFFFFFFFFFFFFFFFFFFFFFFFF464646464646464646464646A03736300201010600000100010002000300040000000000000000000000000000000000000000000000000101000000000000000000000000000000AA";

    // Eventos 0x24 (24 bytes).
    public const string EventZoneAlarm1130 = "7B185224303030313131333030313031300000391D820081";
    public const string EventArm3407 = "7B184B24303030313334303739393039390000391902001F";
    public const string EventAutoBypass1570 = "7B183C24303030313135373030313030390000391C020066";

    // Comandos enviados (capturados).
    public const string CmdArmPartition1 = "7B066F4E015D";       // seq 0x6F
    public const string CmdDisarmPartition1 = "7B06794F014A";    // seq 0x79
    public const string CmdPgm2On = "7B064550026A";              // seq 0x45
    public const string CmdPgm2Off = "7B0649510267";             // seq 0x49
    public const string CmdBypassZone9 = "7B124F5200800000000000000000000000F4"; // seq 0x4F
    public const string CmdBypassClearAll = "7B125F520000000000000000000000000064"; // seq 0x5F

    // Resposta de status (127 bytes) ecoando o comando: apos PGM 2 ligar (seq 0x45) e apos bypass da zona 9 (seq 0x4F).
    public const string StatusAfterPgmOn =
        "7B7F45508835080826163140C502010000000000000000000000000000000100000008788088088888888888888888000000000000000000000000000000000000000000000000000000000000000000000000000000090A0B00000000000000000000000000000080B7FFFF00000000000000000000000F0000000000006A";
    public const string StatusAfterBypass =
        "7B7F4F528835080826163330C500010000000000000000000000000000000100000008188088088888888888888888000000000000000000000000000000000000000000000000000000000000000000000000000000090A1F00000000000000000000000000000080B7FFFF00000000000000000000000F00000000000066";

    public static byte[] Bytes(string hex) => Convert.FromHexString(hex);
}
