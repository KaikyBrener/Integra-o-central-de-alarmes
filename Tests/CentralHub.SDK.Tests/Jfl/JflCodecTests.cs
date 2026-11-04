using CentralHub.SDK.Adapters;
using CentralHub.SDK.Adapters.Jfl;
using Xunit;

namespace CentralHub.SDK.Tests.Jfl;

/// <summary>Codec contra quadros reais capturados (Active 32 Duo fw 7.60). Ver <see cref="RealCaptures"/>.</summary>
public class JflCodecTests
{
    [Theory]
    [InlineData(RealCaptures.Connection)]
    [InlineData(RealCaptures.EventZoneAlarm1130)]
    [InlineData(RealCaptures.EventArm3407)]
    [InlineData(RealCaptures.EventAutoBypass1570)]
    [InlineData(RealCaptures.CmdArmPartition1)]
    [InlineData(RealCaptures.CmdPgm2On)]
    [InlineData(RealCaptures.CmdBypassZone9)]
    [InlineData(RealCaptures.StatusAfterPgmOn)]
    [InlineData(RealCaptures.StatusAfterBypass)]
    public void Capturas_reais_sao_quadros_validos(string hex)
    {
        var raw = RealCaptures.Bytes(hex);
        Assert.True(JflFrame.IsValid(raw));
        Assert.Equal(raw.Length, raw[1]); // TAM e o tamanho TOTAL
        Assert.Equal(0, JflFrame.Xor(raw)); // XOR total == 0
    }

    // ---- Construcao: reproduz byte a byte o que foi capturado ----

    [Fact]
    public void Arme_particao_1_reproduz_a_captura() =>
        Assert.Equal(RealCaptures.Bytes(RealCaptures.CmdArmPartition1), JflEncoder.Arm(0x6F, 1));

    [Fact]
    public void Desarme_particao_1_reproduz_a_captura() =>
        Assert.Equal(RealCaptures.Bytes(RealCaptures.CmdDisarmPartition1), JflEncoder.Disarm(0x79, 1));

    [Fact]
    public void Ligar_pgm_2_reproduz_a_captura() =>
        Assert.Equal(RealCaptures.Bytes(RealCaptures.CmdPgm2On), JflEncoder.PgmOn(0x45, 2));

    [Fact]
    public void Desligar_pgm_2_reproduz_a_captura() =>
        Assert.Equal(RealCaptures.Bytes(RealCaptures.CmdPgm2Off), JflEncoder.PgmOff(0x49, 2));

    [Fact]
    public void Bypass_da_zona_9_reproduz_a_captura_MSB_first() =>
        Assert.Equal(RealCaptures.Bytes(RealCaptures.CmdBypassZone9), JflEncoder.Bypass(0x4F, new[] { 9 }));

    [Fact]
    public void Limpar_bypass_reproduz_a_captura() =>
        Assert.Equal(RealCaptures.Bytes(RealCaptures.CmdBypassClearAll), JflEncoder.Bypass(0x5F, Array.Empty<int>()));

    [Fact]
    public void Ack_de_conexao_ecoa_o_seq_da_central()
    {
        // Documentado/capturado em pyjfl: 7B 07 F9 21 01 05 A0 (responde ao 0x21 de seq 0xF9).
        var panelSeq = RealCaptures.Bytes(RealCaptures.Connection)[2];
        Assert.Equal(0xF9, panelSeq);
        Assert.Equal(Convert.FromHexString("7B07F9210105A0"), JflEncoder.ConnectionAck(panelSeq));
    }

    [Fact]
    public void Ack_de_evento_devolve_o_contador_binario_sem_alteracao()
    {
        var evento = new JflFrame(RealCaptures.Bytes(RealCaptures.EventZoneAlarm1130));
        var ack = new JflFrame(JflEncoder.EventAck(evento.Seq, evento.Slice(17, 21)));

        Assert.Equal(JflCmd.Event, ack.Cmd);
        Assert.Equal(evento.Seq, ack.Seq);
        Assert.Equal(new byte[] { 0x01, 0x00, 0x00, 0x39, 0x1D }, ack.Payload.ToArray());
        Assert.Equal(10, ack.Length);
    }

    [Fact]
    public void Ack_de_keepalive_informa_o_intervalo_em_minutos()
    {
        var ack = new JflFrame(JflEncoder.KeepAliveAck(0x11, 5));
        Assert.Equal(JflCmd.KeepAlive, ack.Cmd);
        Assert.Equal(new byte[] { 0x05 }, ack.Payload.ToArray());
    }

