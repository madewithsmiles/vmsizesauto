//// Copyright (c) Microsoft Corporation. All rights reserved.
//// Licensed under the MIT License. See License.txt in the project root for license information.

//https://sourcegraph.com/github.com/Azure/azure-sdk-tools/-/blob/src/dotnet/Mgmt.CI.BuildTools/CI/CI.Common/Mgmt.CI.Common/Services/GitHubService.cs?L128

//namespace GitHubPullRequestsPlayground.Learning
//{
//    using Octokit;
//    using Octokit.Internal;
//    using System;
//    using System.Collections.Generic;
//    using System.Linq;
//    using System.Net;

//    /// <summary>
//    /// Access to Github api model
//    /// </summary>
//    public class GitHubService
//    {
//        GitHubClient _octokitClient;
//        Credentials _githubCredentials;
//        InMemoryCredentialStore _credentialStore;
//        ProductHeaderValue _myProductInfo;
//        PrSvc _pr;

//        public PrSvc PR
//        {
//            get
//            {
//                if (_pr == null)
//                {
//                    _pr = new PrSvc(OctoClient);
//                }

//                return _pr;
//            }
//        }

//        ProductHeaderValue MyProductInfo
//        {
//            get
//            {
//                if (_myProductInfo == null)
//                {
//                    Type thisType = this.GetType();
//                    _myProductInfo = new ProductHeaderValue(thisType.FullName, thisType.Assembly.GetName().Version.ToString());
//                }
//                return _myProductInfo;
//            }
//        }

//        Credentials GitHubCredentials
//        {
//            get
//            {
//                if (_githubCredentials == null)
//                {
//                    //TODO: Find the apiKey For the user that has access to both repo (public/private) in the new flow
//                    //_githubCredentials = new Credentials(KVSvc.GetSecret(CommonConstants.AzureAuth.KVInfo.Secrets.GH_AdxSdkNetAcccesToken));
//                    _githubCredentials = new Credentials(GHAccessToken);
//                    //_githubCredentials = new Credentials(GHAccessToken, AuthenticationType.Bearer);
//                }

//                return _githubCredentials;
//            }
//        }

//        InMemoryCredentialStore CredentialStore
//        {
//            get
//            {
//                if (_credentialStore == null)
//                {
//                    _credentialStore = new InMemoryCredentialStore(GitHubCredentials);
//                }
//                return _credentialStore;
//            }
//        }

//        public GitHubClient OctoClient
//        {
//            get
//            {
//                if (_octokitClient == null)
//                {
//                    _octokitClient = new GitHubClient(MyProductInfo);
//                    _octokitClient.Credentials = GitHubCredentials;
//                    //_octokitClient.Credentials = CredentialStore.GetCredentials().GetAwaiter().GetResult();
//                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
//                }

//                return _octokitClient;
//            }
//        }

//        string GHAccessToken { get; set; }

//        public GitHubService(string ghAccessToken)
//        {
//            GHAccessToken = ghAccessToken;
//        }


//        /// <summary>
//        /// Get Repository Information for any repo under Azure organization
//        /// </summary>
//        /// <param name="repoName"></param>
//        /// <returns></returns>
//        public Repository GetRepository(string repoName)
//        {
//            Repository r = OctoClient.Repository.Get("Azure", repoName).GetAwaiter().GetResult();
//            return r;
//        }

//        /// <summary>
//        /// Get list of files for a particular commit
//        /// </summary>
//        /// <param name="repoDirPath"></param>
//        /// <param name="gitHubForkName"></param>
//        /// <param name="gitHubRepoName"></param>
//        /// <param name="refCommit"></param>
//        /// <returns></returns>
//        public IEnumerable<String> GetDownloadUrlForFilesUnderDirectory(String repoDirPath, String gitHubForkName, String gitHubRepoName, String refCommit)
//        {
//            try
//            {
//                return OctoClient.Repository.Content.GetAllContentsByRef(gitHubForkName, gitHubRepoName, repoDirPath, refCommit).Result.Select(item => item.DownloadUrl);
//            }
//            catch (Exception ex)
//            {
//                throw;
//            }
//        }    
//    }

//    /// <summary>
//    /// All functionality related to Pull Requests
//    /// </summary>
//    public class PrSvc
//    {
//        const int DEFAULT_TIMEOUT_IN_MINUTES = 1;

