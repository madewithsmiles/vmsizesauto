using System;
using Octokit;
using Octokit.Internal;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Globalization;
using Octokit.Helpers;
using System.IO;

namespace GitHubPullRequestsPlayground
{
    public class GHService
    {
        GitHubClient _octokitClient;
        Credentials _githubCredentials;
        InMemoryCredentialStore _credentialStore;
        ProductHeaderValue _myProductInfo;
        PullRequestService _pr;
        // Use gh action: https://github.com/bdougie/fetch-upstream <-- to fetch upstream regularly so that we 
        // don't cause any issue
        // to create new branch?
        // https://github.com/marketplace/actions/create-pull-request#:~:text=Action%20inputs%20%20%20%20Name%20%20,%5Bcreate-pull-request%5D%20automated%20change%20%2016%20more%20rows%20

        public PullRequestService PullRequestService
        {
            get
            {
                if (_pr == null)
                {
                    _pr = new PullRequestService(OctoClient);
                }

                return _pr;
            }
        }

        ProductHeaderValue MyProductInfo
        {
            get
            {
                if (_myProductInfo == null)
                {
                    Type thisType = this.GetType();
                    _myProductInfo = new ProductHeaderValue(thisType.FullName, thisType.Assembly.GetName().Version.ToString());
                }
                return _myProductInfo;
            }
        }

        Credentials GitHubCredentials
        {
            get
            {
                if (_githubCredentials == null)
                {
                    _githubCredentials = new Credentials(GHAccessToken);
                }

                return _githubCredentials;
            }
        }

        InMemoryCredentialStore CredentialStore
        {
            get
            {
                if (_credentialStore == null)
                {
                    _credentialStore = new InMemoryCredentialStore(GitHubCredentials);
                }
                return _credentialStore;
            }
        }

        public GitHubClient OctoClient
        {
            get
            {
                if (_octokitClient == null)
                {
                    _octokitClient = new GitHubClient(MyProductInfo);
                    _octokitClient.Credentials = GitHubCredentials;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }

                return _octokitClient;
            }
        }

        string GHAccessToken { get; set; }

        public GHService(string ghAccessToken)
        {
            GHAccessToken = ghAccessToken;
        }

        public Repository GetRepository(string owner, string repoName)
        {
            Repository r = OctoClient.Repository.Get(owner, repoName).GetAwaiter().GetResult();
            return r;
        }
    }

    public class PullRequestService
    {
        const int DEFAULT_TIMEOUT_IN_MINUTES = 1;

        Octokit.GitHubClient client { get; set; }

        public PullRequestService(Octokit.GitHubClient ghc)
        {
            client = ghc;
        }

        public PullRequest GetPullRequest(Repository repo, long prNumber)
        {
            return client.PullRequest.Get(repo.Id, (int)prNumber).GetAwaiter().GetResult();
        }

        public PullRequest CreateNewPullRequest(string owner, string name, NewPullRequest pullRequest)
        {
            return client.PullRequest.Create(owner, name, pullRequest).GetAwaiter().GetResult();
        }
    }

    public class Program
    {
        public static readonly string GHPAT = ConfigurationManager.AppSettings["PersonalAccessToken"];
        public static readonly string GHOwner = ConfigurationManager.AppSettings["Owner"];
        public static readonly string GHRepoName = ConfigurationManager.AppSettings["RepoName"];

        static string[] FilesPathToPush =
        {
                "testfolder/testvmsizedoc.md",
                "testfolder/testsubfolder/testvmsizespec.md"
        };


        static string GetTrueRepoOwner(Repository repo)
        {
            return repo.FullName.Replace($"/{repo.Name}", "");
        }

