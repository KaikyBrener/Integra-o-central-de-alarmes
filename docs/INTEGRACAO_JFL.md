# Integração JFL Active 100 Bus (PCI-350) e Active 20 (PCI-325)

Estado em 01/11/2026. Este documento descreve **o que foi efetivamente implementado**, em que evidência cada parte se apoia e **o que ainda depende de teste em hardware** (não há acesso físico às centrais).

## 1. Como a comunicação funciona (pesquisa)

| Fato | Fonte |
|---|---|
| A central JFL **conecta como cliente** no IP/porta programados: IP de destino nos endereços **702/703**, porta nos **706/707** (4 dígitos, máx. 9999), reporte Ethernet habilitado no **700 tecla 3**. O manual chama esse destino de "computador onde o Active NET está instalado". | Manual JFL das linhas Active, itens 14.4 e 30 (fw 7.3+) |
| No Active 100 Bus e na Active 20 a Ethernet vem do módulo **ME-05 / ME-05 WB** (a Active 20 não tem Ethernet integrada). | Datasheets JFL; changelog JFL (ME-05) |
| A central pode reportar para **2 destinos** (IP1/IP2, modo duplo 700 tecla 8) e a janela de supervisão é o **keep-alive** programado no Active NET (2 a 10 min na tabela de consumo). | Manual JFL 14, 14.3 |
| O protocolo "padrão" de eventos é **Contact ID**; as centrais Active 20 Bus / 32 Duo / 100 Bus usam o "novo protocolo de reporte" ao qual o software de monitoramento (Moni, Sigma, Condor etc.) precisa ser compatível. | Manual JFL 14; changelog JFL (Active 20 Ultra 6.0) |
| **Nuvem/aplicativo é outro canal** (MQTT com TLS na porta 8883, mais 8245 e NTP 123). Nada disso é usado aqui. | Manual JFL 14.6 |
| O quadro binário (`7B`, tamanho total, sequência, comando, dados, XOR) é o do "PDF de comandos Active 100/32/20 Bus/20/8/20 Eth", **não público**; vem de implementações independentes (jmceara/pyjfl, fernac03/JFL_ACTIVE) e foi **conferido byte a byte contra capturas reais de uma Active 32 Duo fw 7.60**. | pyjfl (fixtures), fernac03 |

> **Nenhum byte capturado de uma Active 100 Bus ou de uma Active 20 foi encontrado em fonte pública.** O que vale para os dois modelos é o *layout documentado da família* + os tetos oficiais dos datasheets. Por isso nada foi assumido como "igual à 32 Duo" sem ficar marcado.

## 2. Diferenças entre os modelos (tetos oficiais)

| | Active 100 Bus (PCI-350) | Active 20 (PCI-325) | Active 32 Duo (PCI-356) — só referência |
|---|---|---|---|
| Zonas | 99 | 32 | 32 |
| Partições reais | 16 | 2 (+1 de eletrificador) | 4 |
| PGMs | 1 na placa, até **16** (módulo PGM Bus) | 1 na placa, até **4** | 4 |
| Buffer de eventos | 1024 | 256 | — |
| Ethernet | ME-04 / ME-05 | ME-05 WB | — |
| Firmware citado no changelog | 4.0 → 7.1 | 6.5 → 8.0 | 7.60 (capturas) |
| Byte de modelo (deslocamento 41 do quadro 0x21) | `0xA4` *(pyjfl, não confirmado em hardware)* | `0xA7` *(pyjfl, não confirmado em hardware)* | `0xA0` *(capturado)* |

O CentralHub aplica esses tetos: PGM 17 na Active 100 Bus, PGM 5 ou partição 3 na Active 20 são recusados **sem enviar nada** à central ([JflModelProfile.cs](../SDK/CentralHub.SDK/Adapters/Jfl/JflModelProfile.cs)). Os demais modelos Active/M-300 são reconhecidos pelo byte, **recebem eventos, mas têm comandos desabilitados**.

