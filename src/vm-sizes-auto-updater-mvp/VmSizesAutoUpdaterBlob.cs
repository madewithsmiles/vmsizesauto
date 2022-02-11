using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Compute.Supportability.Tools
{
    public static class VmSizesAutoUpdaterBlob
    {
        [FunctionName("VmSizesAutoUpdaterBlob")]
        public static void Run([BlobTrigger("public-vm-sizes-release/{name}", Connection = "ngdiarravmsizehackpoc_STORAGE")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
        }
    }
}