        static NewPullRequest BuildNewPullRequestFromFork(string title, Branch sourceRepoBranch, Branch targetRepoBranch, Repository sourceRepo, Repository targetRepo)
        {
            //string trueOwner = GetTrueRepoOwner(sourceRepo);
            string trueOwner = sourceRepo.Owner.Login;
            return new NewPullRequest(title: title,
                head: string.Format(CultureInfo.InvariantCulture, "{0}:{1}", trueOwner, sourceRepoBranch.Name),
                baseRef: targetRepoBranch.Name);
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            GHService ghSvc = new GHService(ghAccessToken: GHPAT);

            Console.WriteLine("Repositories: ");
            string orgRepoOwner = "kebab-junior-test-org";
            var sourceRepo = ghSvc.GetRepository(GHOwner, GHRepoName);
            var targetRepo = ghSvc.GetRepository(orgRepoOwner, GHRepoName);
            Console.WriteLine(new { sourceRepo.FullName, sourceRepo.Name, sourceRepo.Description, sourceRepo.Private });
            Console.WriteLine(new { targetRepo.FullName, targetRepo.Name, targetRepo.Description, targetRepo.Private });


            string nBranch = $"compute-vmsizes-update-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            //ghSvc.OctoClient.Git.Tree.Get()

            // --------------- Add files, push and create PR
            CreateBranchAndPushFiles(ghSvc, repo: sourceRepo, branchName: nBranch, filesPathToPush: FilesPathToPush);
            CreatePR(ghSvc, sourceBranchName: nBranch, sourceRepoOwner: GHOwner, sourceRepo: sourceRepo,
                targetBranchName: "main", targetRepoOwner: orgRepoOwner, targetRepo: targetRepo);

            // -------------------Playing around
            //CreatePR(ghSvc, sourceBranchName: "firsttest", sourceRepoOwner: GHOwner, sourceRepo: sourceRepo,
            //    targetBranchName: "main", targetRepoOwner: orgRepoOwner, targetRepo: targetRepo);

            //Console.WriteLine($"Creating a new branch named: {nBranch}");
            //CreateBranchFromDefaultBranch(ghSvc, repo: sourceRepo, newBranch: nBranch);
            //Console.WriteLine($"Succesfully created new branch named {nBranch} based off default branch");

            //string branchToDelete = "compute-vmsizes-update-1644594562";
            //DeleteBranch(ghSvc, sourceRepo, branchToDelete);

        }

        static void CreateBranchAndPushFiles(GHService ghSvc, Repository repo, string branchName, string[] filesPathToPush)
        { /// https://github.com/octokit/octokit.net/blob/b4d7fb09784f5695fb1360ca40d88262e9bc519f/Octokit.Tests.Integration/Clients/RepositoryCommitsClientTests.cs#L445
            string repoOwner = GetTrueRepoOwner(repo);
            string repoName = repo.Name;

            Reference newBranch = CreateBranchFromDefaultBranch(ghSvc, repo, branchName);
            string newBranchReference = $"heads/{branchName}";

            // Create commit
            string commitMessage = $"compute-vmsizes-publisher: Adding {filesPathToPush.Length} files!";
            /// What if the files already exist? what do we do? Need to check that scenario 
            /// -- maybe we'll delete in one commit and re-upload in the next lol
            /// Might not need too! 
            TreeResponse treeForNewBranch = CreateTree(ghSvc, repo, filesPathToPush.ToList(), treeBranchToUpdateReference: newBranchReference);
            Commit newBranchCommit = CreateCommit(ghSvc, repo, message: commitMessage, sha: treeForNewBranch.Sha, parent: newBranch.Object.Sha);

            // Update new branch
            ReferenceUpdate newReferenceUpdate = new ReferenceUpdate(newBranchCommit.Sha);
            ghSvc.OctoClient.Git.Reference.Update(
                owner: repoOwner, name: repoName,
                reference: newBranchReference, referenceUpdate: newReferenceUpdate).GetAwaiter().GetResult();
        }

        static TreeResponse CreateTree(GHService ghSvc, Repository repo, List<string> treeContentsRelativeFilePath, 
            string treeBranchToUpdateReference=null)
        {
            string repoOwner = GetTrueRepoOwner(repo);
            string repoName = repo.Name;
            var collection = new List<NewTreeItem>();

            foreach (var filePath in treeContentsRelativeFilePath)
            {
                string content = File.ReadAllText(filePath);

                var newBlob = new NewBlob
                {
                    Content = content,
                    Encoding = Octokit.EncodingType.Utf8
                };

                var newBlobReference = ghSvc.OctoClient.Git.Blob.Create(owner: repoOwner, name: repoName, newBlob).GetAwaiter().GetResult();

                collection.Add(new NewTreeItem
                {
                    Type = TreeType.Blob,
                    Mode = Octokit.FileMode.File,
                    Path = filePath,
                    Sha = newBlobReference.Sha
                });
            }

            var newTree = new NewTree();
            foreach (var item in collection)
            {
                newTree.Tree.Add(item);
            }

            if (treeBranchToUpdateReference != null)
            {
                TreeResponse baseTree = ghSvc.OctoClient.Git.Tree.Get(
                    owner: repoOwner, name: repoName, reference: treeBranchToUpdateReference).GetAwaiter().GetResult();
                newTree.BaseTree = baseTree.Sha;
            }


            return ghSvc.OctoClient.Git.Tree.Create(owner: repoOwner, name: repoName, newTree).GetAwaiter().GetResult();
        }

