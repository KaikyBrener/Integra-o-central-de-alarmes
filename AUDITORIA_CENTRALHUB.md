# Auditoria técnica — CentralHub

Data: 01/11/2026 · Escopo: projeto CentralHub (branch `DEV`) e planilha `Integracao_Centrais_Alarme.xlsx` (68 fontes).
Nenhum código foi alterado. Planilha revisada: `Integracao_Centrais_Alarme_Auditada.xlsx`.

---

## 1. Resumo executivo

1. **O CentralHub hoje só controla PGM por "chamada" (cliente TCP) e nenhum fluxo de eventos existe.** A interface `IEventoListener` é só um contrato sem implementação.
2. **A integração Intelbras existente provavelmente não funciona com uma central real**: o quadro ISECNet usado em `IsecNetProtocol.cs` é o formato do *Receptor IP*, não o do ISECNet2 usado para comandos (dois projetos independentes usam um cabeçalho de 8 bytes que o CentralHub não envia).
3. **A integração JFL existente não é uma integração**: marca a central como "Online" só porque a porta TCP abriu, e a JFL funciona no sentido oposto (a central conecta no servidor). Os quadros em `JflActiveProtocol.cs` também têm o byte de comprimento errado.
4. **Um comando para fabricante desconhecido cai no `FakeAdapter` e é gravado como sucesso** no histórico (`AdapterFactory.cs:38`).
5. **A planilha é, no geral, confiável**, mas das 68 fontes só 26 foram plenamente confirmadas por mim; 6 links estão quebrados, 7 não puderam ser verificadas, 2 divergem, e há afirmações sem fonte (principalmente "DC-09 em Satel/Abus/Trikdis").
6. **Recomendação:** começar pela **Intelbras AMT 8000 — comandos diretos ISECNet2** (corrigir a camada de protocolo e entregar status, zonas, arme/desarme e bypass). **PGM Intelbras não tem opcode público** e depende dos PDFs do suporte Intelbras ou de captura do AMT Remoto. O host de escuta (eventos) vem depois e atende Intelbras, SIA DC-09 e JFL.

> **Decisão que depende de você:** a escolha acima assume que você consegue uma **AMT 8000** de bancada. Se o único equipamento disponível for outro, veja a seção 6.

---

## 2. O que foi verificado e como (e o que não foi)

| Feito | Detalhe |
|---|---|
| Código do projeto | Backend, SDK, Frontend (estrutura), serviços, adapters lidos. |
| 68 links | Testados por HTTP, com cliente de script e depois com User-Agent de navegador (alguns 403 eram só bloqueio de bot). |
| Código-fonte de referência | `elvis-epx/alarme-intelbras`, `jmceara/pyjfl` (inclui vetores de teste), `eavanvalkenburg/pysiaalarm`, `RafaelEstevamReis/AMT-Monitor`, binding openHAB da Satel. |
| PDFs oficiais | Receptor IP e tutorial Intelbras, datasheet AMT 8000, JFL M-300 ETH/Active 20, DSC IT-100/EnvisaLink, ELK M1, Paradox PRT3, Elipse (driver Intelbras), Vetti, VIAWEB, etc. — baixados e pesquisados em texto. |
| Cálculos independentes | CRC16 de uma mensagem DC-09 de exemplo (`9EC4`, tamanho `0x27`) e XOR de um quadro JFL de conexão (resultado 0, como especificado). |

**Não consegui verificar** (ver coluna "Status de validação" na planilha):
- Normas SIA DC-09 e DC-05 (pagas). Tudo sobre DC-09 vem de implementações abertas.
- Páginas que bloquearam acesso (403 ou JavaScript): Hikvision (HIK-01/02/06), SIA (STD-01), manualslib MG3000 (NIC-02), fórum HA Risco (RIS-01), PDF oficial da Satel (SAT-01).
- PDF do SG-System 5 (58 páginas, só imagem) e manual PositivoSEG (texto ilegível) — não reproduzi os trechos citados.
- **Nenhum equipamento real foi testado**, por mim ou (até onde as fontes mostram) para a maioria das marcas.

---

