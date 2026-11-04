namespace CentralHub.SDK.Adapters.Jfl;

/// <summary>O que o campo "usuario/zona" (3 digitos) de um evento representa para um dado codigo.</summary>
public enum JflSubjectKind
{
    None,
    Zone,
    User
}

public sealed record JflContactIdEntry(string Code, string Description, TipoEvento Tipo, JflSubjectKind Subject, bool PartitionIsPgm = false);

/// <summary>
/// Tabela de eventos Contact ID das centrais Active (secao 29 do manual JFL "Active 8W/20/20 Bus/Full 32/
/// 100 Bus/20 Ultra", fw 7.3+). O primeiro digito e o qualificador da JFL: 1 = evento, 3 = restauracao
/// (arme/desarme seguem o padrao Contact ID: 3401 = arme, 1401 = desarme).
///
/// Somente codigos listados no manual foram incluidos. O significado do campo zona/usuario so e
/// interpretado nos codigos em que o padrao Contact ID o define sem ambiguidade (zona nos eventos de
/// zona/sensor, usuario nos de arme/desarme); nos demais o valor bruto e preservado.
/// A PGM e informada no campo PARTICAO dos eventos 1422/3422 (manual, item 14.9.2).
/// </summary>
public static class JflContactIdTable
{
    private static readonly Dictionary<string, JflContactIdEntry> Entries = Build();

    public static JflContactIdEntry? Find(string code) => Entries.TryGetValue(code, out var e) ? e : null;

    public static IReadOnlyCollection<JflContactIdEntry> All => Entries.Values;