//        Octokit.GitHubClient OC { get; set; }


//        public PrSvc(Octokit.GitHubClient ghc)
//        {
//            OC = ghc;
//        }


//        /// <summary>
//        /// Get PullRequest info
//        /// </summary>
//        /// <param name="repositoryName"></param>
//        /// <param name="prNumber"></param>
//        /// <returns></returns>
//        public PullRequest GetPullRequest(string repositoryName, long prNumber)
//        {
//            Repository r = GetRepository(repositoryName);
//            PullRequest pr = OC.PullRequest.Get(r.Id, (int)prNumber).GetAwaiter().GetResult();
//            return pr;
//        }

//        bool MergePrInRepo(String ghFork, String ghRepo, int ghPRNumber)
//        {
//            var prInfo = OC.PullRequest.Get(ghFork, ghRepo, ghPRNumber).GetAwaiter().GetResult();
//            if (prInfo.Merged)
//            {
//                Logger.LogWarning(String.Format("PR '{0}' has already been merged. Skipping merging task.", prInfo.HtmlUrl));
//                return true;
//            }
//            if (prInfo.MergeableState != MergeableState.Clean)
//            {
//                Logger.LogError(String.Format("Failed to merge PR {0}.", prInfo.HtmlUrl));
//                return false;
//            }
//            OC.PullRequest.Merge(ghFork, ghRepo, ghPRNumber, new MergePullRequest() { MergeMethod = PullRequestMergeMethod.Squash, CommitTitle = prInfo.Title }).Wait();
//            return true;
//        }

//        void ClosePrInRepo(String ghFork, String ghRepo, int ghPRNumber)
//        {
//            var prInfo = OC.PullRequest.Get(ghFork, ghRepo, ghPRNumber).GetAwaiter().GetResult();
//            if (prInfo.State == ItemState.Closed)
//            {
//                Logger.LogWarning(String.Format("PR {0} has already been closed. Skipping closing task.", prInfo.HtmlUrl));
//                return;
//            }
//            OC.PullRequest.Update(ghFork, ghRepo, ghPRNumber, new PullRequestUpdate() { State = ItemState.Closed }).Wait();
//        }

//        void PostOrUpdateCommentOnPR(String ghFork, String ghRepo, int ghPrNumber, String comment, String userName, int? commentId = null)
//        {
//            try
//            {
//                if (commentId == null)
//                {
//                    OC.Issue.Comment.Create(ghFork, ghRepo, ghPrNumber, comment).Wait(TimeSpan.FromMinutes(DEFAULT_TIMEOUT_IN_MINUTES));
//                }
//                else
//                {
//                    OC.Issue.Comment.Update(ghFork, ghRepo, (int)commentId, comment).Wait(TimeSpan.FromMinutes(DEFAULT_TIMEOUT_IN_MINUTES));
//                }

//            }
//            catch (Exception ex)
//            {
//                Logger.LogException(ex);
//                throw;
//            }
//        }

//        IEnumerable<IssueComment> GetAllCommentsByAuthor(String ghFork, String ghRepo, int ghPrNumber, String author)
//        {
//            try
//            {
//                var comments = OC.Issue.Comment.GetAllForIssue(ghFork, ghRepo, ghPrNumber).Result;
//                return comments.Where(cmt => cmt.User.Login == author);
//            }
//            catch (Exception ex)
//            {
//                Logger.LogException(ex);
//                throw;
//            }
//        }

//        void UpdatePrState(string supportedRepo, long prNumber, ItemState prCurrentState, ItemState prNewState)
//        {
//            if (prCurrentState.Equals(prNewState))
//            {
//                Logger.LogInfo("Pr            ");
//            }

//            Repository r = GetRepository(supportedRepo);
//            PullRequest pr = GetPullRequest(r.Name, prNumber);
//            Logger.LogInfo("Pr
//            if (pr.State == prCurrentState)
//            {
//                PullRequestUpdate pru = new PullRequestUpdate();
//                pru.State = prNewState;
//                pr = OC.PullRequest.Update(r.Id, (int)prNumber, pru).GetAwaiter().GetResult();

//                Logger.LogInfo("Pr            }

//            if (!pr.State.Equals(prNewState))
//                {
//                    Logger.LogError("Unable to set PR state to:'{0}'", prNewState.ToString());
//                }
//            }
//        }
//    }
//}