## 3. Diagnóstico do projeto

### 3.1 Como eventos entram e comandos saem hoje

```
Frontend (OperationPage) → OperationController → OperationService
        → AdapterFactory.Criar(fabricante) → ICentralAdapter.{Acionar|Desligar|Pulso}PGM(ip, porta, usuario, senha, pgm)
KeepAliveService (a cada 30 s) → adapter.VerificarConectividade → atualiza Status/Latência
```

- **Comandos:** modelo "uma chamada = uma conexão", sem sessão. Funciona para centrais que *aceitam* conexão (Intelbras AMT 8000).
- **Eventos:** não existe. Falta `TcpListener`/`HostedService`, parser de eventos, tabela de eventos e endpoint. Intelbras (Receptor IP), SIA DC-09 e JFL são todos "a central conecta em você".

### 3.2 O que funciona, está incompleto ou é simulado

| Item | Situação |
|---|---|
| Cadastro de Prédios/Centrais, histórico, UI, KeepAlive | **Funciona** (com `FakeAdapter`). |
| `FakeAdapter` | **Simulado**, por design. |
| Teste de conectividade TCP (latência) | **Real**. |
| Identificação Intelbras (`IdentificarAsync`) | **Incompleta/provavelmente inoperante** — quadro errado (3.3 item 1). |
| Bypass de zona Intelbras | **Incompleto** — opcode certo, quadro errado. |
| PGM (todos os fabricantes reais) | **Não implementado** ("Protocolo nao implementado."). |
| JFL | **Simulado/estático** — Online + "Desconhecido" por TCP aberto. |
| `IZonaAdapter.ConsultarZonas` | Lança `NotImplementedException` (e a premissa "sem opcode" está errada). |
| Eventos (`IEventoListener`) | **Não existe**. |
| Testes automatizados | **Nenhum** (sem projeto de testes; `.github/workflows` vazio). |

### 3.3 Defeitos concretos (comparação código × fontes)