    private static Dictionary<string, JflContactIdEntry> Build()
    {
        var d = new Dictionary<string, JflContactIdEntry>();

        void Add(string code, string desc, TipoEvento tipo, JflSubjectKind subject = JflSubjectKind.None, bool pgm = false)
            => d[code] = new JflContactIdEntry(code, desc, tipo, subject, pgm);

        // Par evento (1xxx) / restauracao (3xxx) com o mesmo tipo.
        void Pair(string suffix, string eventDesc, string restoreDesc, TipoEvento tipo, JflSubjectKind subject = JflSubjectKind.None)
        {
            Add("1" + suffix, eventDesc, tipo, subject);
            Add("3" + suffix, restoreDesc, tipo == TipoEvento.Alarme ? TipoEvento.Geral : tipo, subject);
        }

        // Alarmes
        Add("1100", "Emergencia medica", TipoEvento.Alarme);
        Add("1110", "Incendio", TipoEvento.Alarme);
        Add("1120", "Panico", TipoEvento.Alarme);
        Add("1121", "Coacao", TipoEvento.Alarme);
        Add("1122", "Panico silencioso", TipoEvento.Alarme);
        Add("1123", "Panico audivel", TipoEvento.Alarme);
        Pair("130", "Disparo da zona", "Restauracao do disparo da zona", TipoEvento.Alarme, JflSubjectKind.Zone);
        Pair("134", "Alarme de porta aberta", "Restauracao do alarme de porta aberta", TipoEvento.Alarme, JflSubjectKind.Zone);
        Pair("137", "Alarme de zona tipo tamper", "Restauracao do alarme de zona tipo tamper", TipoEvento.Alarme, JflSubjectKind.Zone);
        Pair("139", "Inatividade de movimento da zona", "Restauracao de movimento da zona", TipoEvento.Problema, JflSubjectKind.Zone);

        // Problemas
        Pair("300", "Problema da saida auxiliar", "Restauracao do problema da saida auxiliar", TipoEvento.Problema);
        Pair("301", "Falta de AC", "Restauracao da falta de AC", TipoEvento.Energia);
        Pair("302", "Problema da bateria da central", "Restauracao do problema da bateria da central", TipoEvento.Bateria);
        Add("1305", "Reset do sistema", TipoEvento.Geral);
        Add("1306", "Alteracao de programacao", TipoEvento.Programacao);
        Add("1311", "Bateria morta", TipoEvento.Bateria);
        Pair("312", "Problema de curto de barramento", "Restauracao de curto de barramento", TipoEvento.Problema);
        Pair("321", "Problema de sirene", "Restauracao do problema de sirene", TipoEvento.Problema);
        Pair("322", "Problema de supervisao de sirene", "Restauracao da supervisao da sirene", TipoEvento.Problema);
        Pair("330", "Problema de teclado", "Restauracao do problema de teclado", TipoEvento.Problema);
        Pair("333", "Problema da supervisao PGM", "Restauracao da supervisao PGM", TipoEvento.Problema);
        Pair("338", "Problema de bateria fraca de controle remoto", "Restauracao de bateria fraca de controle remoto", TipoEvento.Bateria);
        Pair("342", "Problema de AC do teclado sem fio", "Restauracao de AC do teclado sem fio", TipoEvento.Energia);
        Pair("345", "Problema de bateria fraca do teclado sem fio", "Restauracao de bateria fraca do teclado sem fio", TipoEvento.Bateria);
        Pair("346", "Problema de tamper de teclado", "Restauracao de tamper de teclado", TipoEvento.Problema);
        Pair("351", "Problema de linha telefonica", "Restauracao do problema de linha telefonica", TipoEvento.Comunicacao);
        Pair("359", "Problema de Wi-Fi", "Restauracao de Wi-Fi", TipoEvento.Comunicacao);
        Pair("360", "Problema de GPRS", "Restauracao do problema de GPRS", TipoEvento.Comunicacao);
        Pair("361", "Problema de Ethernet", "Restauracao do problema Ethernet", TipoEvento.Comunicacao);
        Pair("362", "Problema de SMS", "Restauracao do problema de SMS", TipoEvento.Comunicacao);
        Pair("363", "Problema de modulo de celular", "Restauracao do problema de modulo de celular", TipoEvento.Comunicacao);
        Pair("364", "Problema de SIM card", "Restauracao do problema de SIM card", TipoEvento.Comunicacao);
        Pair("366", "Problema de modulo de Ethernet", "Restauracao do problema de modulo de Ethernet", TipoEvento.Comunicacao);
        Pair("369", "Problema de cabo de rede", "Restauracao do cabo de rede", TipoEvento.Comunicacao);
        Pair("370", "Curto de zona", "Restauracao do curto de zona", TipoEvento.Problema);
        Pair("381", "Problema de supervisao do sensor", "Restauracao de supervisao dos sensores", TipoEvento.Problema, JflSubjectKind.Zone);
        Pair("383", "Problema de tamper do sensor", "Restauracao do problema de tamper do sensor", TipoEvento.Problema, JflSubjectKind.Zone);
        Pair("384", "Problema de bateria fraca dos sensores sem fio", "Restauracao de bateria fraca dos sensores sem fio", TipoEvento.Bateria, JflSubjectKind.Zone);
        Pair("391", "Problema de supervisao do dispositivo de panico", "Restauracao de supervisao do dispositivo de panico", TipoEvento.Problema);

        // Gerais
        Add("1410", "Acesso remoto a programacao por computador", TipoEvento.Programacao);
        Add("1412", "Usuario logado via aplicativo (nao reportado ao monitoramento)", TipoEvento.Geral);
        Add("1417", "Nova atualizacao de firmware encontrada", TipoEvento.Geral);
        Add("1419", "Usuario registrado para receber notificacao (nao reportado ao monitoramento)", TipoEvento.Geral);
        Add("1421", "Acesso negado (errou senha 5 vezes)", TipoEvento.Geral);
        Add("1422", "PGM acionada pelo usuario", TipoEvento.Pgm, pgm: true);
        Add("3422", "PGM desacionada pelo usuario", TipoEvento.Pgm, pgm: true);
        Add("1429", "Inicio de ronda", TipoEvento.Geral);
        Add("1430", "Fim de ronda", TipoEvento.Geral);
        Add("1454", "Falha ao armar", TipoEvento.Geral);
        Add("1570", "Zona inibida (bypass)", TipoEvento.Bypass, JflSubjectKind.Zone);
        Add("1573", "Zona autoanulada", TipoEvento.Bypass, JflSubjectKind.Zone);
        Add("1578", "Zona armada forcada", TipoEvento.Bypass, JflSubjectKind.Zone);
        Add("3578", "Restauracao da zona armada forcada", TipoEvento.Bypass, JflSubjectKind.Zone);
        Add("1579", "Tentativa de arme com zona aberta", TipoEvento.Geral, JflSubjectKind.Zone);
        Add("1602", "Teste periodico", TipoEvento.Teste);
        Add("1611", "Ronda OK", TipoEvento.Geral);
        Add("1612", "Falhou ronda", TipoEvento.Geral);
        Add("1627", "Entrou na programacao", TipoEvento.Programacao);
        Add("1628", "Saiu da programacao", TipoEvento.Programacao);

        // Arme (3xxx) e desarme (1xxx), usuario no campo zona/usuario
        Add("3401", "Arme", TipoEvento.Arme, JflSubjectKind.User);
        Add("1401", "Desarme", TipoEvento.Desarme, JflSubjectKind.User);
        Add("3403", "Autoarme por horario programado", TipoEvento.Arme);
        Add("1403", "Auto desarme por horario programado", TipoEvento.Desarme);
        Add("3404", "Auto arme por nao movimento", TipoEvento.Arme);
        Add("3407", "Arme remoto", TipoEvento.Arme, JflSubjectKind.User);
        Add("1407", "Desarme remoto", TipoEvento.Desarme, JflSubjectKind.User);
        Add("3408", "Arme rapido", TipoEvento.Arme, JflSubjectKind.User);
        Add("3409", "Arme por controle remoto ou entrada LIGA", TipoEvento.Arme, JflSubjectKind.User);
        Add("1409", "Desarme por controle remoto ou entrada LIGA", TipoEvento.Desarme, JflSubjectKind.User);
        Add("3441", "Arme STAY", TipoEvento.Arme, JflSubjectKind.User);
        Add("3464", "Auto arme adiado", TipoEvento.Geral);

        return d;
    }
}