## 3. O que foi implementado

Legenda de evidência: **OF** = documentação oficial JFL · **CAP** = captura real (Active 32 Duo) · **ESP** = especificação citada pelo pyjfl, sem captura · **SIM** = validado apenas com o simulador de teste · **HW** = precisa de teste em hardware.

| Funcionalidade | Active 100 Bus | Active 20 | Evidência | Pendente em hardware |
|---|:-:|:-:|---|---|
| Servidor de monitoramento (central conecta no CentralHub) | ✔ | ✔ | OF (topologia) · SIM | Confirmar conexão do ME-05 com o servidor |
| Identificação: série, modelo, firmware, MAC, IMEI | ✔ | ✔ | CAP (offsets) | Byte de modelo `0xA4`/`0xA7`; texto do firmware |
| Conta do monitoramento | ✔ (do evento) | ✔ (do evento) | CAP | A lista de contas do quadro 0x21 (offsets 52-83) **não** é decodificada |
| Estado de conexão (online/offline, reconexão, watchdog) | ✔ | ✔ | SIM | Intervalo real do keep-alive de cada firmware |
| Confirmação do 0x21 (aceitar/recusar central) | ✔ | ✔ | CAP | — |
| Confirmação de evento 0x24 e de keep-alive 0x40 | ✔ | ✔ | ESP (formato) · SIM | Confirmar que a central para de retransmitir |
| Recebimento de eventos Contact ID + tabela do manual | ✔ | ✔ | CAP (3 eventos) · OF (tabela, seção 29) | Codes específicos de cada modelo |
| Consulta de status | ✔ | ✔ | CAP | Tamanho/layout do quadro de status em cada modelo |
| Partições (estado) | 16 | 2 | CAP · OF (tetos) | — |
| Zonas (aberta/fechada/disparo/anulada/problema) | 99 | 32 | CAP · OF (tetos) | Nibbles acima de 32 zonas (Active 100 Bus) |
| Arme / desarme por partição | ✔ | ✔ | CAP | Comportamento com tempo de saída e usuários restritos |
| PGM ligar / desligar (confirmado pelo status) | 1-16 | 1-4 | CAP · OF (restrições) | PGMs 9-16 (byte 117/118) na Active 100 Bus |
| PGM pulso | ✔ | ✔ | composição de comandos capturados | — |
| Bypass / reativar zona | ✔ | ✔ | CAP | Bitmap de 13 bytes na Active 100 Bus (99 zonas) |
| Persistência e consulta de eventos (`/api/event`) | ✔ | ✔ | testes de integração | — |

### Comportamentos de segurança

- **Nada é "simulado como sucesso"**: fabricante não identificado → falha explícita; JFL desconectada → falha com orientação; comando só é sucesso quando o **status devolvido pela central confirma** a mudança. Falhas aparecem no histórico com o prefixo `FALHA:`.
- **Somente comandos sem autenticação** (`0x4E/4F/50/51/52`). A família `0x37` com senha **não foi implementada**: 5 senhas erradas bloqueiam o acesso remoto da central, e não há retentativa em lugar nenhum.
- Antes de ligar/desligar uma PGM o CentralHub lê o status e confere o bit de permissão (P-PGM). Bit zerado = a PGM não é "acionada pelo usuário" e o comando **não é enviado**.
- `Jfl:ReadOnly = true` bloqueia qualquer comando que altere a central.
- Pulso é **por software** (ligar → esperar → desligar); o desligamento é sempre tentado e, se não for confirmado, o resultado diz isso.

## 4. Limitações e armadilhas conhecidas (importante para operação)

