using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Newtonsoft.Json.Linq;

namespace TempSensorDevice
{
    /// <summary>
    /// Serializes MQTT notifications to XML and validates against XSD schema
    /// Requirement: "all incoming notifications for each application are serialized into XML files, validated against a predefined XML schema"
    /// </summary>
    public static class XmlNotificationSerializer
    {
        private static readonly string XSD_PATH = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Schemas", 
            "notification.xsd"
        );

        private static readonly string OUTPUT_FOLDER = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Notifications"
        );

        /// <summary>
        /// Serializes JSON notification payload to XML and validates against XSD
        /// </summary>
        /// <param name="jsonPayload">JSON notification from SOMIOD</param>
        /// <param name="appName">Application name for folder organization</param>
        public static void SerializeAndValidate(string jsonPayload, string appName)
        {
            try
            {
                // 1. Parse JSON payload
                JObject jsonObj = JObject.Parse(jsonPayload);

                // 2. Convert JSON to XML structure
                XElement xmlDoc = new XElement("notification",
                    new XElement("subscription_name", GetJsonValue(jsonObj, "subscription_name")),
                    new XElement("event_type", GetJsonValue(jsonObj, "event_type")),
                    new XElement("resource_name", GetJsonValue(jsonObj, "resource_name")),
                    new XElement("container_path", GetJsonValue(jsonObj, "container_path")),
                    new XElement("timestamp", GetJsonValue(jsonObj, "timestamp"))
                );

                // 3. Create output directory structure
                string appFolder = Path.Combine(OUTPUT_FOLDER, appName);
                Directory.CreateDirectory(appFolder);

                // 4. Generate unique filename
                string filename = $"notification_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.xml";
                string filepath = Path.Combine(appFolder, filename);

                // 5. Save XML to file
                xmlDoc.Save(filepath);
                Console.WriteLine($"  📄 XML saved: {filepath}");

                // 6. Validate against XSD schema
                bool isValid = ValidateXmlAgainstXsd(filepath);
                Console.WriteLine($"  {(isValid ? "✓" : "✗")} XSD Validation: {(isValid ? "PASSED" : "FAILED")}");

                if (!isValid)
                {
                    throw new Exception("XML validation failed against schema");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"XML serialization error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Safely extracts value from JSON object
        /// </summary>
        private static string GetJsonValue(JObject json, string key)
        {
            return json[key]?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Validates XML file against XSD schema
        /// </summary>
        private static bool ValidateXmlAgainstXsd(string xmlPath)
        {
            if (!File.Exists(XSD_PATH))
            {
                Console.WriteLine($"  ⚠ XSD schema not found: {XSD_PATH}");
                Console.WriteLine($"  ⚠ Skipping validation (schema missing)");
                return false;
            }

            try
            {
                // Load XSD schema
                XmlSchemaSet schemas = new XmlSchemaSet();
                schemas.Add("", XSD_PATH);

                // Load XML document
                XDocument doc = XDocument.Load(xmlPath);

                bool isValid = true;
                string validationErrors = "";

                // Validate XML against schema
                doc.Validate(schemas, (sender, e) =>
                {
                    if (e.Severity == XmlSeverityType.Error)
                    {
                        isValid = false;
                        validationErrors += $"\n    - {e.Message}";
                    }
                });

                if (!isValid)
                {
                    Console.WriteLine($"  ✗ Validation errors:{validationErrors}");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Validation exception: {ex.Message}");
                return false;
            }
        }
    }
}
