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

        public static string GetTrueRepoOwner(Repository repo)
        {
            return repo.FullName.Replace($"/{repo.Name}", "");
        }

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
}