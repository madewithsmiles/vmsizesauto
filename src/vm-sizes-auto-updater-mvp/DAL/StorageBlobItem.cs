using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Compute.Supportability.Tools.DAL
{
    /// <summary>
    /// Represents a generic Azure storage blob item.
    /// </summary>
    /// FROM GARTNER AVAIL
    public class StorageBlobItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StorageBlobItem"/> class.
        /// </summary>
        /// <param name="name">The blob name.</param>
        /// <param name="contentHash">The blob content hash.</param>
        /// <param name="contentLength">The blob content length.</param>
        public StorageBlobItem(string name, string contentHash, long? contentLength)
        {
            Name = name;
            ContentHash = contentHash;
            ContentLength = contentLength ?? 0;
        }

        /// <summary>The blob name.</summary>
        public string Name { get; set; }

        /// <summary>The blob content hash.</summary>
        public string ContentHash { get; }

        /// <summary>The blob content length.</summary>
        public long ContentLength { get; }
    }
}