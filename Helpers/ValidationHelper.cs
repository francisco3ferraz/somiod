using System;

namespace SOMIOD.Helpers
{
    /// <summary>
    /// Validation helper class for SOMIOD API
    /// Contains validation and utility methods used across controllers
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// Generates a unique resource name with a prefix
        /// From Worksheet requirements: auto-generate unique names when not provided
        /// </summary>
        /// <param name="prefix">Prefix for the resource name (e.g., "app", "container", "data", "sub")</param>
        /// <returns>Unique resource name</returns>
        public static string GenerateUniqueResourceName(string prefix)
        {
            // Generate unique name using timestamp + short GUID
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string shortGuid = Guid.NewGuid().ToString("N").Substring(0, 6);
            return $"{prefix}_{timestamp}_{shortGuid}";
        }

        /// <summary>
        /// Validates resource name format
        /// Ensures names are URL-safe and don't contain problematic characters
        /// </summary>
        /// <param name="name">Resource name to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidResourceName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Allow only alphanumeric, hyphens, and underscores (URL-safe)
            // No spaces, slashes, or special characters that break URLs
            return System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z0-9_-]+$");
        }

        /// <summary>
        /// Validates endpoint URL format
        /// Used for subscription endpoints
        /// </summary>
        /// <param name="endpoint">Endpoint URL to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return false;

            // Check if it's a valid URI
            return Uri.TryCreate(endpoint, UriKind.Absolute, out Uri uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Validates content type format
        /// Used for content instances
        /// </summary>
        /// <param name="contentType">Content type to validate (e.g., "text/plain", "application/json")</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidContentType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return false;

            // Basic MIME type validation (type/subtype)
            return System.Text.RegularExpressions.Regex.IsMatch(contentType, @"^[a-zA-Z0-9]+/[a-zA-Z0-9\-\+\.]+$");
        }

        /// <summary>
        /// Validates event type for subscriptions
        /// </summary>
        /// <param name="evt">Event type value (1 = creation, 2 = deletion, or both)</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidEventType(int evt)
        {
            // According to SOMIOD spec: 1 = creation, 2 = deletion, 3 = both
            return evt >= 1 && evt <= 3;
        }

        /// <summary>
        /// Gets event type description
        /// </summary>
        /// <param name="evt">Event type value</param>
        /// <returns>Human-readable event description</returns>
        public static string GetEventTypeDescription(int evt)
        {
            switch (evt)
            {
                case 1:
                    return "creation";
                case 2:
                    return "deletion";
                case 3:
                    return "both";
                default:
                    return "unknown";
            }
        }
    }
}
