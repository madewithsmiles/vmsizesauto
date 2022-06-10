using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace Microsoft.Azure.Compute.Supportability.Tools.DAL
{
    /// <summary>
    /// Represents provider accessing a group status message payload.
    /// </summary>
    /// FROM GARTNER AVAIL
    public class StorageBlobDataProvider : IBlobDataProvider
    {
        private const string STORAGE_URL_FORMAT = "https://{0}.blob.core.windows.net/{1}";
        private readonly BlobServiceClient blobServiceClient;
        private readonly BlobContainerClient containerClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageBlobDataProvider"/> class.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        /// <param name="containerName">Blob container name.</param>
        /// <param name="clientOptions">Client connection option.</param>
        public StorageBlobDataProvider(string connectionString, string containerName, BlobClientOptions clientOptions = null)
        {
            ValidationUtility.EnsureIsNotNullOrWhiteSpace(connectionString, nameof(connectionString));
            ValidationUtility.EnsureIsNotNullOrWhiteSpace(containerName, nameof(containerName));
            if (clientOptions == null)
            {
                clientOptions = GetDefaultClientOption();
            }
            this.blobServiceClient = new BlobServiceClient(connectionString, clientOptions);
            this.containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            this.InstantiateContainerClient();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageBlobDataProvider"/> class.
        /// </summary>
        /// <param name="accountName">Storage account name.</param>
        /// <param name="containerName">Blob container name.</param>
        /// <param name="defaultAzureCredential">Managed Identity credential.</param>
        /// <param name="clientOptions">Client connection option.</param>
        public StorageBlobDataProvider(string accountName, string containerName, DefaultAzureCredential defaultAzureCredential, BlobClientOptions clientOptions = null)
        {
            ValidationUtility.EnsureIsNotNullOrWhiteSpace(accountName, nameof(accountName));
            ValidationUtility.EnsureIsNotNullOrWhiteSpace(containerName, nameof(containerName));
            ValidationUtility.EnsureIsNotNull(defaultAzureCredential, nameof(DefaultAzureCredential));

            //Reference: https://docs.microsoft.com/en-us/azure/storage/common/storage-auth-aad-msi?toc=/azure/storage/blobs/toc.json
            // Construct the blob container endpoint from the arguments.
            string containerEndpoint = string.Format(STORAGE_URL_FORMAT, accountName, containerName);
            if (clientOptions == null)
            {
                clientOptions = GetDefaultClientOption();
            }
            // Get a credential and create a client object for the blob container.
            this.blobServiceClient = new BlobServiceClient(new Uri(containerEndpoint),
                                                                            defaultAzureCredential, clientOptions);
            this.containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            this.InstantiateContainerClient();
        }

        /// <summary>
        /// Create container if no exists.
        /// </summary>
        private void InstantiateContainerClient()
        {
            // The call below will fail if the sample is configured to use the storage emulator in the connection string, but
            // the emulator is not running.
            // Change the retry policy for this call so that if it fails, it fails quickly.
            this.containerClient.CreateIfNotExists();
        }

        /// <summary>
        /// Default client option.
        /// </summary>
        private static BlobClientOptions GetDefaultClientOption()
        {
            BlobClientOptions clientOptions = new BlobClientOptions();
            //By default, dealy 2 seconds before new retry
            clientOptions.Retry.Delay = TimeSpan.FromSeconds(2);
            //By default, retrying 3 times
            clientOptions.Retry.MaxRetries = 3;
            clientOptions.Retry.Mode = RetryMode.Exponential;

            return clientOptions;
        }

        /// <summary>
        /// Create new blob.
        /// </summary>
        /// <param name="blobName">Blob file name.</param>
        /// <param name="content">Blob Content.</param>
        public async Task<BlobContentInfo> CreateNewBlob(string blobName, Stream content)
        {
            return await this.containerClient.UploadBlobAsync(blobName, content).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete blob using blob file name.
        /// </summary>
        /// <param name="blobName">Blob file name.</param>
        public async Task<Response<bool>> DeleteBlob(string blobName)
        {
            return await this.containerClient.DeleteBlobIfExistsAsync(blobName).ConfigureAwait(false);
        }

        /// <summary>
        /// Get all blob contents with prefix.
        /// </summary>
        public async Task<Dictionary<string, string>> GetAllBlobContentsByHierarchy(string folder)
        {
            Dictionary<string, string> contentList = new Dictionary<string, string>();
            List<BlobItem> allBlobsUnderHierarchy = await GetBlobsByHierarchicalListing(container: this.containerClient, prefix: folder, segmentSize: null).ConfigureAwait(false);

            foreach (BlobItem blob in allBlobsUnderHierarchy)
            {
                var blobItem = this.containerClient.GetBlobClient(blob.Name);
                var blobDownload = blobItem.Download();
                using (var stream = new StreamReader(blobDownload.Value.Content))
                {
                    contentList.Add(blob.Name, stream.ReadToEnd());
                }
            }
            return contentList;
        }

        private static async Task<List<BlobItem>> GetBlobsByHierarchicalListing(BlobContainerClient container,
                                                                           string prefix,
                                                                           int? segmentSize)
        {
            List<BlobItem> results = new List<BlobItem>();
            try
            {
                // Call the listing operation and return pages of the specified size.
                var resultSegment = container.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/")
                    .AsPages(default, segmentSize);

                // Enumerate the blobs returned for each page.
                await foreach (global::Azure.Page<BlobHierarchyItem> blobPage in resultSegment)
                {
                    // A hierarchical listing may return both virtual directories and blobs.
                    foreach (BlobHierarchyItem blobhierarchyItem in blobPage.Values)
                    {
                        if (blobhierarchyItem.IsPrefix)
                        {
                            // Write out the prefix of the virtual directory.
                            Console.WriteLine("Virtual directory prefix: {0}", blobhierarchyItem.Prefix);

                            // Call recursively with the prefix to traverse the virtual directory.
                            results.AddRange(await GetBlobsByHierarchicalListing(container, blobhierarchyItem.Prefix, null));
                        }
                        else
                        {
                            // Write out the name of the blob.
                            Console.WriteLine("Blob name: {0}", blobhierarchyItem.Blob.Name);
                            results.Add(blobhierarchyItem.Blob);
                        }
                    }

                }

                return results;
            }
            catch (RequestFailedException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                throw;
            }
        }

        /// <summary>
        /// Get all blob contents.
        /// </summary>
        public async Task<IEnumerable<string>> GetAllBlobContents()
        {
            List<string> contentList = new List<string>();
            await foreach (BlobItem blob in this.containerClient.GetBlobsAsync())
            {
                var blobItem = this.containerClient.GetBlobClient(blob.Name);
                var blobDownload = blobItem.Download();
                using (var stream = new StreamReader(blobDownload.Value.Content))
                {
                    contentList.Add(stream.ReadToEnd());
                }
            }
            return contentList;
        }

        /// <summary>
        /// Get blob contents by file name.
        /// </summary>
        /// <param name="fileName">Blob file name.</param>
        public async Task<string> GetBlobByFileName(string fileName)
        {
            var blobItem = this.containerClient.GetBlobClient(fileName);
            var blobDownload = await blobItem.DownloadAsync().ConfigureAwait(false);
            string content = string.Empty;
            using (var stream = new StreamReader(blobDownload.Value.Content))
            {
                content = stream.ReadToEnd();
            }
            return content;
        }

        /// <summary>
        /// Update blob.
        /// </summary>
        /// <param name="blobName">Blob file name.</param>
        /// <param name="content">Blob Content.</param>
        public async Task<BlobContentInfo> UpdateBlob(string blobName, Stream content)
        {
            var blobClient = this.containerClient.GetBlobClient(blobName);
            return await blobClient.UploadAsync(content, true).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IList<StorageBlobItem>> GetBlobItems(string prefix, int pageSize)
        {
            IList<StorageBlobItem> blobItems = new List<StorageBlobItem>();

            IAsyncEnumerable<Page<BlobItem>> resultSegment = this.containerClient.GetBlobsAsync(prefix: prefix)
                .AsPages(default, pageSize);
            if (resultSegment == null)
            {
                throw new RequestFailedException($"Blobs browsing has no result for {this.containerClient.AccountName}.");
            }

            // Enumerate the blobs returned for each page.
            await foreach (global::Azure.Page<BlobItem> blobPage in resultSegment)
            {
                foreach (BlobItem blobItem in blobPage.Values)
                {
                    blobItems.Add(new StorageBlobItem(blobItem.Name, GetBase64String(blobItem.Properties?.ContentHash), blobItem.Properties?.ContentLength));
                }
            }

            return blobItems;
        }

        /// <summary>
        /// Gets the base64 string from the content hash representing by a byte array.
        /// </summary>
        /// <param name="contentHash">The content hash.</param>
        /// <returns>The base64 string from the byte array.</returns>
        private static string GetBase64String(byte[] contentHash) => contentHash == null ? string.Empty : Convert.ToBase64String(contentHash);

        /// <summary>
        /// Acquire blob lease.
        /// </summary>
        /// <param name="blobName">blob name.</param>
        /// <param name="leaseDuration">lease duration.</param>
        /// <returns>status.</returns>
        public async Task AcquireBlobLease(string blobName, int leaseDuration)
        {
            // Create a BlobClient representing the source blob to copy.
            BlobClient sourceBlob = this.containerClient.GetBlobClient(blobName);

            // Ensure that the source blob exists.
            if (await sourceBlob.ExistsAsync().ConfigureAwait(false))
            {
                // Lease the source blob for the copy operation
                // to prevent another client from modifying it.
                BlobLeaseClient lease = sourceBlob.GetBlobLeaseClient();

                await lease.AcquireAsync(TimeSpan.FromSeconds(leaseDuration)).ConfigureAwait(false); // let it throw
                return;
            }

            throw new ApplicationException($"blob {blobName} doesn't exist. The error happened in acquiring blob lease."); // TODO: maybe use custom exception
        }

        /// <summary>
        /// Release blob lease.
        /// </summary>
        /// <param name="blobName">blob name.</param>
        /// <returns>status.</returns>
        public async Task ReleaseBlobLease(string blobName)
        {
            // Create a BlobClient representing the source blob to copy.
            BlobClient sourceBlob = this.containerClient.GetBlobClient(blobName);

            // Ensure that the source blob exists.
            if (await sourceBlob.ExistsAsync().ConfigureAwait(false))
            {
                // Lease the source blob for the copy operation
                // to prevent another client from modifying it.
                BlobLeaseClient lease = sourceBlob.GetBlobLeaseClient();

                // Update the source blob's properties.
                BlobProperties sourceProperties = await sourceBlob.GetPropertiesAsync().ConfigureAwait(false);

                if (sourceProperties.LeaseState == LeaseState.Leased)
                {
                    // Break the lease on the source blob.
                    await lease.BreakAsync().ConfigureAwait(false);
                }

                return;
            }

            throw new ApplicationException($"blob {blobName} doesn't exist. The error happened in releasing blob lease."); // TODO: maybe use custom exception
        }

        /// <summary>
        /// Get blob Sas Uri.
        /// </summary>
        /// <param name="blobName">blob name.</param>
        /// <param name="expirationInMinute">expiration duration in minute.</param>
        /// <returns>Sas Uri.</returns>
        public async Task<Uri> GetBlobSasUri(string blobName, int expirationInMinute)
        {
            // Create a BlobClient representing the source blob to copy.
            BlobClient sourceBlob = this.containerClient.GetBlobClient(blobName);

            // Ensure that the source blob exists.
            if (await sourceBlob.ExistsAsync().ConfigureAwait(false))
            {
                DateTimeOffset dt = DateTimeOffset.UtcNow;
                dt = dt.AddMinutes(expirationInMinute);
                return sourceBlob.GenerateSasUri(global::Azure.Storage.Sas.BlobSasPermissions.Read, dt);
            }

            throw new ApplicationException($"blob {blobName} doesn't exist in GetBlobSasUri."); // TODO: maybe use custom exception
        }

        /// <summary>
        /// Gets an account-level SAS Uri.
        /// </summary>
        /// <param name="targetServices">Services for which the SAS key should be generated.</param>
        /// <param name="resourceTypes">The resource types for the objects.</param>
        /// <param name="expiresOn">The expiration date of the SAS token.</param>
        /// <param name="sasProtocol">The protocol used for the access with SAS.</param>
        /// <param name="permissions">The permissions associated with the SAS token.</param>
        /// <remarks>Depending on how the client was authenticated, this may raise an exception.
        /// Won't raise an exception if authenticated with <see cref="Azure.Storage.StorageSharedKeyCredential"/>.
        /// Currently, this method was only validated with connection string authentication, which is the method
        /// currently used for the runs. It was not tested with DefaultAzureCrendentials.</remarks>
        /// <returns>A SAS uri for the account matching the options specified in <paramref name="sasBuilder"/>.</returns>
        public string GetAccountLevelSASToken(AccountSasServices targetServices, AccountSasResourceTypes resourceTypes, DateTimeOffset expiresOn, SasProtocol sasProtocol, AccountSasPermissions permissions)
        {
            // Creating a sas builder
            AccountSasBuilder sasBuilder = new AccountSasBuilder()
            {
                Services = targetServices,
                ResourceTypes = resourceTypes,
                ExpiresOn = expiresOn,
                Protocol = sasProtocol
            };
            sasBuilder.SetPermissions(permissions);

            // Generating the account level sas uri
            Uri acctSasUri = this.blobServiceClient.GenerateAccountSasUri(sasBuilder);

            return acctSasUri.Query[1..]; // Query property of Uri starts with '?' so using substring after 1st char.
        }

        /// <summary>
        /// Copy blob.
        /// </summary>
        /// <param name="blobName">blob name.</param>
        /// <param name="sourceBlobUri">source blob sas uri.</param>
        public async Task CopyBlob(string blobName, Uri sourceBlobUri)
        {
            // Get a BlobClient representing the destination blob with a unique name.
            BlobClient destBlob = this.containerClient.GetBlobClient(blobName);

            //copy the blob.
            await destBlob.StartCopyFromUriAsync(sourceBlobUri).ConfigureAwait(true);  // let it throw
        }
    }
}
