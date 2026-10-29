# CentralHub

![CI](https://github.com/KaikyBrener/Integra-o-central-de-alarmes/actions/workflows/ci.yml/badge.svg)

MVP para cadastrar e operar centrais de alarme de fabricantes diferentes (Intelbras e JFL) a partir de uma única interface. O sistema cadastra prédios e centrais, testa a conexão TCP com cada central, tem a estrutura para enviar comandos às saídas PGM e registra o histórico de cada operação. Os protocolos reais dos fabricantes ainda não estão implementados (veja "Estado atual").

## Funcionalidades

- Cadastro de prédios e das centrais de cada prédio (IP, porta, usuário e senha).
- Teste de conexão TCP com a central, que informa status e latência. Os adapters são tentados começando pelo que tem a porta padrão igual à informada (Intelbras ou JFL).
- Comandos de PGM (pulso, ligar e desligar) com histórico, incluindo se o comando teve sucesso.
- Verificação periódica de conectividade das centrais cadastradas (serviço em segundo plano, a cada 30 segundos, até 10 centrais ao mesmo tempo). O status aparece como Online, Offline ou Desconhecido.
- A senha da central é criptografada no banco, nunca é devolvida pela API nem registrada em log.
- Toda a API exige a chave no header `X-Api-Key`.

## Estado atual

O que funciona de verdade:

- Teste de conexão TCP (IP e porta) com medição de latência, nos adapters da Intelbras e da JFL.
- Sem hardware: se nenhuma conexão TCP for possível, o teste de conexão devolve `sucesso = false` com o erro da última tentativa. O `FakeAdapter` só substitui essa falha quando a API roda em ambiente `Development` **e** `UsarAdapterFake` é `true` (é o caso de `appsettings.Development.json`; o padrão em `appsettings.json` é `false`).
- Histórico de comandos com o campo `sucesso`. Um comando que o adapter reporta como falho é gravado e devolvido com `sucesso = false`, e a tela de Operação mostra a falha.
- Validação dos comandos: o comando precisa ser Pulso, Ligar ou Desligar, o PGM deve ser maior que zero e o pulso precisa de um tempo maior que zero. Central inexistente devolve 404.
- Central sem fabricante reconhecido (vazio, "Fake" ou qualquer valor diferente de Intelbras e JFL): o `FakeAdapter` não é usado. Os comandos devolvem erro 422 pedindo para rodar o teste de conexão e salvar a central de novo, e a verificação periódica marca a central como `Desconhecido`. Só em `Development` com `UsarAdapterFake = true` essas centrais continuam usando o `FakeAdapter`.
- Verificação periódica em paralelo, com no máximo 10 centrais por vez. As verificações não usam o banco: os resultados são coletados e gravados juntos ao final de cada rodada. Se a verificação de uma central lançar exceção, ela é registrada em log e a central mantém o último status.
- Banco de dados criado e atualizado por migrations do EF Core (`Database.Migrate()` na inicialização). Bancos criados por versões anteriores, com `EnsureCreated`, são adotados sem recriar as tabelas: a migration inicial é registrada como aplicada e, se faltar, a coluna `Histories.Sucesso` é adicionada.
- Datas do histórico gravadas em UTC.
- Senha da central criptografada no SQLite com ASP.NET Core Data Protection (valores gravados com o prefixo `enc:v1:`).
- Autenticação por chave de API no header `X-Api-Key`. A página `/swagger` em si é a única rota liberada. Quando a API responde 401, o frontend mostra "Chave de API ausente ou inválida: confira VITE_API_KEY".

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

Swagger em http://localhost:5000/swagger. O banco SQLite (`centralhub.db`) é criado e atualizado pelas migrations na inicialização. O perfil de execução padrão usa o ambiente `Development`, em que o `FakeAdapter` é usado quando o teste de conexão não consegue conectar e para centrais sem fabricante reconhecido.

As chaves usadas para criptografar as senhas ficam na pasta `keys` ao lado da API (ou no caminho de `DataProtection:KeysPath`). Veja "Limitações".

### Migrations

O esquema do banco é controlado por migrations do EF Core, na pasta `Backend/CentralHub.Api/Migrations`. Elas são aplicadas automaticamente quando a API inicia. Para criar uma nova migration depois de alterar um model ou o `AppDbContext`, na raiz do repositório:

```
dotnet tool restore
dotnet ef migrations add NomeDaMigration --project Backend/CentralHub.Api
```

Versione os três arquivos gerados (a migration, o `.Designer.cs` e o `AppDbContextModelSnapshot.cs`). Não edite uma migration que já foi aplicada em algum banco: crie outra. Para desfazer a última migration ainda não aplicada, use `dotnet ef migrations remove --project Backend/CentralHub.Api`. Se um model mudar sem a migration correspondente, o teste `ModeloAtualCorrespondeAoSnapshotDasMigrations` falha.

A ferramenta `dotnet-ef` é instalada como ferramenta local do repositório (`dotnet-tools.json`). Os comandos acima usam o `AppDbContextFactory`, então não exigem a `ApiKey` nem um banco existente.

Um banco criado por uma versão anterior (com `EnsureCreated`, sem a tabela `__EFMigrationsHistory`) é adotado na primeira inicialização: as tabelas e os dados são mantidos e a migration inicial é registrada como aplicada.

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

Os testes da API sobem a aplicação em memória com SQLite em memória e usam uma chave de API própria, sem precisar de configuração. Eles cobrem também a verificação periódica em paralelo (com um adapter lento) e as migrations, inclusive a adoção de bancos criados com `EnsureCreated`. Os testes de conexão com porta fechada esperam a recusa da conexão TCP e levam alguns segundos.

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
- Em `Development` com `UsarAdapterFake = true`, centrais sem fabricante reconhecido continuam usando o `FakeAdapter` nos comandos e na verificação periódica, e os comandos aparecem como sucesso mesmo sem falar com nenhuma central. Centrais salvas assim em desenvolvimento passam a dar erro 422 (e status `Desconhecido`) fora dele, até serem testadas e salvas de novo.
- A chave de API é única e compartilhada: não há usuários, perfis nem auditoria por usuário. No frontend, `VITE_API_KEY` é embutida no JavaScript entregue ao navegador, então qualquer pessoa que consiga abrir o frontend consegue ler a chave. Use apenas em rede local ou confiável e com HTTPS fora do `localhost`.
- As chaves do Data Protection ficam em arquivos XML sem proteção adicional. Quem tiver acesso à pasta `keys` e ao banco consegue descriptografar as senhas, e quem perder a pasta `keys` perde as senhas já gravadas (é preciso recadastrá-las). Faça backup da pasta e nunca a versione.
- Senhas gravadas antes da criptografia continuam em texto simples no banco até a central ser salva de novo.
- Registros de histórico anteriores ao campo `Sucesso` ficam com `Sucesso = true`, e as datas antigas, gravadas em hora local, passam a ser lidas como UTC.
- As migrations só são aplicadas para frente na inicialização; não há rotina automática de reversão (`Down`). Faça backup do `centralhub.db` antes de atualizar uma instalação com dados.
- A verificação periódica é limitada a 10 centrais ao mesmo tempo e o limite não é configurável. Uma central offline ainda leva o timeout de 3 segundos do teste TCP, mas não atrasa mais as outras.
- Os testes cobrem o SDK e a API; o frontend não tem testes automatizados (o 401 e os status foram só verificados manualmente, sem teste visual no navegador), e nenhum teste exercita protocolos reais de central.