1. **PGM remota só funciona se a PGM estiver programada como "com retenção / sem retenção acionada pelo usuário"** (funções 12 e 13). O changelog informa que, a partir da **fw 7.0** (Active 100 Bus e Active 20), outras funções também podem ser acionadas por software; o manual 7.3+ ainda descreve só 12/13. O CentralHub não presume: usa o bit de permissão informado pela própria central.
2. **PGM com função 18/25 comanda o eletrificador** (manual). Não exponha essa PGM como botão comum.
3. Eventos de arme/desarme/PGM comandados por software aparecem como **usuário 99** (manual, item 9).
4. O **usuário mestre pode restringir o que o monitoramento faz** (menu "Opções do monitoramento", item 9.6, que tem prioridade sobre o Active NET). Se a central recusar arme/desarme/PGM/bypass, é esta configuração ou o perfil do usuário.
5. **Bypass substitui o conjunto inteiro** de zonas anuladas (13 bytes). O CentralHub lê o conjunto atual e reenvia com a zona alterada; zonas *auto-anuladas* não aparecem no status e podem ser afetadas (não documentado).
6. **Eletrificador, arme STAY/AWAY separados, leitura/gravação de programação (0x44/0x45), download do buffer de eventos (0x48), inventário RF e a família autenticada 0x37 não foram implementados.**
7. Gerações antigas/diferentes do protocolo (cabeçalho `0xB3` para Active 32 Duo fw < 5.0 e `0x7A` para Active 8W/QC/Vulcano) **não são suportadas**: a conexão é encerrada e registrada em log.
8. Uma conexão nova do mesmo número de série **substitui** a anterior (a central pode voltar com outro IP).
9. Com **dois destinos** (IP1/IP2) programados para o mesmo CentralHub, a central pode abrir duas conexões com o mesmo número de série; como a última substitui a anterior, isso pode gerar vai-e-vem. Programe apenas um destino para o CentralHub.
10. Licenças: o código de referência `fernac03/JFL_ACTIVE` não declara licença e o `pyjfl` é Apache-2.0 com um README que descreve obrigações adicionais. **Nada foi copiado**: o código C# foi escrito a partir dos layouts documentados e conferido contra os bytes capturados.

## 5. Como usar

### Configurar a central JFL
1. Habilitar reporte via Ethernet (endereço **700, tecla 3**).
2. IP de destino = IP do servidor do CentralHub (**702/703**); porta (**706/707**) = `Jfl:Port` (padrão **9090**, no máximo 9999).
3. Liberar a porta TCP no firewall do servidor.
4. Em modo duplo, programar só um destino para o CentralHub (ver limitação 9).

### Configurar o CentralHub (`appsettings.json`, seção `Jfl`)
| Chave | Padrão | Função |
|---|---|---|
| `Enabled` | `true` | Liga/desliga o servidor de monitoramento |
| `Port` | `9090` | Porta de escuta (1-9999 para ser programável na central) |
| `BindAddress` | `0.0.0.0` | Interface de escuta |
| `KeepAliveMinutes` | `5` | Intervalo devolvido à central (1-20) |
| `AcceptUnknownPanels` | `true` | `false` = só aceita séries cadastradas |
| `ReadOnly` | `false` | Bloqueia PGM/arme/bypass |
| `LogRawFrames` | `false` | Loga todos os quadros em hexadecimal (use para validar modelos) |

### Cadastrar a Central no CentralHub
Campos do cadastro (a tela ganhou dicas): **Usuário = número de série da central** (10 dígitos, aparece no log e nos eventos); IP, Porta e Senha são obrigatórios pela API mas **ignorados** para JFL. Use *Testar Conexão* com a central já conectada: o CentralHub identifica `Fabricante = JFL`, modelo e firmware pela sessão.

### API
| Método | Rota | Função |
|---|---|---|
| POST | `/api/operation/enviar` | PGM `Ligar` / `Desligar` / `Pulso` |
| POST | `/api/operation/particao` | `Armar` / `Desarmar` partição |
| POST | `/api/operation/bypass` | `Anular` / `Reativar` zona |
| GET | `/api/operation/particoes/{centralId}` | Estado das partições |
| GET | `/api/operation/zonas/{centralId}` | Estado das zonas |
| GET | `/api/event?centralId=&serial=&limite=` | Eventos recebidos |