        static Commit CreateCommit(GHService ghSvc, Repository repo, string message, string sha, string parent)
        {
            string repoOwner = GetTrueRepoOwner(repo);
            string repoName = repo.Name;

            var newCommit = new NewCommit(message: message, tree: sha, parent: parent);

            return ghSvc.OctoClient.Git.Commit.Create(owner: repoOwner, name: repoName, commit: newCommit).GetAwaiter().GetResult();
        }

        static Reference CreateBranchFromDefaultBranch(GHService ghSvc, Repository repo, string newBranch)
        { /// https://github.com/octokit/octokit.net/blob/main/Octokit/Helpers/ReferenceExtensions.cs
            string repoOwner = GetTrueRepoOwner(repo);
            string repoName = repo.Name;
            string defaultBranchName = repo.DefaultBranch;
            string defaultBranchNameReference = $"refs/heads/{defaultBranchName}";
            Reference defaultBranchReference = ghSvc.OctoClient.Git.Reference.Get(
                owner: repoOwner, name: repoName, reference: defaultBranchNameReference).GetAwaiter().GetResult();

            return ghSvc.OctoClient.Git.Reference.CreateBranch(
                owner: repoOwner, name: repoName, branchName: newBranch, baseReference: defaultBranchReference).GetAwaiter().GetResult();
        }

        static void DeleteBranch(GHService ghSvc, Repository repo, string branchToDelete)
        {
            string repoOwner = GetTrueRepoOwner(repo);
            string repoName = repo.Name;
            ghSvc.OctoClient.Git.Reference.Delete(
                owner: repoOwner, name: repoName, reference: $"refs/heads/{branchToDelete}").GetAwaiter().GetResult();
        }


        static void CreatePR(GHService ghSvc, string sourceBranchName, string sourceRepoOwner, Repository sourceRepo, 
            string targetBranchName, string targetRepoOwner, Repository targetRepo)
        {
            Console.WriteLine("Branches: ");
            //ghSvc.OctoClient.Git.Commit.Create
            //ghSvc.OctoClient.Repository.Content.CreateFile
            Branch sourceBranch = ghSvc.OctoClient.Repository.Branch.Get(owner: sourceRepoOwner, name: GHRepoName, branch: sourceBranchName).GetAwaiter().GetResult();
            Branch targetBranch = ghSvc.OctoClient.Repository.Branch.Get(owner: targetRepoOwner, name: GHRepoName, branch: targetBranchName).GetAwaiter().GetResult();
            Console.WriteLine(new { sourceBranch.Name, sourceBranch.Commit.Ref, sourceBranch.Commit.Url });
            Console.WriteLine(new { targetBranch.Name, targetBranch.Commit.Ref, targetBranch.Commit.Url });

            Console.WriteLine("Creating a PR: ");
            string title = $"[Compute]-[VMSizeUpdater]-Test-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            NewPullRequest npr = Program.BuildNewPullRequestFromFork(title: title, sourceRepoBranch: sourceBranch,
                targetRepoBranch: targetBranch, sourceRepo: sourceRepo, targetRepo: targetRepo);
            npr.MaintainerCanModify = true;
            Console.WriteLine(new { npr.Title, npr.Head, npr.Body, npr.MaintainerCanModify });

            PullRequest nprRes = ghSvc.PullRequestService.CreateNewPullRequest(Program.GetTrueRepoOwner(targetRepo),
                targetRepo.Name, npr);
        }
    }

    /*
     * 
     *             foreach (var file in filesPathToPush)
            {
                Console.WriteLine($"Reading: {file}");
                foreach(var lineInFile in File.ReadAllLines(file))
                {
                    Console.WriteLine(lineInFile);
                }
            }
     * 
     */


    //ghSvc.OctoClient.Git.Reference.CreateBranch()

    //ghSvc.OctoClient.Git.

    //ghSvc.OctoClient.Repository.Branch.
    //sourceBranch.
    // Playing with creation of new branches and uploading files
    //ghSvc.OctoClient.Git.Commit
    //ghSvc.OctoClient.Git.Commit.Create() # Sign commits..

    //sourceRepo.D=
    //NewCommit nc = NewCommit()
    //ghSvc.OctoClient.Git.Commit.Create()

    // latest api has merge-upstream..https://docs.github.com/en/rest/reference/branches#sync-a-fork-branch-with-the-upstream-repository


    //IRepositoryForksClient client = // sync 

    // head is source. baseRef: is destination
    //NewPullRequest npr = new NewPullRequest(title: 'Test PR', head: '', baseRef: '')

    //,
    //                                       targetBranch.Name


    //var pr = ghSvc.PullRequestService.GetPullRequest(repo, 0);
    //Console.Write(new { pr.Number, pr.Url });
}
