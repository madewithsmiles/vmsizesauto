using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Microsoft.Azure.Compute.Supportability.Tools.DAL
{
    /// <summary>
    /// Represent interface to blob accessing provider.
    /// </summary>
    public interface IBlobDataProvider
    {
        /// <summary>
        /// Create new blob.
        /// </summary>
        /// <param name="blobName">Blob file name.</param>
        /// <param name="content">Blob Content.</param>
        Task<BlobContentInfo> CreateNewBlob(string blobName, Stream content);

        /// <summary>
        /// Update blob.
        /// </summary>
        /// <param name="blobName">Blob file name.</param>
        /// <param name="content">Blob Content.</param>
        Task<BlobContentInfo> UpdateBlob(string blobName, Stream content);

        /// <summary>
        /// Get all blob contents.
        /// </summary>
        Task<IEnumerable<string>> GetAllBlobContents();

        /// <summary>
        /// Get blob contents by file name.
        /// </summary>
        /// <param name="fileName">Blob file name.</param>
        Task<string> GetBlobByFileName(string fileName);

        /// <summary>
        /// Delete blob using blob file name.
        /// </summary>
        /// <param name="blobName">Blob file name.</param>
        Task<Response<bool>> DeleteBlob(string blobName);

        /// <summary>
        /// Get all blob items under the prefix.
        /// </summary>
        /// <param name="prefix">blob prefix.</param>
        /// <param name="pageSize">Page size.</param>
        /// <returns>a collection of blob items.</returns>
        Task<IList<StorageBlobItem>> GetBlobItems(string prefix, int pageSize);

        /// <summary>
        /// Acquire blob lease.
        /// </summary>
        /// <param name="blobName">blob name.</param>
        /// <param name="leaseDuration">lease duration.</param>
        Task AcquireBlobLease(string blobName, int leaseDuration);

        /// <summary>
        /// Release blob lease.
        /// </summary>
        /// <param name="blobName">blob name.</param>
        Task ReleaseBlobLease(string blobName);

        /// <summary>
        /// Get blob Sas Uri.
        /// </summary>
        /// <param name="blobName">blob name.</param>
        /// <param name="expirationInMinute">expiration duration in minute.</param>
        /// <returns>Sas Uri.</returns>
        Task<Uri> GetBlobSasUri(string blobName, int expirationInMinute);

        /// <summary>
        /// Gets an account-level SAS Uri.
        /// </summary>
        /// <param name="targetServices">Services for which the SAS key should be generated.</param>
        /// <param name="resourceTypes">The resource types for the objects.</param>
        /// <param name="expiresOn">The expiration date of the SAS token.</param>
        /// <param name="sasProtocol">The protocol used for the access with SAS.</param>
        /// <param name="permissions">The permissions associated with the SAS token.</param>
        /// <returns>A SAS uri for the account matching the options specified in <paramref name="sasBuilder"/>.</returns>
        string GetAccountLevelSASToken(AccountSasServices targetServices, AccountSasResourceTypes resourceTypes, DateTimeOffset expiresOn, SasProtocol sasProtocol, AccountSasPermissions permissions);

        /// <summary>
        /// Copy blob.
        /// </summary>
        /// <param name="blobName">blob name.</param>
        /// <param name="sourceBlobUri">source blob sas uri.</param>
        Task CopyBlob(string blobName, Uri sourceBlobUri);
    }
}
