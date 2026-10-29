# CentralHub

![CI](https://github.com/KaikyBrener/Integra-o-central-de-alarmes/actions/workflows/ci.yml/badge.svg)

MVP para cadastrar centrais de alarme de fabricantes diferentes (Intelbras e JFL) e operá-las a partir de uma única interface. A arquitetura de adapters por fabricante já está pronta; a comunicação real com os protocolos proprietários ainda está em desenvolvimento (veja "Estado atual").

## Funcionalidades

- Cadastro de prédios e das centrais de cada prédio (IP, porta, usuário e senha).
- Teste de conexão TCP real (IP e porta), com latência e escolha do adapter pela porta padrão do fabricante.
- Fluxo de comandos de PGM (pulso, ligar e desligar) com validação e histórico.
- Verificação periódica de conectividade das centrais cadastradas (serviço em segundo plano).
- A senha da central nunca é devolvida pela API nem registrada em log.

## Arquitetura

```
CentralHub/
├── Backend/CentralHub.Api       API web: ASP.NET Core 9, EF Core, SQLite e Swagger
├── SDK/CentralHub.SDK           Adapters por fabricante (ICentralAdapter, IntelbrasAdapter,
│                                JflAdapter, FakeAdapter e AdapterFactory)
├── Tests/CentralHub.SDK.Tests   Testes unitários do SDK (xUnit)
└── Frontend                     React, TypeScript, Vite, Material UI, Axios e React Router
```

Cada fabricante implementa a mesma interface `ICentralAdapter`, então a API trata todas as centrais do mesmo jeito. Para dar suporte a um novo fabricante, basta criar um adapter e registrá-lo no `AdapterFactory`. O `FakeAdapter` simula uma central e permite desenvolver e testar sem hardware.

## Como rodar

Requisitos: .NET 9 SDK e Node.js 20 ou superior.

**Backend**

```
cd Backend/CentralHub.Api
dotnet restore
dotnet run
```

Swagger em http://localhost:5000/swagger. O banco SQLite (`centralhub.db`) é criado na primeira execução.

**Frontend**

```
cd Frontend
npm install
npm run dev
```

Aplicação em http://localhost:5173.

## Testes

```
dotnet test Tests/CentralHub.SDK.Tests/CentralHub.SDK.Tests.csproj
```

A integração contínua (GitHub Actions) compila a API e o frontend e roda os testes a cada push e pull request.

## Fluxo de uso

1. Cadastrar um prédio.
2. Cadastrar uma central vinculada ao prédio, informando IP, porta, usuário e senha.
3. Clicar em "Testar Conexão" para identificar fabricante, modelo, firmware, status e latência.
4. Salvar a central.
5. Na tela de Operação, selecionar prédio, central e PGM.
6. Escolher o comando (Pulso, Ligar, Desligar) e enviar.
7. O histórico do comando é registrado e exibido na tela.

## Estado atual

- Os adapters da Intelbras e da JFL já fazem o teste de conectividade TCP real. Modelo e firmware ainda são valores fixos, e os comandos de PGM são **simulados** (marcados com `TODO` no código): nenhum comando é enviado à central de verdade.
- Quando nenhum adapter real consegue conectar, a detecção cai no `FakeAdapter`, para permitir desenvolvimento sem hardware. Na prática, o teste de conexão pode indicar sucesso para um endereço que não responde.
- Próximo passo: implementar os protocolos de cada fabricante e remover esse fallback em produção.

## Limitações do MVP

- As senhas das centrais são guardadas em texto simples no SQLite. Antes de qualquer uso real, é preciso criptografá-las.
- A API não tem autenticação de usuários.
- Os testes cobrem o SDK; a API e o frontend ainda não têm testes automatizados.
