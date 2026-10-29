# CentralHub

![CI](https://github.com/KaikyBrener/Integra-o-central-de-alarmes/actions/workflows/ci.yml/badge.svg)

MVP para cadastrar e operar centrais de alarme de fabricantes diferentes (Intelbras e JFL) a partir de uma única interface. O sistema cadastra prédios e centrais, testa a conexão TCP com cada central, tem a estrutura para enviar comandos às saídas PGM e registra o histórico de cada operação. Os protocolos reais dos fabricantes ainda não estão implementados (veja "Estado atual").

## Funcionalidades

- Cadastro de prédios e das centrais de cada prédio (IP, porta, usuário e senha).
- Teste de conexão TCP com a central, que informa status e latência. Os adapters são tentados começando pelo que tem a porta padrão igual à informada (Intelbras ou JFL).
- Comandos de PGM (pulso, ligar e desligar) com histórico, incluindo se o comando teve sucesso.
- Verificação periódica de conectividade das centrais cadastradas (serviço em segundo plano, a cada 30 segundos).
- A senha da central é criptografada no banco, nunca é devolvida pela API nem registrada em log.
- Toda a API exige a chave no header `X-Api-Key`.

## Estado atual

O que funciona de verdade:

- Teste de conexão TCP (IP e porta) com medição de latência, nos adapters da Intelbras e da JFL.
- Sem hardware: se nenhuma conexão TCP for possível, o teste de conexão devolve `sucesso = false` com o erro da última tentativa. O `FakeAdapter` só substitui essa falha quando a API roda em ambiente `Development` **e** `UsarAdapterFake` é `true` (é o caso de `appsettings.Development.json`; o padrão em `appsettings.json` é `false`).
- Histórico de comandos com o campo `sucesso`. Um comando que o adapter reporta como falho é gravado e devolvido com `sucesso = false`, e a tela de Operação mostra a falha.
- Validação dos comandos: o comando precisa ser Pulso, Ligar ou Desligar, o PGM deve ser maior que zero e o pulso precisa de um tempo maior que zero. Central inexistente devolve 404.
- Datas do histórico gravadas em UTC.
- Senha da central criptografada no SQLite com ASP.NET Core Data Protection (valores gravados com o prefixo `enc:v1:`).
- Autenticação por chave de API no header `X-Api-Key`. A página `/swagger` em si é a única rota liberada.

O que ainda é simulado (não fala com a central):

- **Modelo e firmware** são valores fixos dos adapters (`AMT 8000` / `2.4.1` na Intelbras e `Active 20` / `3.1.0` na JFL), não lidos da central.
- **Comandos de PGM** dos adapters da Intelbras e da JFL só montam a mensagem de resposta e sempre retornam sucesso (há `TODO` no código). Por isso, hoje, `sucesso = true` nesses adapters não significa que a central executou o comando.
- O `FakeAdapter` simula tudo e sempre tem sucesso.

## Arquitetura

```
CentralHub/
├── Backend/CentralHub.Api       API web: ASP.NET Core 9, EF Core, SQLite e Swagger
├── SDK/CentralHub.SDK           Adapters por fabricante (ICentralAdapter, IntelbrasAdapter,
│                                JflAdapter, FakeAdapter e AdapterFactory)
├── Tests/CentralHub.SDK.Tests   Testes unitários do SDK (xUnit)
├── Tests/CentralHub.Api.Tests   Testes de integração da API (xUnit, WebApplicationFactory, SQLite em memória)
└── Frontend                     React, TypeScript, Vite, Material UI, Axios e React Router
```

Cada fabricante implementa a mesma interface `ICentralAdapter`, então a API trata todas as centrais do mesmo jeito. Para dar suporte a um novo fabricante, basta criar um adapter e registrá-lo no `AdapterFactory`. O `FakeAdapter` simula uma central e permite desenvolver e testar sem hardware.

## Como rodar

Requisitos: .NET 9 SDK e Node.js 20 ou superior.

### Chave de API (obrigatória)

A API não sobe sem a configuração `ApiKey`, e toda requisição precisa enviar o mesmo valor no header `X-Api-Key`. A chave não fica em nenhum arquivo versionado. Escolha uma das formas:

```
# User Secrets (recomendado em desenvolvimento)
cd Backend/CentralHub.Api
dotnet user-secrets set "ApiKey" "uma-chave-longa-e-aleatoria"

# ou variável de ambiente
export ApiKey="uma-chave-longa-e-aleatoria"        # Linux/macOS
$env:ApiKey = "uma-chave-longa-e-aleatoria"        # PowerShell
```

No frontend, copie `Frontend/.env.example` para `Frontend/.env.local` e preencha `VITE_API_KEY` com a mesma chave. No Swagger, use o botão "Authorize" e informe a chave.

### Backend

```
cd Backend/CentralHub.Api
dotnet restore
dotnet run
```

Swagger em http://localhost:5000/swagger. O banco SQLite (`centralhub.db`) é criado na primeira execução. O perfil de execução padrão usa o ambiente `Development`, em que o teste de conexão cai no `FakeAdapter` quando a central não responde.

As chaves usadas para criptografar as senhas ficam na pasta `keys` ao lado da API (ou no caminho de `DataProtection:KeysPath`). Veja "Limitações".

### Frontend

```
cd Frontend
npm install
npm run dev
```

Aplicação em http://localhost:5173.

## Testes

```
dotnet test Tests/CentralHub.SDK.Tests/CentralHub.SDK.Tests.csproj
dotnet test Tests/CentralHub.Api.Tests/CentralHub.Api.Tests.csproj
```

Os testes da API sobem a aplicação em memória com SQLite em memória e usam uma chave de API própria, sem precisar de configuração. Os testes de conexão com porta fechada esperam a recusa da conexão TCP e levam alguns segundos.

A integração contínua (GitHub Actions) compila a API e o frontend e roda os testes a cada push e pull request.

## Fluxo de uso

1. Cadastrar um prédio.
2. Cadastrar uma central vinculada ao prédio, informando IP, porta, usuário e senha.
3. Clicar em "Testar Conexão" para verificar a conectividade TCP e ver status e latência.
4. Salvar a central.
5. Na tela de Operação, selecionar prédio, central e PGM.
6. Escolher o comando (Pulso, Ligar, Desligar) e enviar.
7. O histórico do comando é registrado e exibido na tela, com o status de sucesso ou falha.

## Limitações

- Os protocolos reais da Intelbras e da JFL não estão implementados: modelo e firmware são fixos e os comandos de PGM são simulados e sempre retornam sucesso (veja "Estado atual").
- A escolha do fabricante no teste de conexão é só uma heurística de porta seguida de uma conexão TCP. Qualquer host que aceite conexão na porta informada é tratado como Intelbras ou JFL.
- Uma central salva com fabricante vazio, "Fake" ou desconhecido usa o `FakeAdapter` nos comandos e na verificação periódica, em qualquer ambiente. Nesse caso os comandos são simulados e aparecem como sucesso.
- A chave de API é única e compartilhada: não há usuários, perfis nem auditoria por usuário. No frontend, `VITE_API_KEY` é embutida no JavaScript entregue ao navegador, então qualquer pessoa que consiga abrir o frontend consegue ler a chave. Use apenas em rede local ou confiável e com HTTPS fora do `localhost`.
- As chaves do Data Protection ficam em arquivos XML sem proteção adicional. Quem tiver acesso à pasta `keys` e ao banco consegue descriptografar as senhas, e quem perder a pasta `keys` perde as senhas já gravadas (é preciso recadastrá-las). Faça backup da pasta e nunca a versione.
- Senhas gravadas antes da criptografia continuam em texto simples no banco até a central ser salva de novo.
- O projeto não usa migrations: o banco é criado com `EnsureCreated` e há apenas um ajuste pontual para adicionar a coluna `Sucesso` a bancos antigos. Registros antigos do histórico ficam com `Sucesso = true`, e as datas antigas, gravadas em hora local, passam a ser lidas como UTC.
- Os testes cobrem o SDK e a API; o frontend ainda não tem testes automatizados, e nenhum teste exercita protocolos reais de central.
