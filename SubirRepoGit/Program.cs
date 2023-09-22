using System;
using System.IO;
using System.Threading.Tasks;
using LibGit2Sharp;
using Octokit;
using OctokitRepository = Octokit.Repository;

class Program
{
    static async Task Main(string[] args)
    {
        string username = "Fulano"; // user.name git
        string email = "teste@email"; // user.mail git
        string dir = @""; // diretorios das pastas
        string githubToken = ""; // token de acesso do perfil git

        var github = new GitHubClient(new ProductHeaderValue("SubirRepoGit"));
        github.Credentials = new Octokit.Credentials(githubToken);

        foreach (var subDir in Directory.GetDirectories(dir))
        {
            try
            {
                Console.WriteLine($"Processando {subDir}");

                LibGit2Sharp.Repository.Init(subDir);

                using (var repo = new LibGit2Sharp.Repository(subDir))
                {
                    repo.Config.Set("user.name", username);
                    repo.Config.Set("user.email", email);

                    Commands.Stage(repo, "*");

                    // Verifica se há arquivos para fazer commit
                    if (repo.RetrieveStatus().IsDirty)
                    {
                        var author = new LibGit2Sharp.Signature(username, email, DateTime.Now);
                        var committer = author;

                        repo.Commit("Commit inicial", author, committer);
                    }

                    // Cria um novo repositório no GitHub
                    var newRepo = new NewRepository(Path.GetFileName(subDir))
                    {
                        AutoInit = true,
                        Private = true,
                        GitignoreTemplate = "VisualStudio"
                    };

                    var createdRepo = await CreateRepoAsync(github, newRepo, githubToken);

                    if (createdRepo == null)
                    {
                        Console.WriteLine($"O repositório {newRepo.Name} já existe, pulando para o próximo diretório.");
                        continue;
                    }

                    // Adiciona o repositório remoto ao repositório local
                    Remote remote;
                    if (repo.Network.Remotes["origin"] == null)
                    {
                        remote = repo.Network.Remotes.Add("origin", createdRepo.CloneUrl);
                    }
                    else
                    {
                        remote = repo.Network.Remotes["origin"];
                        repo.Network.Remotes.Update(remote.Name, r => r.Url = createdRepo.CloneUrl);
                    }

                    // Configura a branch
                    var branch = repo.Branches["master"];
                    repo.Branches.Update(branch, b => b.Remote = remote.Name, b => b.UpstreamBranch = branch.CanonicalName);

                    // Faz push para o repositório remoto
                    var options = new PushOptions();
                    options.CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = githubToken, Password = string.Empty };
                    repo.Network.Push(repo.Branches["master"], options);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar {subDir}: {ex.Message}");
                continue;
            }
        }
    }

    static async Task<OctokitRepository> CreateRepoAsync(GitHubClient github, NewRepository newRepo, string githubToken)
    {
        try
        {
            // Tenta criar o repositório
            return await github.Repository.Create(newRepo);
        }
        catch (Octokit.RepositoryExistsException)
        {
            // Se o repositório já existir, retorna nulo
            return null;
        }
    }
}
