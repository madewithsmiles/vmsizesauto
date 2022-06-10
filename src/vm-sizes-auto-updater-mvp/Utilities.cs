using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.Compute.Supportability.Tools
{

    /// <summary>
    /// Utility class to work with Json.
    /// </summary>
    /// FROM GARTNER AVAIL
    public static class JsonUtils
    {
        /// <summary>
        /// The default serialization settings <see cref="JsonSerializerSettings"/>.
        /// </summary>
        public static readonly JsonSerializerSettings DefaultSerializationSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        /// <summary>
        /// Converts the string to a given object of type T.
        /// </summary>
        /// <param name="strToConvert">The object of type T's string representation.</param>
        /// <param name="settings">An optional serialization setting (<see cref="JsonSerializerSettings"/>).
        /// <see cref="JsonUtils.DefaultSerializationSettings"/> is used if that value is null.</param>
        /// <typeparam name="T">Any serializable object.</typeparam>
        /// <returns>An instance of the type T from the serialized string.</returns>
        public static T To<T>(string strToConvert, JsonSerializerSettings settings = null)
        {
            ValidationUtility.EnsureIsNotNullOrWhiteSpace(strToConvert, nameof(strToConvert));
            T desrialized = JsonConvert.DeserializeObject<T>(strToConvert, settings ?? DefaultSerializationSettings);
            if (desrialized == null)
            {
                throw new JsonSerializationException($"Failed to deserialize {strToConvert}");
            }
            return desrialized;
        }
    }
}