    [Fact]
    public void Seq_zero_nunca_e_emitido()
    {
        Assert.Equal(1, JflEncoder.StatusRequest(0)[2]);
        var seq = new JflSequence(0xFE);
        Assert.Equal(new byte[] { 0xFE, 0xFF, 0x01, 0x02 }, new[] { seq.Next(), seq.Next(), seq.Next(), seq.Next() });
    }

    [Fact]
    public void Quadro_acima_de_255_bytes_e_recusado() =>
        Assert.Throws<ArgumentException>(() => JflFrame.Build(1, 0x4D, new byte[251]));

    // ---- Decodificacao ----

    [Fact]
    public void Decodifica_quadro_de_conexao_real()
    {
        var info = JflDecoder.DecodeConnection(new JflFrame(RealCaptures.Bytes(RealCaptures.Connection)));

        Assert.Equal("0000000000", info.Serial);
        Assert.Equal(0xA0, info.ModelByte);
        Assert.Equal("760", info.Firmware);
        Assert.Equal("FFFFFFFFFFFF", info.Mac);
        Assert.Equal(1, info.PartitionCount);
        Assert.Equal(0x01, info.FenceRaw);
        Assert.Equal(16, info.Partitions.Count);
        Assert.True(info.Partitions[0].Disarmed);
        Assert.All(info.Partitions.Skip(1), p => Assert.False(p.Programmed));
    }

    [Fact]
    public void Decodifica_evento_de_disparo_de_zona_real()
    {
        var e = JflDecoder.DecodeEvent(new JflFrame(RealCaptures.Bytes(RealCaptures.EventZoneAlarm1130)));

        Assert.Equal("0001", e.Account);
        Assert.Equal("1130", e.Code);
        Assert.Equal("01", e.Partition);
        Assert.Equal("010", e.Subject);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x39, 0x1D }, e.Counter);
    }

    [Fact]
    public void Decodifica_status_real_apos_ligar_pgm_2()
    {
        var frame = new JflFrame(RealCaptures.Bytes(RealCaptures.StatusAfterPgmOn));
        var s = JflDecoder.DecodeStatus(frame);

        Assert.Equal(JflCmd.PgmOn, frame.Cmd);   // o status ecoa o comando
        Assert.Equal(0x45, frame.Seq);            // e o seq
        Assert.Equal("08/08/26 16:31:40", s.Clock);
        Assert.Equal(14.07, s.BatteryVolts, 2);
        Assert.True(s.IsPgmOn(2));
        Assert.False(s.IsPgmOn(1));
        Assert.True(s.IsPgmPermitted(2));         // P-PGM = 0x0A -> PGM 2 e 4
        Assert.True(s.IsPgmPermitted(4));
        Assert.False(s.IsPgmPermitted(1));
        Assert.True(s.Partitions[0].Disarmed);
        Assert.Equal(23, s.Zones.Count);
        Assert.Equal(JflZoneStatus.Open, s.Zones.Single(z => z.Number == 9).Status);
        Assert.Equal(JflZoneStatus.Closed, s.Zones.Single(z => z.Number == 8).Status);
        Assert.Equal(new[] { 8, 9, 10, 11, 13, 14 }, s.BypassableZones.Take(6));
        Assert.Empty(s.BypassedZones);
        Assert.Equal(127, s.FrameLength);
    }

    [Fact]
    public void Decodifica_status_real_apos_bypass_da_zona_9()
    {
        var s = JflDecoder.DecodeStatus(new JflFrame(RealCaptures.Bytes(RealCaptures.StatusAfterBypass)));

        Assert.Equal(new[] { 9 }, s.BypassedZones);
        Assert.False(s.IsPgmOn(2));
    }

    [Fact]
    public void Bitmaps_de_zona_tem_ordens_opostas_no_status_e_no_comando()
    {
        // P-INIB (status) e LSB-first; o comando 0x52 e MSB-first (provado por efeito na captura).
        Assert.Equal(new[] { 1 }, JflDecoder.BitmapToNumbers(new byte[] { 0x01 }, lsbFirst: true));
        Assert.Equal(new[] { 1 }, JflDecoder.BitmapToNumbers(new byte[] { 0x80 }, lsbFirst: false));
        Assert.Equal(new byte[] { 0x80, 0x00 }, JflDecoder.NumbersToBitmap(new[] { 1 }, 2, lsbFirst: false));
    }

    // ---- Leitor de fluxo ----

    [Fact]
    public void Leitor_remonta_quadro_entregue_byte_a_byte()
    {
        var raw = RealCaptures.Bytes(RealCaptures.EventZoneAlarm1130);
        var reader = new JflFrameReader();
        var frames = new List<JflFrame>();
        foreach (var b in raw) frames.AddRange(reader.Feed(new[] { b }));

        Assert.Single(frames);
        Assert.Equal(raw, frames[0].Raw);
    }

    [Fact]
    public void Leitor_separa_quadros_colados_em_uma_unica_leitura()
    {
        var a = RealCaptures.Bytes(RealCaptures.EventZoneAlarm1130);
        var b = RealCaptures.Bytes(RealCaptures.EventArm3407);
        var frames = new JflFrameReader().Feed(a.Concat(b).ToArray());

        Assert.Equal(2, frames.Count);
        Assert.Equal(a, frames[0].Raw);
        Assert.Equal(b, frames[1].Raw);
    }

    [Fact]
    public void Leitor_ressincroniza_apos_lixo_e_checksum_invalido()
    {
        var good = RealCaptures.Bytes(RealCaptures.EventArm3407);
        var corrupt = (byte[])RealCaptures.Bytes(RealCaptures.EventZoneAlarm1130).Clone();
        corrupt[^1] ^= 0xFF; // checksum quebrado

        var reader = new JflFrameReader();
        var frames = reader.Feed(new byte[] { 0x01, 0x02, 0x03 }.Concat(corrupt).Concat(good).ToArray());

        Assert.Single(frames);
        Assert.Equal(good, frames[0].Raw);
        Assert.True(reader.DroppedBytes > 0);
    }

    [Fact]
    public void Leitor_nao_perde_quadro_real_atras_de_0x7B_falso_em_payload()
    {
        // 0x7B ('{') aparece em contas/IMEI; um "tamanho" grande logo depois simula um quadro longo.
        var good = RealCaptures.Bytes(RealCaptures.EventArm3407);
        var phantom = new byte[] { 0x7B, 0xF0, 0x01 };
        var frames = new JflFrameReader().Feed(phantom.Concat(good).ToArray());

        Assert.Single(frames);
        Assert.Equal(good, frames[0].Raw);
    }

    [Theory]
    [InlineData(0xB3, JflUnsupportedGeneration.Legacy0xB3)]
    [InlineData(0x7A, JflUnsupportedGeneration.Wireless0x7A)]
    public void Leitor_sinaliza_geracoes_de_protocolo_nao_suportadas(byte first, JflUnsupportedGeneration expected)
    {
        var reader = new JflFrameReader();
        reader.Feed(new byte[] { first, 0x10, 0x20 });
        Assert.Equal(expected, reader.UnsupportedGeneration);
    }

    [Fact]
    public void Leitor_nao_sinaliza_geracao_para_quadro_0x7B()
    {
        var reader = new JflFrameReader();
        reader.Feed(RealCaptures.Bytes(RealCaptures.Connection));
        Assert.Equal(JflUnsupportedGeneration.None, reader.UnsupportedGeneration);
    }
}

