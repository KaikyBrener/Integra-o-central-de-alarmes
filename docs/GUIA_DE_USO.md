# Guia de uso do CentralHub

Guia passo a passo para quem está começando. Cobre central JFL (Active 100 Bus, Active 20, Active 32 Duo) pelo painel web.

## 1. Como as peças se encaixam

```
Central JFL  ──(módulo ME-05, rede)──►  CentralHub API (porta 9090)  ◄──  Painel web (localhost:5173)
```

- **A central é quem se conecta ao CentralHub**, não o contrário. Por isso o IP e a porta programados *na central* devem apontar para o computador onde a API roda.
- O painel web só conversa com a API (`http://localhost:5000`). Quem fala com a central é a API.
- Enquanto a central não estiver conectada, **nenhum botão de comando aparece**: o painel não finge dados.

## 2. Subir o sistema

1. Terminal 1: `cd Backend/CentralHub.Api` e `dotnet run` (API na porta 5000, servidor JFL na 9090).
2. Terminal 2: `cd Frontend` e `npm install` (só na primeira vez) e `npm run dev`.
3. Abra o endereço que o Vite mostrar (normalmente `http://localhost:5173`). Mantenha **só um** Vite aberto.

## 3. Colocar a central para conversar com o CentralHub

Na central JFL (pelo Programador JFL ou teclado), programe o destino do módulo de rede:

| Dado | Valor |
|---|---|
| IP do servidor | IP do computador que roda a API (ex.: 192.168.0.50) |
| Porta do servidor | **9090** (a mesma de `Jfl:Port` no `appsettings.json`) |

> **Atenção:** a central e o computador do servidor precisam estar **na mesma rede** (mesma faixa de IP, ex.: 192.168.0.x). O IP privado do servidor só é alcançável de dentro da rede local; de fora (internet/4G) a central não consegue conectar, a menos que você configure redirecionamento da porta 9090 no roteador (port forwarding) ou use VPN, e use o IP público/DDNS no lugar do IP local.

Libere a porta 9090 no firewall do Windows. Quando a central conectar, ela aparece no **Dashboard**, na tabela "Centrais JFL conectadas".

## 4. Cadastrar a central

1. Vá em **Cadastros → Prédios** e crie um prédio.
2. No **Dashboard**, na linha da central conectada, clique em **Cadastrar** (já preenche série e IP). Ou vá em **Cadastros → Centrais**.
3. Escolha o modo **JFL**, confira o **número de série (10 dígitos)**, escolha o prédio e salve.
4. Selecione a central no seletor do topo da tela. A seleção fica salva.

Sem cadastro a central já envia eventos, mas fica "não vinculada" a um prédio.

## 5. Dashboard

Mostra identificação (modelo, série, firmware, MAC), partições armadas, zonas abertas, disparos, zonas anuladas, PGMs ligadas, problemas, bateria e últimos eventos. A leitura se atualiza sozinha a cada 10 s; há botão para atualizar na hora e para ligar/desligar o automático.

## 6. PGMs: Ligar, Desligar e Pulso

PGM é uma saída programável da central (portão, sirene, luz, trava...). Menu **Central JFL → PGMs**.

| Ação | O que faz | Quando usar |
|---|---|---|
| **Ligar** | Fecha a saída e **mantém** ligada até você desligar | Luz, ventilação, qualquer coisa que deve ficar ativa |
| **Desligar** | Desativa a saída | Encerrar o que foi ligado |
| **Pulso** | Liga por um tempo em **milissegundos** e desliga sozinha | Abrir portão/fechadura (precisa só de um "toque") |

Como usar:
1. Selecione a central (topo).
2. No cartão da PGM desejada, clique em **Ligar**, **Desligar** ou **Pulso**.
3. Em **Pulso**, digite o tempo em ms: `1000` = 1 segundo; `500` = meio segundo; máximo `600000` (10 min).
4. Confirme no pop-up.
5. O painel envia o comando, aguarda o status devolvido pela central e **só mostra sucesso se a central confirmar** a mudança. Se falhar, aparece a mensagem de erro (ex.: central não respondeu).

Limites por modelo: Active 100 Bus tem PGM 1 a 16; Active 20, PGM 1 a 4.

Quando a PGM **não mostra botões**: a central não está conectada, o modelo não tem comandos habilitados, o servidor está em somente leitura (`Jfl:ReadOnly = true`) ou a PGM **não permite acionamento remoto** (a central informa isso; o motivo aparece na tela).

