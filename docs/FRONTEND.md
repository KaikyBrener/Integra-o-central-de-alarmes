# Frontend do CentralHub — painel JFL

React 18 + TypeScript + Vite, com **Bootstrap 5**, **Bootstrap Icons** e **SweetAlert2**. O Material UI foi removido; não há mistura de bibliotecas de componentes. Toda a interface está em português.

## Executar

```
cd Frontend
npm install
npm run dev          # http://localhost:5173
npm run build        # tsc -b && vite build
```

A API padrão é `http://localhost:5000/api`. Para usar outra, crie `Frontend/.env.local` com `VITE_API_URL=http://host:porta/api`.

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `components/Layout.tsx` | Sidebar responsiva (vira gaveta abaixo de 992 px), barra superior com o seletor da central JFL e o estado do servidor |
| `components/ui.tsx` | Breadcrumbs, cabeçalho de página, selo de suporte e tabela "Recursos deste módulo" |
| `components/ComCentralJfl.tsx` | Só mostra o conteúdo quando a central está **selecionada e conectada**; caso contrário explica o motivo (não há tela "fingindo" dados) |
| `components/Grades.tsx` | Grades de zonas, partições e PGMs, limitadas ao teto do modelo |
| `context/JflContext.tsx` | Centrais cadastradas, sessões JFL conectadas e servidor (atualiza a cada 5 s); central selecionada fica salva no navegador |
| `hooks/usePolling.ts` | Polling e `useJflStatus` (cada leitura envia um pedido de status 0x4D; padrão 10 s, o Active NET usa ~12,5 s) |
| `utils/masks.ts` | Máscaras: IP, MAC, telefone, série (10 dígitos), horário, porta |
| `utils/alerts.ts` | SweetAlert2: confirmação, carregamento, resultado de comando (`FALHA:` → erro) |

## Módulos e classificação dos recursos

Cada tela traz a tabela **"Recursos deste módulo"** com um destes selos: **Funcional** (lê/envia comandos reais), **Somente consulta** (dado real sem comando de alteração), **Aguardando suporte do protocolo** (depende de comandos não implementados, principalmente leitura/gravação da programação 0x44/0x45) e **Não suportado**.

| Módulo | Funcional | Somente consulta | Aguardando protocolo |
|---|---|---|---|
| Dashboard | sessões conectadas, identificação (série, modelo, placa, firmware, MAC, origem), status, eventos recentes | indicadores, problemas, bateria, relógio, eletrificador | — |
| Eventos | lista, filtros, exportação CSV | — | — |
| Logs | RX/TX hex + interpretação, ACK, erros, timeouts, (re)conexões, filtros por série/direção/tipo/texto/horário, pausa, CSV/JSON, limpar | — | histórico persistente (log é em memória: *não suportado*) |
| Zonas | estado, anular/reativar | "permite inibir" (P-INIB) | nome, tipo, partição, atributos, edição |
| Partições | estado, armar/desarmar | estado do eletrificador | STAY/AWAY por comando, comando do eletrificador, tempos, autoarme, nomes |
| PGMs | estado, ligar/desligar/pulso | permissão de acionamento remoto (P-PGM) | função, tempo, agendamento, nome |
| Dispositivos | — | problemas de teclado/sirene/barramento/sensor/tamper; zonas com problema de dispositivo; capacidades do datasheet | inventário sem fio (0x59), teclados/módulos (0x47), controles, cadastro |
| Programação | — | porta do servidor (destino a programar na central) | **todas as seções** (mapa de endereços do manual oficial, sem campos editáveis) |
| Centrais / Prédios / Comandos | cadastro, teste de conexão real, comandos de PGM, histórico | — | — |

**Diferenças por modelo respeitadas:** a Active 100 Bus mostra 99 zonas, 16 partições e 16 PGMs; a Active 20 mostra 32 zonas, 2 partições e 4 PGMs. Comandos fora do teto não são oferecidos (e o backend também os recusa). Notas de endereço específicas (403xx, 404xx, 421xx, 511xx, 515xx na Active 100 Bus; função 25 só na Active 20) aparecem na Programação e nas telas correspondentes.

**Controles só aparecem quando funcionam:** sem central conectada, com o modelo sem comandos habilitados ou com `Jfl:ReadOnly = true`, os botões de comando não são exibidos. PGM sem permissão remota e zona que não permite inibir ficam sem ação, com o motivo.

**Máscaras:** IP, série e porta são usadas no cadastro de Centrais; horário nos filtros de Logs; MAC na exibição. A máscara de telefone existe em `utils/masks.ts`, mas nenhuma tela tem campo de telefone editável hoje (telefones de reporte ficam na programação, ainda não suportada).

## Novos endpoints usados (backend)

| Rota | Função |
|---|---|
| `GET /api/jfl/servidor` | Porta em uso, keep-alive, somente leitura, aceitação de centrais |
| `GET /api/jfl/modelos` | Perfis de modelo (tetos e validação) |
| `GET /api/jfl/sessoes` | Centrais conectadas (+ vínculo com o cadastro) |
| `GET /api/jfl/sessoes/{serie}/status?atualizar=true` | Status decodificado, limitado ao modelo; se a central não responder, devolve o último com aviso |
| `GET /api/jfl/log` · `DELETE /api/jfl/log` | Log de protocolo (filtros, leitura incremental por `desdeId`) |

Os comandos continuam nos endpoints existentes (`/api/operation/enviar`, `/particao`, `/bypass`).

## Verificação feita

- `npm run build` sem erros (TypeScript estrito).
- Testes do backend: 102 (SDK) + 5 (API), incluindo o log de protocolo e os novos endpoints.
- Teste de fumaça: API isolada (porta 5055, servidor JFL 9191) com central **simulada** como Active 100 Bus. Teste de conexão, cadastro, PGM 12, arme da partição 16, evento com ACK, log RX/TX e CORS para `localhost:5173` responderam como a interface espera.
- **Não foi possível abrir a interface num navegador nesta sessão.** Layout e interação visual precisam de conferência manual (`npm run dev`).