public class JflModelProfileTests
{
    [Fact]
    public void Active_100_Bus_segue_os_tetos_do_datasheet_oficial()
    {
        var p = JflModelProfile.Resolve(JflModelProfile.ByteActive100Bus);
        Assert.Equal("Active 100 Bus", p.Name);
        Assert.Equal("PCI-350", p.Board);
        Assert.Equal(16, p.Partitions); // "16 partições reais"
        Assert.Equal(99, p.Zones);      // "99 zonas"
        Assert.Equal(16, p.Pgms);       // "1 saída PGM ... expansível até 16"
        Assert.True(p.CommandsEnabled);
        Assert.False(p.HardwareValidated);
        Assert.Contains("nao validado em hardware", p.Description);
    }

    [Fact]
    public void Active_20_segue_os_tetos_do_datasheet_oficial()
    {
        var p = JflModelProfile.Resolve(JflModelProfile.ByteActive20);
        Assert.Equal("Active 20", p.Name);
        Assert.Equal("PCI-325", p.Board);
        Assert.Equal(2, p.Partitions);  // "2 partições reais"
        Assert.Equal(32, p.Zones);      // "32 zonas"
        Assert.Equal(4, p.Pgms);        // "expansível até 4 PGMs"
        Assert.True(p.CommandsEnabled);
        Assert.False(p.HardwareValidated);
    }

    [Fact]
    public void Os_dois_modelos_NAO_compartilham_o_perfil_da_Active_32_Duo()
    {
        var duo = JflModelProfile.Resolve(JflModelProfile.ByteActive32Duo);
        Assert.True(duo.HardwareValidated);
        Assert.NotEqual(duo.Partitions, JflModelProfile.Resolve(JflModelProfile.ByteActive100Bus).Partitions);
        Assert.NotEqual(duo.Zones, JflModelProfile.Resolve(JflModelProfile.ByteActive100Bus).Zones);
        Assert.NotEqual(duo.Partitions, JflModelProfile.Resolve(JflModelProfile.ByteActive20).Partitions);
    }

