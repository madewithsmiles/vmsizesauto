using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Internal;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Globalization;
using Octokit.Helpers;

namespace Microsoft.Azure.Compute.Supportability.Tools
{
    public static class GitHubUtils
    {
        public static ILogger Logger;

        public static NewPullRequest BuildNewPullRequestFromFork(string title, Branch sourceRepoBranch, Branch targetRepoBranch, Repository sourceRepo, Repository targetRepo)
        {
            string trueOwner = GHService.GetTrueRepoOwner(sourceRepo);
            return new NewPullRequest(title: title,
                head: string.Format(CultureInfo.InvariantCulture, "{0}:{1}", trueOwner, sourceRepoBranch.Name),
                baseRef: targetRepoBranch.Name);
        }

        
        public static void CreateBranchAndPushFiles(GHService ghSvc, Repository repo, string branchName, Dictionary<string,string> filesPathToPush, string branchToCopy = "")
        { /// https://github.com/octokit/octokit.net/blob/b4d7fb09784f5695fb1360ca40d88262e9bc519f/Octokit.Tests.Integration/Clients/RepositoryCommitsClientTests.cs#L445
            string repoOwner = GHService.GetTrueRepoOwner(repo);
            string repoName = repo.Name;

            Reference newBranch = CreateNewCopyOfBranch(ghSvc, repo, branchName, branchToCopy);
            string newBranchReference = $"heads/{branchName}";

            // Create commit
            string commitMessage = $"compute-vmsizes-publisher: Adding/Updating {filesPathToPush.Count} files!";
            TreeResponse treeForNewBranch = CreateTree(ghSvc, repo, filesPathToPush, treeBranchToUpdateReference: newBranchReference);
            Commit newBranchCommit = CreateCommit(ghSvc, repo, message: commitMessage, sha: treeForNewBranch.Sha, parent: newBranch.Object.Sha);

            // Update new branch
            ReferenceUpdate newReferenceUpdate = new ReferenceUpdate(newBranchCommit.Sha);
            ghSvc.OctoClient.Git.Reference.Update(
                owner: repoOwner, name: repoName,
                reference: newBranchReference, referenceUpdate: newReferenceUpdate).GetAwaiter().GetResult();
        }

        public static TreeResponse CreateTree(GHService ghSvc, Repository repo, Dictionary<string, string> treeContentsRelativeFilePathToRemotePath, 
            string treeBranchToUpdateReference=null)
        {
            string repoOwner = GHService.GetTrueRepoOwner(repo);
            string repoName = repo.Name;
            var collection = new List<NewTreeItem>();

            foreach (var filePath in treeContentsRelativeFilePathToRemotePath)
            {
                string content = File.ReadAllText(filePath.Key);

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
                    Path = filePath.Value,
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

        public static Commit CreateCommit(GHService ghSvc, Repository repo, string message, string sha, string parent)
        {
            string repoOwner = GHService.GetTrueRepoOwner(repo);
            string repoName = repo.Name;

            var newCommit = new NewCommit(message: message, tree: sha, parent: parent);

            return ghSvc.OctoClient.Git.Commit.Create(owner: repoOwner, name: repoName, commit: newCommit).GetAwaiter().GetResult();
        }

        public static Reference CreateNewCopyOfBranch(GHService ghSvc, Repository repo, string newBranch, string branchToCopy = "")
        { /// https://github.com/octokit/octokit.net/blob/main/Octokit/Helpers/ReferenceExtensions.cs
            string repoOwner = GHService.GetTrueRepoOwner(repo);
            string repoName = repo.Name;
            string defaultBranchName = branchToCopy;
            if (string.IsNullOrWhiteSpace(branchToCopy)) // Use default if branch to copy is empty or whitespace
            {
                defaultBranchName = repo.DefaultBranch;
            }

            string defaultBranchNameReference = $"refs/heads/{defaultBranchName}";
            Reference defaultBranchReference = ghSvc.OctoClient.Git.Reference.Get(
                owner: repoOwner, name: repoName, reference: defaultBranchNameReference).GetAwaiter().GetResult();

            return ghSvc.OctoClient.Git.Reference.CreateBranch(
                owner: repoOwner, name: repoName, branchName: newBranch, baseReference: defaultBranchReference).GetAwaiter().GetResult();
        }

        public static void DeleteBranch(GHService ghSvc, Repository repo, string branchToDelete)
        {
            string repoOwner = GHService.GetTrueRepoOwner(repo);
            string repoName = repo.Name;
            ghSvc.OctoClient.Git.Reference.Delete(
                owner: repoOwner, name: repoName, reference: $"refs/heads/{branchToDelete}").GetAwaiter().GetResult();
        }


        public static void CreatePR(GHService ghSvc, string sourceBranchName, string sourceRepoOwner, Repository sourceRepo, 
            string targetBranchName, string targetRepoOwner, Repository targetRepo, string title = "", string prMessage = "", bool isDraft = false)
        {
            //Logger.LogInformation("Branches: ");
            Branch sourceBranch = ghSvc.OctoClient.Repository.Branch.Get(owner: sourceRepoOwner, name: sourceRepo.Name, branch: sourceBranchName).GetAwaiter().GetResult();
            Branch targetBranch = ghSvc.OctoClient.Repository.Branch.Get(owner: targetRepoOwner, name: targetRepo.Name, branch: targetBranchName).GetAwaiter().GetResult();
            Logger.LogInformation(new { sourceBranch.Name, sourceBranch.Commit.Ref, sourceBranch.Commit.Url }.ToString());
            Logger.LogInformation(new { targetBranch.Name, targetBranch.Commit.Ref, targetBranch.Commit.Url }.ToString());

            Logger.LogInformation("Creating a PR: ");
            NewPullRequest npr = BuildNewPullRequestFromFork(title: title, sourceRepoBranch: sourceBranch,
                targetRepoBranch: targetBranch, sourceRepo: sourceRepo, targetRepo: targetRepo);
            npr.MaintainerCanModify = true; 
            npr.Body = prMessage; // Add description
            npr.Draft = isDraft;
            Logger.LogInformation(new { npr.Title, npr.Head, npr.Body, npr.MaintainerCanModify }.ToString());

            PullRequest nprRes = ghSvc.PullRequestService.CreateNewPullRequest(targetRepoOwner,
                targetRepo.Name, npr);
        }
    }
}