# CentralHub MVP

Estrutura do projeto:

```
CentralHub/
├── Backend/CentralHub.Api    -> ASP.NET Core 9 Web API + EF Core + SQLite + Swagger
├── SDK/CentralHub.SDK        -> Adapters (ICentralAdapter, IntelbrasAdapter, JflAdapter, FakeAdapter, AdapterFactory)
└── Frontend                  -> React + TypeScript + Vite + Bootstrap 5 + Bootstrap Icons + SweetAlert2 + Axios + React Router
```

## Backend

```
cd Backend/CentralHub.Api
dotnet restore
dotnet run
```

Swagger disponível em: http://localhost:5000/swagger
O banco SQLite (`centralhub.db`) é criado automaticamente na primeira execução.

## Frontend

```
cd Frontend
npm install
npm run dev
```

Aplicação disponível em: http://localhost:5173

## Fluxo de uso

1. Cadastrar um Prédio.
2. Cadastrar uma Central vinculada ao Prédio, informando IP, Porta, Usuário e Senha.
3. Clicar em "Testar Conexão" para identificar Fabricante, Modelo, Firmware, Status e Latência.
4. Salvar a Central.
5. Na tela de Operação, selecionar Prédio, Central e PGM.
6. Escolher o comando (Pulso, Ligar, Desligar) e enviar.
7. O histórico do comando é registrado e exibido na tela.

## Centrais JFL (Active 100 Bus e Active 20)

O CentralHub atua como servidor de monitoramento das centrais JFL Active (a central conecta nele): recebe eventos,
mostra o estado de partições/zonas e envia PGM, arme/desarme e bypass. Detalhes, evidências por modelo,
limitações e checklist de validação em hardware em [docs/INTEGRACAO_JFL.md](docs/INTEGRACAO_JFL.md).

Painel (Dashboard, Eventos, Logs de protocolo, Zonas, Partições, PGMs, Dispositivos, Programação): ver [docs/FRONTEND.md](docs/FRONTEND.md).

Testes: `dotnet test CentralHub.sln` (capturas reais da Active 32 Duo + simulador de central; o simulador **não** é hardware).
