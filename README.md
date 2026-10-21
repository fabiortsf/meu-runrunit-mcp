# Meu Runrun.it MCP

Servidor [MCP](https://modelcontextprotocol.io/) em C# que conecta o **Runrun.it** ao seu fluxo no Cursor: lê tarefas (com comentários) e ajuda a localizar arquivos em **qualquer repositório** que você indicar — MVC 5, ASP.NET Core, React, Node, etc.

Este repositório é só o **servidor MCP**. O código que você analisa fica em **outro projeto**, em outra pasta.

---

## Índice

1. [Visão geral](#visão-geral)
2. [Pré-requisitos](#pré-requisitos)
3. [Instalação global (sem abrir este projeto)](#instalação-global-sem-abrir-este-projeto) **recomendado**
4. [Instalação para desenvolvimento](#instalação-para-desenvolvimento)
5. [Como usar em outro projeto](#como-usar-em-outro-projeto)
6. [Fluxo de trabalho recomendado](#fluxo-de-trabalho-recomendado)
7. [Ferramentas disponíveis](#ferramentas-disponíveis)
8. [Exemplos de prompts no Cursor](#exemplos-de-prompts-no-cursor)
9. [Configuração opcional](#configuração-opcional)
10. [Solução de problemas](#solução-de-problemas)
11. [Versionamento e releases](#versionamento-e-releases)
12. [Build e desenvolvimento](#build-e-desenvolvimento)
13. [API Runrun.it](#api-runrunit)

---

## Visão geral

```text
┌─────────────┐     stdio      ┌──────────────────┐     HTTPS      ┌─────────────┐
│   Cursor    │ ◄────────────► │ meu-runrunit-mcp │ ◄────────────► │  Runrun.it  │
│  (agente)   │                │  (este repo)     │                │    API      │
└─────────────┘                └────────┬─────────┘                └─────────────┘
                                        │
                                        │ lê arquivos no disco
                                        ▼
                               ┌──────────────────┐
                               │  OUTRO PROJETO   │  ← projectRoot que você informa
                               │  (MVC, React…)   │
                               └──────────────────┘
```

O parâmetro **`projectRoot`** é o caminho absoluto da **raiz do repositório alvo** (onde está a `.sln`, `package.json`, `Web.config`, etc.). Não use a pasta do MCP.

---

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download) ou superior (o app targeta `net8.0` com roll-forward para runtimes 9/10)
- [Cursor](https://cursor.com/) com MCP habilitado
- Conta Runrun.it com **App-Key** e **User-Token** (Configurações → Integrações → API)
- O **outro projeto** clonado/local no disco, com caminho que você conhece (ex.: `C:\dev\cliente\portal-web`)

---

## Instalação global (sem abrir este projeto)

Você **não precisa** ter a pasta `meu-runrunit-mcp` aberta no Cursor. O MCP é registrado **uma vez** no arquivo global do usuário e fica disponível em **qualquer workspace** (MVC, React, API, etc.).

### Como o Cursor enxerga o MCP

| Arquivo | Escopo |
|---------|--------|
| `%USERPROFILE%\.cursor\mcp.json` | **Global** — vale para todos os projetos |
| `.cursor/mcp.json` na pasta do projeto | Só aquele workspace (opcional) |

Use o **global** para não repetir config em cada cliente.

### Passo 1 — Publicar o MCP em uma pasta fixa

No PowerShell, na pasta deste repositório (só na primeira vez ou após atualizar o código):

```powershell
cd C:\dev\projetos\trabalho\meu-runrunit-mcp
.\scripts\publish-global.ps1
```

O script **verifica os User Secrets antes de publicar**:

| Situação | O que o script faz |
|----------|-------------------|
| Sem App-Key / User-Token | Pede para preencher |
| Já configurado | Pergunta: manter secrets **[S]** ou informar novas **[N]** |

As credenciais ficam em `%APPDATA%\Microsoft\UserSecrets\` — **não** na pasta da DLL. Republicar **não exige** digitar de novo, a menos que você escolha **[N]**.

Pular a verificação: `.\scripts\publish-global.ps1 -SkipSecretsCheck`  
Forçar novas credenciais: `.\scripts\publish-global.ps1 -ForceNewSecrets`

Isso gera os binários em:

```text
%LOCALAPPDATA%\MeuRunrunItMCP\
  MeuRunrunItMCP.dll
  appsettings.json
  ...
```

Equivalente manual:

```bash
dotnet publish MeuRunrunItMCP.csproj -c Release -o "%LOCALAPPDATA%\MeuRunrunItMCP"
```

### Passo 2 — Configurar o Cursor (global)

Edite `%USERPROFILE%\.cursor\mcp.json`:

```json
{
  "mcpServers": {
    "meu-runrunit": {
      "command": "dotnet",
      "args": [
        "exec",
        "C:\\Users\\SEU_USUARIO\\AppData\\Local\\MeuRunrunItMCP\\MeuRunrunItMCP.dll"
      ]
    }
  }
}
```

Substitua `SEU_USUARIO` pelo seu usuário Windows. Exemplo completo com credenciais em variáveis de ambiente: [`docs/mcp.global.example.json`](docs/mcp.global.example.json).

### Passo 3 — Credenciais Runrun.it (se ainda não configurou no publish)

**Opção A — Só credenciais (sem publish)**

```powershell
.\scripts\configure.ps1
```

Mesma lógica do `publish-global`: detecta secrets existentes e pergunta manter ou criar novas.

**Opção B — Setup completo (credenciais + publish)**

```powershell
.\scripts\setup.ps1
```

**Opção C — Variáveis no `mcp.json` (funciona sem User Secrets)**

```json
"env": {
  "RunrunIt__AppKey": "sua-app-key",
  "RunrunIt__UserToken": "seu-user-token"
}
```

### Passo 4 — Usar em qualquer outro projeto

1. Abra no Cursor **apenas** o projeto do cliente (ex.: `C:\dev\acme\portal-web`).
2. No chat, chame as tools com `projectRoot` apontando para esse projeto.
3. O MCP sobe em segundo plano via `mcp.json` global — **não** precisa do workspace do MCP.

### Atualizar o MCP após mudanças no código

```powershell
cd C:\dev\projetos\trabalho\meu-runrunit-mcp
.\scripts\publish-global.ps1
```

Na republicação, pressione **S** para manter as credenciais já salvas (padrão).

Reinicie o Cursor (ou desligue/ligue o servidor MCP nas configurações).

### Comparação das formas de instalação

| Modo | Comando no `mcp.json` | Precisa repo aberto? | Velocidade |
|------|------------------------|----------------------|------------|
| **Publicado (recomendado)** | `dotnet exec ...\MeuRunrunItMCP.dll` | Não | Mais rápido |
| `dotnet run --project` | Caminho absoluto do `.csproj` | Não (só pasta no disco) | Compila ao iniciar |
| Workspace local | `.cursor/mcp.json` no projeto | Só se usar config local | — |

---

## Instalação para desenvolvimento

Use esta seção se você está **alterando o código** do MCP. Para só **usar** em outros projetos, prefira [Instalação global](#instalação-global-sem-abrir-este-projeto).

### 1. Clone ou mantenha este repositório

Exemplo:

```text
C:\dev\projetos\trabalho\meu-runrunit-mcp
```

### 2. Credenciais Runrun.it (User Secrets)

No terminal, na pasta do MCP:

```powershell
cd C:\dev\projetos\trabalho\meu-runrunit-mcp
.\scripts\configure.ps1
```

### 3. Registre o MCP no Cursor (modo desenvolvimento)

Em `%USERPROFILE%\.cursor\mcp.json`, apontando para o `.csproj` (caminho absoluto):

```json
{
  "mcpServers": {
    "meu-runrunit": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\dev\\projetos\\trabalho\\meu-runrunit-mcp\\MeuRunrunItMCP.csproj"
      ]
    }
  }
}
```

Para uso diário em outros projetos, prefira a [instalação global publicada](#passo-1--publicar-o-mcp-em-uma-pasta-fixa).

### 4. Reinicie o Cursor

Feche e abra o Cursor (ou recarregue a janela) para o servidor MCP subir.

### 5. Confirme que as tools aparecem

No chat, o agente deve listar ferramentas como:

- `analisar_tarefa`
- `obter_informacoes_da_tarefa`
- `obter_html_da_descricao`
- `comentar_tarefa`
- `comentar_entregas_webiss`
- `search`
- `read_file`
- `validate_project_root`

---

## Como usar em outro projeto

Você **não copia** o MCP para dentro do outro projeto. Você **aponta** o MCP para a pasta dele em cada uso.

### O que é `projectRoot`

| Correto | Incorreto |
|---------|-----------|
| `C:\dev\cliente-a\portal-mvc5` (raiz do app) | `C:\dev\projetos\trabalho\meu-runrunit-mcp` |
| Pasta que contém `Controllers/`, `src/`, `.sln`, etc. | Pasta `bin\` ou `node_modules\` |

Use sempre caminho **absoluto** no Windows (`C:\...`).

### Prioridade do caminho

1. **`projectRoot` na chamada da tool** (recomendado para vários clientes/repos)
2. **`CodeAnalysis:ProjectRoot`** na config (atalho se você usa sempre o mesmo repo)
3. Se faltar os dois → erro pedindo o caminho

### Abrir o “outro projeto” no Cursor

Duas formas comuns:

**A) Workspace só do projeto alvo**  
Abra a pasta do cliente no Cursor e, no chat, informe o mesmo caminho em `projectRoot`.

**B) Workspace com MCP + projeto**  
Abra uma pasta pai ou multi-root; no prompt, passe o `projectRoot` absoluto do repo que quer analisar.

O MCP lê o disco diretamente — não depende de qual pasta está aberta no editor, desde que o caminho exista e seja legível.

### Passo a passo rápido

1. Pegue o **ID da tarefa** no Runrun.it (número na URL, ex.: `.../tasks/12345` → `12345`).
2. Defina o **caminho absoluto** do repositório alvo.
3. No Cursor, peça algo como:

```text
Use analisar_tarefa com taskId 12345 e projectRoot C:\dev\cliente-a\portal-mvc5.
Depois diga quais arquivos devo alterar e por quê.
```

4. Se precisar de detalhe, peça `read_file` com o mesmo `projectRoot` e o caminho relativo retornado na busca.

---

## Fluxo de trabalho recomendado

### Cenário: nova tarefa em um projeto MVC 5

```text
1. validate_project_root        → confirma que o caminho existe
2. analisar_tarefa              → tarefa + comentários + lista de arquivos
3. obter_html_da_descricao      → HTML da descrição e imagens embutidas, se precisar
4. read_file                    → trechos dos arquivos mais relevantes
5. (você / agente)              → plano de implementação e código
```

### Cenário: só ler a tarefa (sem código)

```text
obter_informacoes_da_tarefa com taskId 12345
```

A descrição nesse retorno vem em texto, sem tags. Para o HTML e as imagens, use `obter_html_da_descricao`.

### Cenário: outro repositório no mesmo dia

Troque apenas o `projectRoot`; credenciais Runrun.it são as mesmas:

```text
analisar_tarefa taskId 200 projectRoot C:\dev\cliente-b\api-node
```

---

## Ferramentas disponíveis

| Ferramenta | Quando usar | Parâmetros principais |
|------------|-------------|------------------------|
| `analisar_tarefa` | Fluxo completo (tarefa + busca no código) | `taskId`, **`projectRoot`**, `extraQuery?`, `maxResults?` |
| `obter_informacoes_da_tarefa` | Título, descrição em texto e comentários de usuários | `taskId` |
| `obter_html_da_descricao` | HTML completo da descrição e as imagens embutidas, cada uma com URL temporária de download | `taskId` |
| `comentar_tarefa` | Publicar um comentário com o texto que você enviar | `taskId`, `text` |
| `comentar_entregas_webiss` | Comentar a próxima versão só nas entregas homologadas do WebISS atribuídas ao usuário do token | **`siteUrl`**: página inicial do ambiente. A versão está no rodapé, em `Versão: 5.3.0.43` |
| `search` | Busca manual por termos | **`projectRoot`**, `query?`, `taskContext?`, `maxResults?` |
| `read_file` | Ler trecho de um arquivo | **`projectRoot`**, `relativePath`, `startLine?`, `endLine?` |
| `validate_project_root` | Testar caminho antes de analisar | `projectRoot?` |

Tipos de arquivo considerados na busca (entre outros): `.cs`, `.cshtml`, `.js`, `.ts`, `.tsx`, `.vue`, `.py`, `.java`, `.go`, `.json`, `.xml`, `.config`, `.sql`, `.md`.

Pastas ignoradas: `bin`, `obj`, `node_modules`, `.git`, `dist`, `packages`, etc.

### HTML da descrição e imagens

`obter_informacoes_da_tarefa` remove o HTML. `obter_html_da_descricao` devolve o corpo inteiro, inclusive as tags `<img>`.

Cada imagem que aponta para um documento do Runrun.it inclui:

- a origem (`src`)
- o id do documento
- a URL temporária vinda de `GET /documents/{id}/download` (resposta 302 para o arquivo)

### Comentário automático de versão

`comentar_entregas_webiss` só comenta a tarefa quando tudo isto vale:

- encerrada
- o usuário do token está alocado nela, pelo **id** (`GET /users/me`) ou pelo **nome**
- cliente ou projeto é WebISS
- `evaluation_status` é `approved`
- a etapa não contém "cancel"
- ainda não existe o comentário de versão

O texto é fixo. O número sai do rodapé da página, com o último trecho somado em 1:

```html
Disponível a partir da versão 5.3.0.44.
<i>Automated comment by Cursor IA</i>
```

`comentar_tarefa` continua aceitando qualquer texto em `text`.

---

## Exemplos de prompts no Cursor

Substitua `TASK_ID`, caminhos e nomes pelos seus valores reais.

### ASP.NET MVC 5 (Full Framework)

```text
Valide projectRoot C:\dev\acme\MeuPortalMvc5.
Em seguida use analisar_tarefa na tarefa TASK_ID com esse projectRoot.
Liste controllers e views impactados e um plano de alteração.
```

### ASP.NET Core

```text
analisar_tarefa taskId TASK_ID projectRoot C:\dev\acme\MinhaApiCore
```

### Frontend React / Node

```text
analisar_tarefa taskId TASK_ID projectRoot C:\dev\acme\frontend
extraQuery "checkout pagamento"
```

### Ler arquivo específico após a análise

```text
Use read_file com projectRoot C:\dev\acme\MeuPortalMvc5,
relativePath Areas/Vendas/Controllers/PedidoController.cs e startLine 1 endLine 120.
```

### HTML e imagens da descrição

```text
Use obter_html_da_descricao na tarefa TASK_ID.
Traga o HTML e a URL temporária de cada imagem embutida.
```

### Comentário de versão nas minhas entregas WebISS

```text
Use comentar_entregas_webiss com siteUrl https://portal.municipio.gov.br.
Comente só as tarefas encerradas e homologadas atribuídas a mim.
```

### Vários projetos na mesma sessão

```text
Primeiro analise a tarefa 100 no projeto C:\dev\projeto-a.
Depois analise a tarefa 200 no projeto C:\dev\projeto-b.
Use analisar_tarefa em cada um com o projectRoot correto.
```

---

## Configuração opcional

Arquivo `appsettings.json` (valores vazios por padrão):

```json
{
  "RunrunIt": {
    "BaseUrl": "https://runrun.it/api/v1.0",
    "AppKey": "",
    "UserToken": ""
  },
  "CodeAnalysis": {
    "ProjectRoot": "",
    "MaxSearchResults": 25,
    "MaxFileReadLines": 200,
    "MaxTerms": 30
  },
  "WebissRelease": {
    "Enabled": true,
    "ClientName": "WebISS",
    "VersionPageUrl": "",
    "IntervalHours": 1,
    "LookbackHours": 24,
    "OverlapMinutes": 15,
    "MaxCommentsPerRun": 20
  }
}
```

### Repositório padrão (opcional)

Se você trabalha **sempre** no mesmo repo e não quer repetir o caminho:

```bash
dotnet user-secrets set "CodeAnalysis:ProjectRoot" "C:\dev\meu-repo-padrao"
```

No chat pode omitir `projectRoot` — o MCP usa esse padrão. Para outro projeto, passe `projectRoot` na chamada (ele tem prioridade).

`WebissRelease:VersionPageUrl` é opcional. Sem ela, a verificação automática não comenta e `comentar_entregas_webiss` exige `siteUrl` na chamada. `LookbackHours` define a janela das tarefas encerradas (padrão 24 h) e `MaxCommentsPerRun` limita quantos comentários saem em cada execução (padrão 20).

### Variáveis de ambiente (alternativa)

No `mcp.json`, se preferir não usar User Secrets:

```json
{
  "mcpServers": {
    "meu-runrunit": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\...\\MeuRunrunItMCP.csproj"],
      "env": {
        "RunrunIt__AppKey": "sua-key",
        "RunrunIt__UserToken": "seu-token",
        "CodeAnalysis__ProjectRoot": "C:\\dev\\repo-opcional"
      }
    }
  }
}
```

---

## Solução de problemas

| Problema | Causa provável | O que fazer |
|----------|----------------|-------------|
| `Credenciais Runrun.it ausentes` | App-Key / User-Token não configurados | `.\scripts\configure.ps1` ou `env` no `mcp.json` |
| `Informe projectRoot...` | Caminho não passado e config vazia | Informe `projectRoot` absoluto no prompt |
| `ProjectRoot não encontrado` | Caminho errado ou pasta não existe | Confira o disco; use `validate_project_root` |
| `não parece um repositório de código` | Pasta vazia ou só documentação | Aponte para a raiz da solution/app |
| Nenhum arquivo na busca | Termos da tarefa muito genéricos | Use `extraQuery` com nomes de tela, controller, módulo |
| Tools não aparecem no Cursor | MCP não carregou | Verifique `mcp.json`, `dotnet build`, reinicie o Cursor |
| Erro ao compilar o MCP | SDK incompatível | Instale .NET 8 SDK ou superior |
| Erro ao iniciar o MCP publicado | Runtime ausente | Instale [.NET 8 Runtime](https://dotnet.microsoft.com/download) (8, 9 ou 10 com roll-forward) |

---

## Compatibilidade .NET

O projeto compila para **`net8.0`** com **`RollForward: Major`** no `.csproj`. Isso significa:

- **Build:** .NET 8 SDK ou superior.
- **Execução (publish global):** qualquer máquina com **.NET 8, 9 ou 10** runtime instalado.
- Não é necessário ter exatamente a mesma versão do SDK usada no desenvolvimento.

Após alterar a versão do framework, rode `.\scripts\publish-global.ps1` de novo e reinicie o Cursor.

## Versionamento e releases

Este repositório usa **Conventional Commits**, **Semantic Versioning** e **Semantic Release** (via Node.js apenas para tooling de git/release — o app continua em .NET).

### Pré-requisito

- [Node.js](https://nodejs.org/) 20+ (para Commitizen, Commitlint e Husky localmente)

Na primeira vez, na pasta do repositório:

```powershell
npm install
```

O script `prepare` configura o **Husky** e valida mensagens de commit no hook `commit-msg`.

### Commits no dia a dia

| Ferramenta | Uso |
|------------|-----|
| **Commitizen** | `npm run commit` — assistente interativo (`feat`, `fix`, `chore`, etc.) |
| **Commitlint** | Valida automaticamente no `git commit` (hook Husky) |
| **Conventional Commits** | Formato: `tipo(escopo opcional): descrição` |

Exemplos válidos:

```text
feat: adicionar busca por tag na API Runrun.it
fix(tools): corrigir projectRoot com barras invertidas
chore: atualizar dependências MCP
```

Commits que **não** seguem o padrão são rejeitados localmente antes de subir.

### Releases automáticas (Semantic Release)

Ao fazer **push na branch `main`**, o workflow [`.github/workflows/release.yml`](.github/workflows/release.yml):

1. Analisa commits desde a última tag (`feat` → minor, `fix` → patch, `BREAKING CHANGE` → major).
2. Gera/atualiza **`CHANGELOG.md`** (Conventional Changelog).
3. Atualiza `<Version>` em `MeuRunrunItMCP.csproj` e `package.json`.
4. Cria **tag Git** e **GitHub Release** com notas.

O commit de release usa `[skip ci]` para não disparar outro release em loop.

**Importante:** commits antigos fora do padrão convencional não entram no cálculo da próxima versão. A partir da adoção deste fluxo, use sempre `npm run commit` ou mensagens no formato conventional.

### Referência rápida de tipos

| Tipo | Versão (SemVer) | Exemplo |
|------|-----------------|---------|
| `feat` | minor | nova tool MCP |
| `fix` | patch | correção de bug |
| `perf` | patch | melhoria de performance |
| `BREAKING CHANGE` no corpo ou `!` após tipo | major | remoção de parâmetro |

Documentação: [Conventional Commits](https://www.conventionalcommits.org/), [Semantic Versioning](https://semver.org/).

## Build e desenvolvimento

```bash
cd C:\dev\projetos\trabalho\meu-runrunit-mcp
dotnet build MeuRunrunItMCP.slnx
```

Executar localmente (teste):

```bash
dotnet run --project MeuRunrunItMCP.csproj
```

## API Runrun.it

Base: `https://runrun.it/api/v1.0`. Toda chamada envia `App-Key` e `User-Token`.

| Método | Caminho | Uso |
|--------|---------|-----|
| `GET` | `/tasks/{id}` | Detalhe da tarefa (título, projeto, quadro, URL) |
| `GET` | `/tasks/{id}/description` | HTML completo da descrição, inclusive `<img>` |
| `GET` | `/tasks/{id}/comments` | Comentários; o MCP guarda só os de usuários |
| `POST` | `/comments` | Cria comentário (`task_id`, `text`) |
| `GET` | `/users/me` | Usuário dono do token (id e nome) |
| `GET` | `/tasks?is_closed=true&assignee_id={id}&sort=close_date&sort_dir=desc&limit=100&page=` | Tarefas encerradas alocadas a esse usuário. O assignment entra se o id ou o nome coincidir |
| `GET` | `/documents/{id}/download` | Resposta 302 para a URL temporária da imagem embutida |

Documentação oficial: https://runrun.it/api/documentation

---

## Resumo

| Pergunta | Resposta |
|----------|----------|
| Preciso abrir o projeto do MCP no Cursor? | **Não.** Use `mcp.json` global + publish. |
| Preciso instalar o MCP dentro do outro projeto? | **Não.** Só `projectRoot` no prompt. |
| Como indico o outro projeto? | Parâmetro **`projectRoot`** com caminho absoluto. |
| Posso usar em vários projetos? | **Sim.** Mude o `projectRoot` a cada tarefa/cliente. |
| O MCP analisa só MVC 5? | **Não.** Qualquer repo com arquivos-fonte suportados. |
| Onde ficam as credenciais Runrun.it? | User Secrets ou `env` no `mcp.json` global. |
| `comentar_entregas_webiss` comenta tarefa de outra pessoa? | **Não.** Só tarefas alocadas ao usuário do token, pelo id ou pelo nome. |
| Como leio as imagens da descrição? | `obter_html_da_descricao`. A outra leitura de tarefa devolve texto sem HTML. |