Atenção: o pulso depende da programação da PGM na central. Se a PGM estiver programada com função própria (ex.: seguir a sirene), o comportamento pode ser diferente do esperado.

Alternativa: **Cadastros → Comandos e histórico** envia comando de PGM sem entrar na central (escolha prédio, central, PGM e comando) e lista tudo que já foi enviado.

## 7. Partições: Armar e Desarmar

Menu **Central JFL → Partições**. Uma partição é uma "área" da central que arma/desarma independente das outras (Active 100 Bus: até 16; Active 20: até 2).

- **Armar**: coloca a partição em modo armado.
- **Desarmar**: desativa.
- Cada ação pede confirmação e só conta como sucesso quando o status da central confirma o novo estado.
- Armar com zona aberta pode ser recusado pela própria central.
- Partições não programadas na central aparecem como não programadas, sem botão.

## 8. Zonas: Anular (bypass) e Reativar

Menu **Central JFL → Zonas** (Active 100 Bus: até 99; Active 20: até 32). Cada zona mostra o estado: fechada, aberta, disparada, anulada.

- **Anular**: a central passa a ignorar a zona (útil para sensor com defeito ou porta que ficará aberta). A zona anulada **não protege** até ser reativada.
- **Reativar**: volta a monitorar.
- Só zonas que a central marca como "permite inibir" têm botão.
- O comando de bypass envia o conjunto completo de zonas anuladas; o CentralHub monta isso a partir do status atual, então confirme sempre o resultado na grade.

## 9. Eventos

Menu **Monitoramento → Eventos**. Lista tudo que a central reportou (disparos, armes/desarmes, falhas), com filtros e exportação CSV. Eventos ficam gravados no banco, mesmo reiniciando a API.

## 10. Logs de protocolo (para diagnóstico)

Menu **Logs**. Mostra em tempo real cada quadro trocado com a central:
- **RX**: central → CentralHub. **TX**: CentralHub → central.
- Hexadecimal bruto + descrição em português (ex.: "0x50 Ligar PGM 2", "ACK de conexão").
- Filtros por série, direção, tipo, texto e horário; pausar; exportar CSV/JSON; limpar.
- O log é **em memória**: reiniciar a API apaga.

Use quando um comando falha: veja se o TX saiu e se a central respondeu (RX). "Timeout" significa que a central não respondeu.

## 11. Dispositivos e Programação

- **Dispositivos**: mostra apenas problemas que a central informa (bateria, sirene, teclado, tamper...) e as capacidades do modelo.
- **Programação**: é só um **mapa de referência** dos endereços do manual. **Ainda não lê nem grava a programação da central** (comandos 0x44/0x45 não implementados). Programe pelo Programador JFL.

Cada tela tem a tabela **"Recursos deste módulo"** com os selos: *Funcional*, *Somente consulta*, *Aguardando suporte do protocolo*, *Não suportado*.

## 12. Problemas comuns

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| "Não foi possível carregar os prédios / conectar à API" | API parada, ou página aberta em porta do Vite diferente da liberada no CORS | Confirme `http://localhost:5000/api/building` no navegador; reinicie a API; deixe só um Vite aberto; Ctrl+F5 |
| Central não aparece no Dashboard | Central não consegue alcançar o servidor | Confira IP/porta programados na central, firewall (porta 9090) e se estão na mesma rede |
| Aparece, mas sem botões | Modelo sem comandos habilitados, `ReadOnly` ligado ou PGM sem permissão remota | Veja o aviso no topo da tela; `Jfl:ReadOnly` no `appsettings.json` |
| Comando deu erro/timeout | Central não respondeu | Veja o **Logs**; confira conexão; tente atualizar o status |
| Modelo "Não validado em hardware" | Active 100 Bus e Active 20 foram implementadas pela documentação, sem teste em equipamento real | Teste com cuidado, primeiro em ambiente sem risco |

## 13. Segurança

- Teste armar/desarmar e PGMs primeiro numa central de bancada.
- Para só monitorar, defina `Jfl:ReadOnly = true` em `appsettings.json`: o painel esconde todos os comandos.
- Não use senha de programação na integração: comandos autenticados (família 0x37) não foram implementados de propósito, pois 5 senhas erradas bloqueiam a central.
