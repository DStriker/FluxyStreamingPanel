using System.Text.Json;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// Puts the field names of a validation error map into the one spelling a client reads.
    /// </summary>
    /// <remarks>
    /// Two layers produce those names and neither of them produces what a client wants. Model
    /// binding reports the path of the offending value inside the JSON document, so a body
    /// property arrives as <c>$.username</c>. The service reports the name of the property in C#
    /// source, which is <c>Username</c>. Both are normalized to the camelCase name of the
    /// property, which is also how the body spells it, so one error map has one shape
    /// regardless of which layer rejected the request.
    /// </remarks>
    internal static class FieldErrorKeys
    {
        /// <summary>
        /// Name a client should read for a property the service rejected.
        /// </summary>
        /// <param name="propertyName">Name of the property in C# source.</param>
        public static string FromPropertyName(string propertyName)
            => JsonNamingPolicy.CamelCase.ConvertName(propertyName);

        /// <summary>
        /// Name a client should read for a property model binding rejected.
        /// </summary>
        /// <param name="modelStateKey">
        /// Key as it appears in <c>ModelState</c>. Depending on where the value came from and on
        /// whether the failure happened while reading it or while validating it, that is either
        /// the JSON path of the value (<c>$.username</c>) or the name of the property as declared
        /// in C# (<c>Username</c>). Both have to arrive at the same answer.
        /// </param>
        public static string FromModelStateKey(string modelStateKey)
        {
            var lastSeparator = modelStateKey.LastIndexOf('.');

            return JsonNamingPolicy.CamelCase.ConvertName(modelStateKey[(lastSeparator + 1)..]);
        }

        /// <summary>
        /// Rewrites the map a service produced so that every key is the camelCase name.
        /// </summary>
        /// <param name="errors">Rejected fields as the service reported them.</param>
        public static IReadOnlyDictionary<string, string[]>? FromPropertyNames(
            IReadOnlyDictionary<string, string[]>? errors)
        {
            if (errors is null)
            {
                return null;
            }

            return errors.ToDictionary(
                entry => FromPropertyName(entry.Key),
                entry => entry.Value);
        }
    }
}