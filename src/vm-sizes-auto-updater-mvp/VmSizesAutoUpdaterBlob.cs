using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Grynwald.MarkdownGenerator;
using Microsoft.Azure.Compute.Supportability.Tools.DAL;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.Compute.Supportability.Tools
{
    public static class EnvKeys
    {
        public static string StorageAccountName = System.Environment.GetEnvironmentVariable("AccountName");
        //public static string ReleaseInfoContainerName = System.Environment.GetEnvironmentVariable("ReleaseInfoContainerName");
        public static string ReleaseDetailsContainerName = System.Environment.GetEnvironmentVariable("ReleaseDetailsContainerName");
        
        public static string SourceRepoOwner = System.Environment.GetEnvironmentVariable("SourceRepoOwner");
        public static string SourceRepoName = System.Environment.GetEnvironmentVariable("SourceRepoName");
        public static string TargetRepoOwner = System.Environment.GetEnvironmentVariable("TargetRepoOwner");
        public static string TargetRepoName = System.Environment.GetEnvironmentVariable("TargetRepoName");
        public static string VmSizesAutoUpdatesRootFolder = System.Environment.GetEnvironmentVariable("VMSizesAutoUpdatesRootFolder"); // "articles/virtual-machines/includes/auto-updated-vm-sizes/"

        // Sensitive
        public static string AccountConnection = System.Environment.GetEnvironmentVariable("AccountConnection");
        public static string AccessToken = System.Environment.GetEnvironmentVariable("AccessToken");

        public static string GetSafeForLogEnvKeysString()
        {
            return (new { StorageAccountName, ReleaseDetailsContainerName, SourceRepoOwner, SourceRepoName, TargetRepoOwner, TargetRepoName, VmSizesAutoUpdatesRootFolder }).ToString();
        }
    }

    public class ReleaseInfo
    {
        /// <summary>
        /// The resource's tenant id.
        /// </summary>
        [JsonProperty(PropertyName = "releaseDate")]
        public string ReleaseDate { get; set; }

        /// <summary>
        /// The resource's subscription id.
        /// </summary>
        [JsonProperty(PropertyName = "releaseMessage")]
        public string ReleaseMessage { get; set; }

        /// <summary>
        /// The resource's location.
        /// </summary>
        [JsonProperty(PropertyName = "releaseDetailsPath")]
        public string ReleaseDetailsPath { get; set; }

        public override string ToString()
        {
            return (new { ReleaseDate, ReleaseMessage, ReleaseDetailsPath }).ToString();
        }
    }

    public class VmSizesAutoUpdaterBlob
    {
        [FunctionName("VmSizesAutoUpdaterBlob")]
        public async Task RunAsync([BlobTrigger("public-vm-sizes-release/{name}", Connection = "ngdiarravmsizehackpoc")]Stream myBlob, string name, ILogger log)
        {
            try
            {
                GitHubUtils.Logger = log;
                log.LogInformation($"C# Function processing blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
                log.LogInformation($"Configuration: {EnvKeys.GetSafeForLogEnvKeysString()}");

                log.LogInformation($"Reading release information");
                ReleaseInfo vmReleaseInfo = JsonUtils.To<ReleaseInfo>(new StreamReader(myBlob).ReadToEnd());
                log.LogInformation($"Successfully read release information: {vmReleaseInfo.ToString()}");


                log.LogInformation("Creating new Storage Blob Provider");
                StorageBlobDataProvider releaseDetailsBlobProvider = new StorageBlobDataProvider(connectionString: EnvKeys.AccountConnection, containerName: EnvKeys.ReleaseDetailsContainerName);
                log.LogInformation("Successfully created a new storage blob provider");
                   
                List<string> downloadedFiles = await GetReleaseDetailsJSONInfoAsync(releaseDetailsBlobProvider, releaseDetailsFolder: vmReleaseInfo.ReleaseDetailsPath, localDestinationFolder: GetTempRandomFolder());
                log.LogInformation($"Successfully downloaded {downloadedFiles.Count} files");

                Dictionary<string, string> filesPathToPush = new Dictionary<string, string>();

                string processedFileFolder = GetTempRandomFolder();
                log.LogInformation($"Processing files");
                foreach (string item in downloadedFiles)
                {
                    var processedVmSizeData = VMSizeProcessor.ProcessVMSizeDataDTOs(VMSizeProcessor.ReadVMSizeJSON(item));
                    MdDocument processedVmSizeDoc = VMSizesJsonMarkdownWriter.GetNewIncludeDoc(processedVmSizeData, new BasicVMSizesTableMarkdownSpec());
                    log.LogInformation($"Processed file {item}");
                    log.LogInformation(processedVmSizeDoc.ToString());

                    string fileName = $"{item.Split("/").Last().Replace(".json", "")}.md";
                    string outputPath = Path.Combine(processedFileFolder, fileName);
                    filesPathToPush.Add(outputPath, fileName);
                    log.LogInformation($"Saving output at: {outputPath}");
                    processedVmSizeDoc.Save(outputPath);
                    log.LogInformation($"Successfully saved file content to {outputPath}");
                }
                log.LogInformation($"Finished processing files. Now creating a new branch and uploading the markdown to GitHub");

                log.LogInformation("Creating new GitHub Client");
                GHService ghSvc = new GHService(ghAccessToken: EnvKeys.AccessToken);

                log.LogInformation($"Getting source repository info: {EnvKeys.SourceRepoOwner}/{EnvKeys.SourceRepoName}");
                var sourceRepo = ghSvc.GetRepository(EnvKeys.SourceRepoOwner, EnvKeys.SourceRepoName);
                log.LogInformation("Successfully retrieved source repo information.");
                log.LogInformation((new { sourceRepo.FullName, sourceRepo.Name, sourceRepo.Description, sourceRepo.Private }).ToString());
                
                string nBranch = $"ngdiarra/compute-demo-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                string branchToCopy = "";// will use master "ngdiarra/vm_sizes_auto_updating_includes";
                log.LogInformation($"Creating new branch: {nBranch} from {branchToCopy} and sending markdown files");

                GitHubUtils.CreateBranchAndPushFiles(ghSvc, repo: sourceRepo, branchName: nBranch, filesPathToPush: filesPathToPush, branchToCopy: branchToCopy);

                string title = $"[Compute]-[VMSizeUpdater]-Test-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                log.LogInformation($"Test creating a new pull request. Title: {title}");
                GitHubUtils.CreatePR(ghSvc,
                    sourceBranchName: nBranch,
                    sourceRepoOwner: EnvKeys.SourceRepoOwner,
                    sourceRepo: sourceRepo,
                    targetBranchName: sourceRepo.DefaultBranch,
                    targetRepoOwner: GHService.GetTrueRepoOwner(sourceRepo),
                    targetRepo: sourceRepo,
                    title: title,
                    prMessage: vmReleaseInfo.ReleaseMessage);
                log.LogInformation($"Successfully created a new PR");

                await Task.Delay(5 * 60 * 1000);
                Directory.Delete(processedFileFolder, true);
            }
            catch (Exception ex)
            {
                log.LogError($"There was an error processing release info for: {name}. Exception: {ex}");
            }
        }

        public static string GetTempRandomFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", ""));
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static async Task<List<string>> GetReleaseDetailsJSONInfoAsync(StorageBlobDataProvider storageBlobDataProvider, string releaseDetailsFolder, string localDestinationFolder)
        {
            List<string> downloadedFiles = new List<string>();

            var filesNameToContent = await storageBlobDataProvider.GetAllBlobContentsByHierarchy(folder: releaseDetailsFolder);
            foreach (var fileNameToContent in filesNameToContent)
            {
                string localFileName = Path.Combine(localDestinationFolder, fileNameToContent.Key.Split("/").Last());
                File.WriteAllText(localFileName, fileNameToContent.Value);
                downloadedFiles.Add(localFileName);
            }

            return downloadedFiles;
        }

        public static void AutoUpdate(string sourceFilePath)
        {
            GHService ghSvc = new GHService(ghAccessToken: EnvKeys.AccessToken);
        }
    }
}