## 6. Testes

`Tests/CentralHub.SDK.Tests` (98) e `Tests/CentralHub.Api.Tests` (4) — `dotnet test CentralHub.sln`.

| Tipo | O que prova | O que **não** prova |
|---|---|---|
| **Capturas reais** (Active 32 Duo fw 7.60, `RealCaptures.cs`) | O codec reproduz **byte a byte** arme, desarme, PGM ligar/desligar, bypass (MSB-first) e ack de conexão; decodifica conexão, eventos e status reais. | Nada sobre Active 100 Bus / Active 20. |
| **Perfis de modelo** | Tetos de cada modelo = datasheets oficiais; demais modelos sem comando. | Que os bytes de modelo estejam corretos. |
| **Simulador** (`Simulation/JflPanelSimulator.cs`, derivado das capturas) | Servidor, acks, registro por série, reconexão, watchdog, recusa de central/geração de protocolo, adapter ponta a ponta, confirmação pelo status, recusas por modelo, API completa e banco legado. | Compatibilidade com equipamento real. **O simulador não é captura.** |

Uma mutação deliberada no decodificador (inverter a ordem dos bits do P-INIB) faz 2 testes falharem — os testes detectam regressões de protocolo.

## 7. Checklist de validação em hardware (a ser feito por quem tiver acesso)

Ative `LogRawFrames=true` e guarde o log. Para **cada** modelo:

1. Conectar e conferir no log: byte de modelo (`0xA4` / `0xA7`), firmware, número de série.
2. Provocar um evento (abrir zona/arme/desarme/falta de AC) e confirmar que a central **para de retransmitir**.
3. `GET /api/operation/zonas` e `/particoes` conferem com o teclado (especialmente zonas > 32 e partições 3-16 na Active 100 Bus).
4. Arme e desarme de uma partição de teste (sirene desconectada); testar com zona aberta.
5. PGM de teste programada como função 12 ou 13: ligar, desligar e pulso. Na Active 100 Bus, repetir com uma PGM 9-16 (módulo PGM Bus).
6. Bypass e reativação de uma zona; conferir no teclado.
7. Derrubar a rede, esperar a central rediscar e confirmar a substituição da sessão.
8. Enviar o log (`rx`/`tx`) para comparar os quadros com os de `RealCaptures.cs` e ajustar os perfis se algo divergir.

## 8. Arquivos principais

- Protocolo: [Jfl/JflFrame.cs](../SDK/CentralHub.SDK/Adapters/Jfl/JflFrame.cs), [JflDecoder.cs](../SDK/CentralHub.SDK/Adapters/Jfl/JflDecoder.cs), [JflEncoder.cs](../SDK/CentralHub.SDK/Adapters/Jfl/JflEncoder.cs), [JflModelProfile.cs](../SDK/CentralHub.SDK/Adapters/Jfl/JflModelProfile.cs), [JflContactIdTable.cs](../SDK/CentralHub.SDK/Adapters/Jfl/JflContactIdTable.cs)
- Servidor/sessões: [JflServer.cs](../SDK/CentralHub.SDK/Adapters/Jfl/JflServer.cs), [JflSession.cs](../SDK/CentralHub.SDK/Adapters/Jfl/JflSession.cs)
- Adapter: [JflAdapter.cs](../SDK/CentralHub.SDK/Adapters/JflAdapter.cs) (+ `IParticaoAdapter`, `IZonaAdapter`)
- Backend: `JflListenerService`, `EventController`, `SchemaUpdater` (tabela `Events` em bancos antigos), `OperationService`
- Removido: `JflActiveProtocol.cs` (quadros com tamanho errado e comandos trocados — ver `AUDITORIA_CENTRALHUB.md`)