| # | Local | Severidade | Achado |
|---|---|---|---|
| 1 | [IsecNetProtocol.cs:74](SDK/CentralHub.SDK/Adapters/IsecNetProtocol.cs#L74) | **Crítico** | `ConstruirFrame` gera `[LEN][DATA][CS]` (formato do **Receptor IP**). O ISECNet2 de comandos é `dst_id(2) src_id(2) len(2) cmd(2) payload cs` (`PacoteIsecNet2` em `protocol.go`; AMT-Monitor usa o mesmo cabeçalho). Auth 0xF0F0, status 0x0B4A e bypass 0x401F saem no formato errado. |
| 2 | [IntelbrasAdapter.cs:42](SDK/CentralHub.SDK/Adapters/IntelbrasAdapter.cs#L42) | Alto | "Sucesso" = qualquer quadro com checksum válido; o opcode da resposta (0xF0FE / 0x0B4A) não é validado, então senha errada pode parecer sucesso. |
| 3 | [IsecNetProtocol.cs:139](SDK/CentralHub.SDK/Adapters/IsecNetProtocol.cs#L139) | Alto | `ParsearRespostaStatus` depende do quadro errado. No formato real: modelo `0x01` = AMT-8000, firmware = 3 bytes seguintes. |
| 4 | [IsecNetProtocol.cs:118](SDK/CentralHub.SDK/Adapters/IsecNetProtocol.cs#L118) | Médio | `FrameHandshake1–3` ("traces oficiais", conta `E0104`) não aparecem em nenhum código de referência (o receptor do elvis-epx trata 0x00, 0x80, 0x94, 0xB0, 0xB5, 0xF7). |
| 5 | `IntelbrasAdapter.ConsultarZonas` | Médio | O TODO diz que não há opcode de zonas; o status 0x0B4A traz zonas abertas/em alarme/bypass e o AMT-Monitor usa 0x0B11/0B12/0B13. |
| 6 | [JflActiveProtocol.cs:109](SDK/CentralHub.SDK/Adapters/JflActiveProtocol.cs#L109) | Alto | `LEN = 3 + data`; no protocolo real é o tamanho **total** do quadro (ex.: `7B 06 6F 4E 01 5D`). Todos os quadros gerados têm LEN errado. |
| 7 | [JflActiveProtocol.cs:42-54](SDK/CentralHub.SDK/Adapters/JflActiveProtocol.cs#L42) | Alto | "ACK de conexão" `7B 06 01 40 01` é na verdade o ack de keep-alive (cmd 0x40); o de conexão é cmd 0x21 (`7B 07 SEQ 21 01 05 CS`). "Keepalive" e "pedido de status" são o mesmo cmd 0x4D; o 3º byte é **sequência**, não "tipo". |
| 8 | comentários de `JflActiveProtocol.cs` | Médio | Dizem que não há PGM nem firmware: PGM = cmd 0x50 (ligar)/0x51 (desligar), capturados; firmware = texto nos bytes 42–45 do quadro 0x21. |
| 9 | [JflAdapter.cs:11](SDK/CentralHub.SDK/Adapters/JflAdapter.cs#L11) | Alto | `Sucesso=true / Online` só porque a porta abriu (porta 8085 sem fonte). A central JFL não aceita conexão do sistema. |
| 10 | [AdapterFactory.cs:38](SDK/CentralHub.SDK/Adapters/AdapterFactory.cs#L38) + [OperationService.cs:58](Backend/CentralHub.Api/Services/OperationService.cs#L58) | **Alto** | Fabricante nulo/desconhecido → `FakeAdapter`; o histórico grava "PGM n ligado" sem tocar em central nenhuma. `ComandoResult.Sucesso` é descartado. |
| 11 | [KeepAliveService.cs](Backend/CentralHub.Api/Services/KeepAliveService.cs) | Alto | A cada 30 s abre 2 conexões e **autentica com a senha** (Intelbras). Sem backoff: risco de ocupar a sessão da central e, no caso JFL, de bloqueio (5 senhas erradas). |
| 12 | `Central.Senha` / git | Médio | Senha em texto puro no SQLite; `centralhub.db-shm/-wal` versionados. |
| 13 | [AdapterFactory.cs:56](SDK/CentralHub.SDK/Adapters/AdapterFactory.cs#L56) | Médio | Heurística por porta (8085 JFL / 9009 Intelbras). 8085 não tem fonte; a porta é configurável nos dois fabricantes. |

**Arquitetura a preservar:** `ICentralAdapter` + `TcpAdapterBase` + interfaces opcionais (`IZonaAdapter`, `IEventoListener`) é uma boa base. Bastam: corrigir a camada de protocolo, um host de escuta para push e persistência de eventos.

---

## 4. Auditoria da planilha

### 4.1 Números

| Status (68 fontes) | Qtde |
|---|---|
| Confirmado | 26 |
| Parcialmente confirmado | 14 |
| Link OK – conteúdo não conferido | 13 |
| Não verificável (bloqueio/ilegível) | 7 |
| Link quebrado (404) | 6 |
| Divergente | 2 |

Original: 10 "Aberto e conferido", 56 "Visto em resultado de busca", 2 "Falhou ao abrir".

### 4.2 Principais correções (todas com evidência na planilha, aba **Inconsistências**)

- **AES-128 do AMT 8000** (INT-04): no datasheet, aparece na seção de comunicação **sem fio** (915–928 MHz). A "contradição" da aba Lacunas provavelmente deixa de existir; o canal IP "sem criptografia" é evidência do elvis-epx (AMT-8000 fw 2.3.1).
- **Portas 9009/9010** (INT-01/03/12/13): o manual oficial diz 9009 = recebe centrais, 9010 = acesso remoto; já o receptor open source e o driver comercial Elipse usam **9010 como padrão para receber**, e a AMT 8000 atende ISECNet2 em **:9009** (exemplo do gocomandar). São configuráveis e o mesmo número tem papéis diferentes.
- **JFL-04** omitia PGM (0x50/0x51), as 3 gerações de quadro (0x7B / 0xB3 / 0x7A), o conflito do byte de modelo 0xA8 e o risco de licença (JFL_ACTIVE sem licença; README do pyjfl com texto copyleft sobre Apache-2.0).
- **JFL-07**: a página "jfl_alarm" do Home Assistant dá 404 (não existe no core).
- **DSC-02** estava "Falhou ao abrir": o PDF abre e confere (v1.08, 23/09/2025, TCP 4025).
- **JFL-03**: o datasheet não cita Contact ID.
- **POS-01**: "Aberto e conferido", mas o texto do PDF é ilegível; os campos "ID ISEP" não foram reproduzidos.
- **Links quebrados:** INT-05, INT-06, INT-07, INT-10 (404 mesmo com navegador), JFL-07, TEX-01 (o repositório agora é `davidMbrooke/texecom-connect`); DSC-04 cita `Z6543/DSC-TLink` (404).
- **DC-09 em Satel/Abus/Trikdis/Genno** (aba Protocolos): **sem fonte** no catálogo.
- **AJX-01**: a página lista "SIA DC-09 (SIA-DCS)" como conexão direta; ADM-CID aparece sob o Translator e AES não é citado.
- Versões desatualizadas: hikaxpro 2.3.1 (planilha: 2.2.1), pyrisco 0.8.1 (0.6.8).

### 4.3 Natureza da informação (para não confundir)

| Natureza | Exemplos |
|---|---|
| **Oficial/documentado** | Manual Receptor IP Intelbras (operação, portas), datasheets, DSC IT-100/TPI, ELK M1, Paradox PRT3, manuais JFL M-300. |
| **Engenharia reversa** (+ nível de validação) | ISECNet2/Receptor IP (elvis-epx; testado em AMT-8000), JFL (pyjfl; só Active 32 Duo fw 7.60), Intelbras .NET (AMT-Monitor, "não para produção"). |
| **Padrão pago, não lido** | SIA DC-09, DC-05. |
| **Hipótese/relato** | Hikvision AX PRO em DC-09, Nice MG3000, Genno Prime, "ISEP" da PositivoSEG. |

---

## 5. Investigação por tema

### 5.1 Intelbras (portas 9009/9010, ISECNet2)
- **Dois papéis diferentes** (importante): (a) *Receptor IP*: a central conecta ao servidor e envia eventos Contact ID num quadro `[len][tipo][payload][checksum]`, ack `0xFE`, data/hora `0x80` (BCD), `0xF7` isolado; (b) *ISECNet2*: cliente conecta na central e usa `dst(2) src(2) len(2) cmd(2) payload cs`.
- **Comandos confirmados em código** (AMT-8000): auth `0xF0F0` (tipo 0x02 = monitoramento, senha de 4/6 dígitos Contact-ID-codificada, versão 0x10); status `0x0B4A`; arme/desarme `0x401E` (`[partição, 0/1/2]`); sirene `0x4019`; bypass `0x401F`; limpar disparo `0x4013`; bye `0xF0F1`; zonas `0x0B11/0B12/0B13`; MAC `0x3FAA`; log `0x3900`.
- **PGM: nenhum opcode** em nenhuma fonte lida. O AMT Remoto aciona PGM (INT-02), logo o protocolo suporta. O README do elvis-epx afirma que os PDFs do protocolo são entregues pelo suporte Intelbras **sem NDA** (não verifiquei isso diretamente).
- **Modelos:** só AMT-8000 validada em hardware. O driver comercial Elipse declara AMT2018 E/EG, 1016 NET, 4010 Smart e 8000 para **eventos**. Comandos nos demais modelos: não validado.
- **Segurança:** o canal do Receptor não tem criptografia nem autenticação (README); firmware < 2.0.6 tem bugs de conexões duplicadas.

### 5.2 JFL (protocolo, eventos, comandos, modelos)
- Topologia **invertida**: a central disca para o servidor; identificação pelo número de série no quadro 0x21; o servidor responde ack; status só sob demanda (0x4D).
- Quadro: `7B | len TOTAL | seq | cmd | payload | cs` (XOR de tudo = 0). Eventos 0x24 (ack obrigatório, senão repete para sempre), buffer 0x48 (1073 registros), programação 0x44, inventário RF 0x59.
- **Comandos sem autenticação:** arme 0x4E, desarme 0x4F, **PGM 0x50/0x51**, bypass 0x52 (bitmap MSB-first, substitui o conjunto inteiro). Autenticados 0x37: 5 senhas erradas bloqueiam o acesso remoto.
- ⚠️ PGM com função 18 aciona a **cerca elétrica**; não expor como toggle comum.
- **Compatibilidade:** só **Active 32 Duo fw 7.60** validada. Outros modelos "pela especificação" (JFL, não pública). Gerações 0xB3 (fw < 5.0) e 0x7A (Active 8W/QC/Vulcano) **não** implementadas. M-300+/ETH são módulos Contact ID para o **Active NET**; o protocolo IP módulo→receptor não é público.
- **Risco jurídico/técnico:** pyjfl tem 1 autor, projeto novo (08/2026), 0 estrelas; JFL_ACTIVE (base) não declara licença.

### 5.3 SIA DC-09
- Mensagem: `<LF><CRC 4hex><len 4hex>"ID"<seq 4><Rrcvr><Lline>#acct[dados]<_hh:mm:ss,MM-DD-AAAA><CR>`; ID `SIA-DCS`, `ADM-CID` ou `NULL` (supervisão); `*` antes do ID = criptografado.
- CRC16 poly `0xA001`, init 0, enviado como 4 hex ASCII (recalculei o exemplo do py-sia-dc-07: tamanho `0x27`, CRC `9EC4` — confere).
- ACK/NAK/DUH com a mesma sequência; AES-CBC IV zero, chave de 16/24/32 bytes; janela de timestamp −40/+20 s (pysiaalarm).
- **Compatibilidade:** testado com Ajax (pysiaalarm). Hikvision AX PRO: ficha comercial cita "SIA DC09 (SIA level 2, Contact ID)", sem teste citado. **É unidirecional (só eventos).**
- A norma é paga; certificar compatibilidade exige comprá-la.

### 5.4 Contact ID
- Formato `ACCT 18 Q XYZ GG CCC` (+ checksum soma mod 15 no DTMF). Q 1 = novo, 3 = restauração, 6 = anterior.
- Transportes confirmados: dentro do Receptor IP Intelbras (17 bytes), dentro do DC-09 (`ADM-CID`), 15 caracteres sem checksum sobre TCP (Nice Guarita IP), módulos JFL/Radioenge (formato do módulo→receptor **não** publicado).
- Tabelas de código: Intelbras (`protocol.go`), pyjfl, STD-04 (PDF).

### 5.5 Nice
- **Guarita IP:** Contact ID sobre TCP, 15 caracteres, sem checksum, `Q` "fixo (E)" — confirmado na cópia do manual. **Não confirmado:** quem conecta em quem, porta e configuração.
- **MG3000:** fonte bloqueada (403); sem verificação. **Genno Prime IP:** só descrição de varejista.

### 5.6 DSC, Paradox, Satel, Hikvision e demais
| Marca | Possibilidade real |
|---|---|
| DSC PowerSeries | IT-100 (RS-232, descontinuado) e EnvisaLink TPI (TCP 4025, 1 cliente) — **documentado oficialmente**; requer módulo. Neo/Pro (ITv2): restrito. |
| Paradox | EVO/DGP-848 via PRT3 (serial ASCII, oficial). MG/SP via IP150: engenharia reversa; EVO fw ≥ 7.50 criptografa a serial. |
| Satel INTEGRA | TCP 7094, `FE FE cmd dados CRC FE 0D`, CRC init `0x147A` (conferido no código openHAB); 1 cliente; ETHM-1 Plus/INT-RS. PDF oficial não pôde ser aberto. |
| ELK M1 | ASCII RS-232 oficial (Rev. 1.86); portas do M1XEP sem fonte. |
| Hikvision AX PRO | DC-09 direto (a validar); ISUP/OpenAPI exigem cadastro TPP; `hikaxpro` (ISAPI) sofre bloqueios em alguns firmwares. |
| Ajax | DC-09 (SIA-DCS confirmado). Enterprise API restrita. |
| Bosch | Mode 2 sob pedido; biblioteca MIT. |
| Risco, Honeywell Vista, Texecom | Engenharia reversa/NDA. |
| PositivoSEG, VIAWEB, Vetti, PPA, Garen, Rossi | Sem protocolo IP comprovado. |

---

## 6. Matriz de viabilidade (A–D)

A = pronta para desenvolvimento · B = depende de componentes adicionais · C = documentação parcial · D = sem viabilidade comprovada.
"Validado" = testado em hardware real segundo a fonte. Versão completa (14 colunas) na aba **Matriz de viabilidade** da planilha.

| Integração | Classe | Eventos | Comandos | Dependência | Validado em hardware | Compatível com a arquitetura? |
|---|:-:|:-:|:-:|---|---|---|
| Intelbras AMT 8000 — comandos ISECNet2 (status, arme, bypass, zonas) | **A** | — | Sim | AMT 8000 + senha | AMT-8000 fw 2.3.1 | **Sim** (TcpAdapterBase/IZonaAdapter) |
| Intelbras AMT 8000 — **PGM** | **C** | evento 422 | opcode ausente | PDF do suporte / captura | — | Sim, quando houver opcode |
| Intelbras — eventos (Receptor IP próprio) | **B** | Sim | via mesma conexão (não validado) | Host de escuta, VPN | AMT-8000 | Exige componente novo |
| Intelbras — outros modelos | C | Elipse: sim | não validado | cada modelo | — | — |
| JFL Active 32 Duo fw 7.60 | **B** | Sim | PGM, arme, bypass | Host de escuta + sessões | só este | Baixa (modelo por chamada) |
| JFL demais Active / M-300 | C | pela especificação | idem | — | — | — |
| JFL fw antigos / Active 8W (0xB3/0x7A) | D | — | — | — | — | — |
| SIA DC-09 (receptor) | **B** | Sim | **Não** | Emissor DC-09 real, norma | Ajax | Host de escuta |
| Ajax via DC-09 | B | Sim (SIA-DCS) | Não | Hub Ajax | Ajax | idem |
| Hikvision AX PRO via DC-09 | C | provável | Não | AX PRO | — | idem |
| Hikvision ISUP/OpenAPI | D | restrito | restrito | TPP | — | — |
| Parser Contact ID (componente) | **A** | formato | — | nenhuma | tabelas de 2 projetos | Sim |
| Contact ID por linha (DTMF) | D | — | — | hardware decodificador | — | Fora |
| Módulos IP Contact ID (M-300, Radioenge) | D | só no software do fabricante | — | — | — | — |
| Nice Guarita IP | C | Sim (formato) | Não | direção/porta pendentes | — | Host de escuta |
| Nice MG3000 / Genno Prime | D | — | — | — | — | — |
| DSC PowerSeries (IT-100/TPI) | B | Sim | Sim | módulo | amplo (comunidade) | Cliente TCP |
| DSC Neo/Pro (ITv2) | D | — | — | TL280, parceiros | — | — |
| Paradox EVO via PRT3 | B | Sim | Sim | PRT3 + serial | — | Serial |
| Paradox MG/SP via IP150 | C | Sim | Sim | IP150 | comunidade | — |
| Satel INTEGRA | B | Sim | Sim | ETHM-1 Plus | comunidade (openHAB) | Cliente TCP |
| ELK M1 | B | Sim | Sim | M1XEP | — | Cliente TCP |
| Bosch Mode 2 | C | Sim | Sim | doc sob pedido | — | — |
| Risco / Vista / Texecom | C/C/D | — | — | — | — | — |
| PositivoSEG, VIAWEB, Vetti, PPA, Garen, Rossi | D | — | — | — | — | — |
| Satel/Abus/Trikdis via DC-09 (alegação da planilha) | D | — | — | sem fonte | — | — |

> Suporte a protocolo ≠ compatibilidade do equipamento: as classes A/B acima valem para o **modelo validado** indicado na coluna "Validado"; os demais modelos da mesma marca ficam em C até haver teste.

---

## 7. Escolha da primeira integração

| Critério | Intelbras (AMT 8000, comandos) | JFL (Active 32 Duo) | SIA DC-09 |
|---|---|---|---|
| Documentação | 2 implementações independentes + driver comercial + manual oficial (operação). Falta PGM. | 1 implementação (nova, 1 autor), 1 modelo; PDF oficial não público | Norma paga (não lida); implementação aberta testada com Ajax |
| Complexidade | Baixa/média (cabeçalho + opcodes) | Alta (servidor, sessões, acks, 3 gerações, bloqueio por senha) | Média (servidor, CRC, AES, ACK) |
| Aderência à arquitetura atual | **Alta** — cabe em `TcpAdapterBase`/`IZonaAdapter`, sem componente novo | Baixa — exige host e sessões | Média — host de escuta, sem comandos |
| Teste com hardware real | AMT 8000 de bancada | Só Active 32 Duo | Qualquer emissor DC-09 (Ajax/AX PRO) |
| Atende o caso de uso atual (PGM) | **Não ainda** (opcode ausente) | **Sim** (0x50/0x51 capturados) | Não |
| Risco legal | Baixo (MIT; docs sem NDA) | **Médio** (licenças incertas) | Baixo |

**Escolha: Intelbras AMT 8000 (comandos ISECNet2).** Menor mudança arquitetural, documentação mais corroborada e já há código parcial (que precisa de correção). **Ressalva honesta:** o JFL é o único com PGM documentado; se seu requisito imediato for acionar PGM e você tiver uma Active 32 Duo, o JFL passa na frente, ao custo de construir o host de escuta primeiro.

---

## 8. Plano resumido — Intelbras AMT 8000

**Fase 0 — Pré-requisitos (sem código):**
1. Pedir ao suporte Intelbras os PDFs do protocolo ISECNet/Receptor IP (que também resolve o PGM).
2. AMT 8000 de bancada (fw ≥ 2.0.6), senha de 4 ou 6 dígitos, **sirene desconectada**.
3. Wireshark + AMT Remoto para capturar o comando de PGM caso o PDF não o traga.

**Fase 1 — Camada de protocolo:** pacote ISECNet2 com cabeçalho, parser, validação do opcode de resposta (0xF0FE / eco do cmd), auth, bye (0xF0F1). Testes unitários com vetores do elvis-epx e do AMT-Monitor (inclui os casos do item 3.3). Remover ou comprovar `FrameHandshake1–3`.

**Fase 2 — Adapter:** `IdentificarAsync` (modelo/firmware reais), `ConsultarZonas` (0x0B4A / 0x0B11–13), bypass/cancelar bypass, arme/desarme (nova interface opcional, ex.: `IParticaoAdapter`, sem tocar em `ICentralAdapter`).

**Fase 3 — Backend (ajustes mínimos):** KeepAlive sem reautenticar a cada 30 s (usar conexão TCP ou intervalo/backoff), gravar `Sucesso` no `History`, fim do fallback silencioso para `FakeAdapter` (exigir "Simulado" explícito), mensagem clara para "Protocolo não implementado".

**Fase 4 — PGM:** adicionar opcode quando obtido; sem ele, manter falha explícita.

**Fase 5 — Teste real (checklist):** conectar e autenticar; status confere com o teclado; zonas abertas/fechadas; bypass e cancelamento em zona de teste; arme/desarme de partição de teste; senha errada → falha clara (e **sem** laço de retentativa); sessão fechada com bye.

**Depois (próximas etapas):** host de escuta único (`HostedService`) compartilhado por Receptor IP Intelbras → SIA DC-09 → JFL, com tabela de eventos e parser Contact ID.

---

## 9. Pendências e decisões suas

1. Você tem **AMT 8000** à mão? E Active 32 Duo? (Define a primeira implementação; veja a ressalva da seção 7.)
2. Posso pedir os PDFs ao suporte Intelbras? (Resolve PGM e valida o que veio de engenharia reversa.)
3. Aceita o ajuste do fallback `FakeAdapter` (item 3.3-10) como parte da Fase 3?
4. Deseja comprar a norma SIA DC-09 antes do receptor DC-09?

Aguardo sua aprovação antes de qualquer alteração de código.

---

## 10. Fontes utilizadas

**Código e documentação técnica**
- elvis-epx/alarme-intelbras — [protocol.go](https://raw.githubusercontent.com/elvis-epx/alarme-intelbras/main/goalarmeitbl/protocol.go), [comandos.go](https://raw.githubusercontent.com/elvis-epx/alarme-intelbras/main/goalarmeitbl/comandos.go), [receptor_tratador.go](https://raw.githubusercontent.com/elvis-epx/alarme-intelbras/main/goalarmeitbl/receptor_tratador.go), [README](https://github.com/elvis-epx/alarme-intelbras)
- RafaelEstevamReis/AMT-Monitor — [repositório](https://github.com/RafaelEstevamReis/AMT-Monitor) (`AMT.Lib/AMTPackets/_DataPacket.cs`)
- jmceara/pyjfl — [repositório](https://github.com/jmceara/pyjfl), [PyPI](https://pypi.org/project/pyjfl/) (frames.py, encode.py, decode.py, fixtures)
- eavanvalkenburg/pysiaalarm — [repositório](https://github.com/eavanvalkenburg/pysiaalarm) (`event.py`, `regexes.py`, `const.py`)
- openHAB Satel binding — [SatelMessage.java](https://raw.githubusercontent.com/openhab/openhab-addons/main/bundles/org.openhab.binding.satel/src/main/java/org/openhab/binding/satel/internal/protocol/SatelMessage.java) e README
- py-sia-dc-07 — [repositório](https://github.com/creeppork/py-sia-dc-07) (mensagem de exemplo recalculada)

**Documentos oficiais e de terceiros**
- Intelbras — [Manual Receptor IP](https://backend.intelbras.com/sites/default/files/2020-08/Manual_Software_receptorIP_ingles_01-20.pdf), [Tutorial AMT Remoto V6](https://backend.intelbras.com/sites/default/files/2019-08/Tutorial%20AMT%20Remoto%20V6%20-%20Conectando%20%C3%A0%20central%20de%20alarme%20via%20Receptor%20IP.pdf), [Datasheet AMT 8000](https://backend.intelbras.com/sites/default/files/2023-01/Datasheet-AMT%208000_PT_1.23.pdf), [AMT Remoto](https://backend.intelbras.com/sites/default/files/2020-03/Manual-do-usu%C3%A1rio-AMT-Remoto-01.20.pdf)
- Elipse — [Driver Intelbras ISEC](https://docs.elipse.com.br/documents/pt-br/driver/intelbrasisec/latest/intelbrasisec_ptb.pdf)
- JFL — [M-300 ETH](https://dev.jflalarmes.com.br/wp-content/uploads/dlm_uploads/M-300ETH.pdf), [M-300+](https://jflalarmes.com.br/wp-content/uploads/dlm_uploads/2019/07/Manual-M-300.pdf), [Active 20 Ultra](https://jflalarmes.com.br/wp-content/uploads/2026/02/Active-20-Ultra.pdf)
- Ajax — [Connecting to CMS](https://ajax.systems/connecting-to-cms/intrusion/), [Hub CMS support](https://ajax.systems/support/posts/hub-cms-support/)
- Nice — [Guarita IP (cópia manualslib)](https://manualslib.mx/manual/230588/Nice-Anatel-Guarita-Ip.html?page=148)
- DSC — [IT-100 Developer's Guide](https://store.tremtech.com/content/KT-IT100_Developer_Guide.pdf), [EnvisaLink UNO TPI](https://forum.eyezon.com/download/file.php?id=404)
- ELK — [M1 RS-232 Protocol 1.86](https://elk-manuals.s3.amazonaws.com/ELK-M1_RS232_PROTOCOL.Ver+1.86.pdf) · Paradox — [PRT3 ASCII](https://static.chipdip.ru/lib/396/DOC034396674.pdf)
- Hikvision — [ficha AX PRO (resultado de busca)](https://www.hikvision.com/sa/products/Alarm-Products/wireless-intrusion-alarm/ax-pro/ds-pwa96-db-wb/)
- Demais fontes: as 68 do catálogo (status individual na coluna "Status de validação").