    [Theory]
    [InlineData(0xA1)] [InlineData(0xA2)] [InlineData(0xA3)] [InlineData(0xA5)]
    [InlineData(0xA6)] [InlineData(0xA8)] [InlineData(0x4B)] [InlineData(0x5D)] [InlineData(0x99)]
    public void Demais_modelos_e_desconhecidos_nao_habilitam_comandos(byte modelByte)
    {
        var p = JflModelProfile.Resolve(modelByte);
        Assert.False(p.CommandsEnabled);
        Assert.False(p.HardwareValidated);
    }
}

public class JflContactIdTests
{
    [Fact]
    public void Mapeia_disparo_de_zona_real()
    {
        var e = JflEventMapper.ToEvento(
            JflDecoder.DecodeEvent(new JflFrame(RealCaptures.Bytes(RealCaptures.EventZoneAlarm1130))), "SER1");

        Assert.Equal(TipoEvento.Alarme, e.Tipo);
        Assert.Equal(10, e.Zona);
        Assert.Equal(1, e.Particao);
        Assert.Equal("0001", e.Conta);
        Assert.Equal("1130", e.Codigo);
        Assert.False(e.Restauracao);
        Assert.Equal("JFL", e.Fabricante);
        Assert.Equal("SER1", e.Serial);
        Assert.Contains("Disparo da zona", e.Detalhe);
    }

    [Fact]
    public void Mapeia_arme_remoto_real_com_usuario_99_do_software()
    {
        // Manual JFL: eventos de arme/desarme e PGM gerados por software saem como usuario 99.
        var e = JflEventMapper.ToEvento(
            JflDecoder.DecodeEvent(new JflFrame(RealCaptures.Bytes(RealCaptures.EventArm3407))), "SER1");

        Assert.Equal(TipoEvento.Arme, e.Tipo);
        Assert.Equal(99, e.Usuario);
        Assert.False(e.Restauracao); // 3xxx de arme NAO e restauracao
    }

    [Fact]
    public void Mapeia_bypass_real_de_zona()
    {
        var e = JflEventMapper.ToEvento(
            JflDecoder.DecodeEvent(new JflFrame(RealCaptures.Bytes(RealCaptures.EventAutoBypass1570))), "SER1");

        Assert.Equal(TipoEvento.Bypass, e.Tipo);
        Assert.Equal(9, e.Zona);
    }

    [Fact]
    public void Pgm_vem_no_campo_particao_dos_eventos_1422_e_3422()
    {
        // Manual JFL, item 14.9.2: "O número da PGM será informado na partição do evento."
        var raw = JflFrame.Build(1, JflCmd.Event,
            System.Text.Encoding.ASCII.GetBytes("00013422" + "03" + "000").Concat(new byte[] { 0, 0, 0, 1, 0, 0 }).ToArray());
        var e = JflEventMapper.ToEvento(JflDecoder.DecodeEvent(new JflFrame(raw)), "SER1");

        Assert.Equal(TipoEvento.Pgm, e.Tipo);
        Assert.Equal(3, e.Pgm);
        Assert.Null(e.Particao);
        Assert.True(e.Restauracao);
    }

    [Fact]
    public void Codigo_fora_da_tabela_e_preservado_sem_inventar_significado()
    {
        var raw = JflFrame.Build(1, JflCmd.Event,
            System.Text.Encoding.ASCII.GetBytes("00019999" + "01" + "123").Concat(new byte[] { 0, 0, 0, 1, 0, 0 }).ToArray());
        var e = JflEventMapper.ToEvento(JflDecoder.DecodeEvent(new JflFrame(raw)), "SER1");

        Assert.Equal(TipoEvento.Geral, e.Tipo);
        Assert.Equal("9999", e.Codigo);
        Assert.Null(e.Zona);
        Assert.Contains("nao catalogado", e.Detalhe);
    }

    [Theory]
    [InlineData("1130")] [InlineData("3130")] [InlineData("1301")] [InlineData("3301")]
    [InlineData("1401")] [InlineData("3401")] [InlineData("1422")] [InlineData("3422")] [InlineData("1570")] [InlineData("1602")]
    public void Codigos_do_manual_estao_na_tabela(string code) => Assert.NotNull(JflContactIdTable.Find(code));
}
