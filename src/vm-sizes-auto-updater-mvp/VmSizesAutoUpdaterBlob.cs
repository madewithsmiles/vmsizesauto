using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Compute.Supportability.Tools
{
    public static class EnvKeys
    {
        public static string PersonalAccessToken = System.Environment.GetEnvironmentVariable("PersonalAccessToken");
        public static string Owner = System.Environment.GetEnvironmentVariable("Owner");
        public static string RepoName = System.Environment.GetEnvironmentVariable("RepoName");
    }

    public static class VmSizesAutoUpdaterBlob
    {
        [FunctionName("VmSizesAutoUpdaterBlob")]
        public static void Run([BlobTrigger("public-vm-sizes-release/{name}", Connection = "ngdiarravmsizehackpoc_STORAGE")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
        }

        public static void AutoUpdate(string sourceFilePath)
        {
            GHService ghSvc = new GHService(ghAccessToken: EnvKeys.PersonalAccessToken);
        }
    }
}
