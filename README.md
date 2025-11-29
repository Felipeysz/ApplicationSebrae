# ApplicationSebrae

Sistema de gamificação educacional desenvolvido para o SEBRAE, focado em dinâmicas de grupo para capacitação empresarial através de estudos de caso (dossiês).

## 📋 Descrição

O ApplicationSebrae é uma aplicação web ASP.NET Core que permite a realização de dinâmicas colaborativas em equipes, onde os participantes analisam casos empresariais e votam nas melhores soluções do portfólio SEBRAE.

## 🎮 Funcionalidades Principais

### Sistema de Salas e Equipes
- Criação de salas com código único de 6 dígitos
- Suporte para múltiplas equipes por sala (padrão: 3 equipes)
- Limite de 5 jogadores por equipe
- Sistema de líderes de equipe (primeiro a entrar)
- Bloqueio de entrada/troca de times após início do jogo

### Sistema de Votação
- Cada jogador seleciona exatamente 2 alternativas por rodada
- Votos são consolidados por equipe (top 2 mais votados)
- Sistema de pontuação: 5 pontos por resposta correta, +10 bônus por acertar ambas
- Tracking de usuários que não votaram

### Gerenciamento de Sessões
- Rastreamento de equipes ativas em tempo real
- Remoção automática de usuários inativos (5 minutos)
- Sistema de reconexão para usuários existentes

### Fluxo do Jogo
1. **Setup** - Criação da sala e entrada dos participantes
2. **Presentation** - Apresentação do dossiê/caso
3. **Investigation** - Fase de votação (timer de 2 minutos)
4. **Results** - Exibição dos resultados e pontuação
5. **Finished** - Fim do jogo com ranking final

### Portfólio de Produtos
- Catálogo de soluções SEBRAE (consultoria, oficinas, cursos)
- Sistema de busca e filtros
- Painel administrativo para CRUD de produtos
- Sistema de avaliações e reviews
- Analytics de visualizações

## 🏗️ Arquitetura

### Services

| Serviço | Responsabilidade |
|---------|------------------|
| `RoomManager` | Gerenciamento de salas (criar, obter, resetar, deletar) |
| `UserManagementService` | Gerenciamento de usuários (entrada, saída, atualização) |
| `VotingService` | Sistema de votação e consolidação de votos |
| `GameService` | Lógica do jogo, dossiês e cálculo de pontuação |
| `SessionManagementService` | Rastreamento de sessões ativas por equipe |

### Controllers

| Controller | Função |
|------------|--------|
| `PortfolioController` | Gerenciamento do catálogo de produtos e admin |
| `GameController` | Endpoints da dinâmica de jogo |

### Models Principais

```csharp
GameRoom          // Sala de jogo
TeamInfo          // Informações da equipe
TeamUser          // Usuário/jogador
Dossier           // Caso/estudo para análise
Product           // Produto do portfólio SEBRAE
```

## 🚀 Como Executar

### Pré-requisitos
- .NET 8.0 SDK ou superior
- Visual Studio 2022 / VS Code / Rider

### Instalação

```bash
# Clone o repositório
git clone https://github.com/seu-usuario/ApplicationSebrae.git

# Entre no diretório
cd ApplicationSebrae

# Restaure as dependências
dotnet restore

# Execute a aplicação
dotnet run
```

A aplicação estará disponível em `https://localhost:5001` ou `http://localhost:5000`.

## 📁 Estrutura do Projeto

```
ApplicationSebrae/
├── Controllers/
│   ├── PortfolioController.cs
│   └── GameController.cs
├── Services/
│   ├── RoomManager.cs
│   ├── UserManagementService.cs
│   ├── VotingService.cs
│   ├── GameService.cs
│   └── SessionManagementService.cs
├── Models/
│   ├── GameRoom.cs
│   ├── TeamInfo.cs
│   ├── TeamUser.cs
│   ├── Dossier.cs
│   └── Product.cs
├── ViewModels/
│   ├── JoinTeamViewModel.cs
│   ├── VoteSubmissionViewModel.cs
│   └── ...
├── Views/
│   └── ...
├── Data/
│   └── products.json
└── wwwroot/
    └── ...
```

## 🔐 Acesso Administrativo

O painel administrativo do portfólio está disponível em `/Portfolio/AdminLogin`.

**Credenciais padrão:**
- Senha: `sebrae2024`

> ⚠️ **Importante:** Altere a senha padrão em ambiente de produção!

## 📊 API Endpoints

### Portfólio
- `GET /Portfolio` - Página inicial do portfólio
- `GET /Portfolio/GetProducts` - Lista todos os produtos
- `GET /Portfolio/GetProductDetails?code={code}` - Detalhes de um produto
- `GET /Portfolio/GetStatistics` - Estatísticas do portfólio
- `POST /Portfolio/AddReview` - Adicionar avaliação

### Jogo (Game)
- `POST /Game/CreateRoom` - Criar nova sala
- `POST /Game/JoinTeam` - Entrar em uma equipe
- `POST /Game/SubmitVote` - Submeter voto individual
- `POST /Game/SubmitTeamAnswer` - Submeter resposta consolidada da equipe
- `GET /Game/GetRoomStatus?roomCode={code}` - Status da sala
- `GET /Game/GetDossier?roomCode={code}&round={n}` - Obter dossiê da rodada

## 🎯 Sistema de Pontuação

| Resultado | Pontos |
|-----------|--------|
| 1 resposta correta | +5 |
| 2 respostas corretas | +10 |
| Bônus (ambas corretas) | +10 |
| **Máximo por rodada** | **20** |

## 🔧 Configurações

### Variáveis de Ambiente

```env
ASPNETCORE_ENVIRONMENT=Development
LOGGING__LOGLEVEL__DEFAULT=Information
```

### Personalização de Dossiês

É possível criar dossiês customizados ao criar uma sala, passando uma lista de `Dossier` no parâmetro `customDossiers`.

## 🤝 Contribuição

1. Faça um fork do projeto
2. Crie uma branch para sua feature (`git checkout -b feature/NovaFeature`)
3. Commit suas mudanças (`git commit -m 'Adiciona NovaFeature'`)
4. Push para a branch (`git push origin feature/NovaFeature`)
5. Abra um Pull Request

## 📝 Licença

Este projeto foi desenvolvido para o SEBRAE. Todos os direitos reservados.

## 📞 Suporte

Para dúvidas ou suporte, entre em contato com a equipe de desenvolvimento.

---

**Desenvolvido com ❤️ para o SEBRAE**