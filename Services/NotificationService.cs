using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text;
using uPLibrary.Networking.M2Mqtt;
using Newtonsoft.Json;

namespace SOMIOD.Helpers
{
    /// <summary>
    /// Service for handling subscription notifications (MQTT and HTTP)
    /// </summary>
    public static class NotificationService
    {
        // MQTT client - reuse connection (singleton pattern)
        private static MqttClient mqttClient = null;
        private static readonly object mqttLock = new object();

        /// <summary>
        /// Triggers notifications for all matching subscriptions in a container
        /// </summary>
        /// <param name="containerId">Container ID where the event occurred</param>
        /// <param name="eventType">1=creation, 2=deletion</param>
        /// <param name="resourceName">Name of the resource that was created/deleted</param>
        /// <param name="containerPath">Full path to container (e.g., "api/somiod/app/container")</param>
        public static void TriggerNotifications(int containerId, int eventType, string resourceName, string containerPath)
        {
            TriggerNotifications(containerId, eventType, resourceName, containerPath, null);
        }

        /// <summary>
        /// Triggers notifications for all matching subscriptions in a container with full resource data
        /// </summary>
        /// <param name="containerId">Container ID where the event occurred</param>
        /// <param name="eventType">1=creation, 2=deletion</param>
        /// <param name="resourceName">Name of the resource that was created/deleted</param>
        /// <param name="containerPath">Full path to container (e.g., "api/somiod/app/container")</param>
        /// <param name="resourceData">Full resource data object (for deletion notifications)</param>
        public static void TriggerNotifications(int containerId, int eventType, string resourceName, string containerPath, object resourceData)
        {
            string connectionString = SOMIOD.Properties.Settings.Default.ConnStr;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    // Get all subscriptions for this container that match the event type
                    string query = @"
                        SELECT Id, Name, Evt, Endpoint 
                        FROM Subscriptions 
                        WHERE ParentId = @ContainerId 
                        AND (Evt = @EventType OR Evt = 3)"; // 3 = both events

                    List<Subscription> subscriptions = new List<Subscription>();

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@ContainerId", SqlDbType.Int).Value = containerId;
                        cmd.Parameters.Add("@EventType", SqlDbType.Int).Value = eventType;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                subscriptions.Add(new Subscription
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Evt = reader.GetInt32(2),
                                    Endpoint = reader.GetString(3)
                                });
                            }
                        }
                    }

                    // Send notifications to each matching subscription
                    foreach (var sub in subscriptions)
                    {
                        SendNotification(sub, eventType, resourceName, containerPath, resourceData);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error triggering notifications: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Sends a single notification via MQTT or HTTP
        /// </summary>
        private static void SendNotification(Subscription subscription, int eventType, string resourceName, string containerPath, object resourceData = null)
        {
            try
            {
                // Build notification payload
                object notificationData;

                if (resourceData != null)
                {
                    // Include full resource data in notification (for deletion events)
                    notificationData = new
                    {
                        subscription_name = subscription.Name,
                        event_type = eventType == 1 ? "creation" : "deletion",
                        resource_name = resourceName,
                        container_path = containerPath,
                        timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                        resource = resourceData
                    };
                }
                else
                {
                    // Basic notification without full resource data
                    notificationData = new
                    {
                        subscription_name = subscription.Name,
                        event_type = eventType == 1 ? "creation" : "deletion",
                        resource_name = resourceName,
                        container_path = containerPath,
                        timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                    };
                }

                string jsonPayload = JsonConvert.SerializeObject(notificationData, Formatting.Indented);

                // Determine if endpoint is MQTT or HTTP
                if (IsMqttEndpoint(subscription.Endpoint))
                {
                    SendMqttNotification(subscription.Endpoint, containerPath, jsonPayload);
                }
                else if (IsHttpEndpoint(subscription.Endpoint))
                {
                    SendHttpNotification(subscription.Endpoint, jsonPayload);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Unknown endpoint type: {subscription.Endpoint}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends MQTT notification
        /// Format: mqtt://broker-address:port or mqtt://broker-address
        /// </summary>
        private static void SendMqttNotification(string endpoint, string topic, string payload)
        {
            try
            {
                // Parse MQTT endpoint (e.g., "mqtt://localhost:1883" or "mqtt://test.mosquitto.org")
                Uri uri = new Uri(endpoint);
                string brokerAddress = uri.Host;
                int port = uri.Port > 0 ? uri.Port : 1883; // Default MQTT port

                // Initialize MQTT client if needed (singleton)
                lock (mqttLock)
                {
                    if (mqttClient == null || !mqttClient.IsConnected)
                    {
                        mqttClient = new MqttClient(brokerAddress, port, false, null, null, MqttSslProtocols.None);

                        string clientId = Guid.NewGuid().ToString();
                        mqttClient.Connect(clientId);
                    }
                }

                if (mqttClient.IsConnected)
                {
                    // Publish to topic matching the container path
                    byte[] message = Encoding.UTF8.GetBytes(payload);
                    mqttClient.Publish(topic, message, 0, false); // QoS 0, not retained

                    System.Diagnostics.Debug.WriteLine($"MQTT notification sent to {brokerAddress} on topic '{topic}'");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MQTT notification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends HTTP POST notification
        /// </summary>
        private static async void SendHttpNotification(string endpoint, string payload)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    var content = new StringContent(payload, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(endpoint, content);

                    if (response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"HTTP notification sent successfully to {endpoint}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"HTTP notification failed: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTTP notification error: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if endpoint is MQTT format
        /// </summary>
        private static bool IsMqttEndpoint(string endpoint)
        {
            return endpoint.StartsWith("mqtt://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if endpoint is HTTP format
        /// </summary>
        private static bool IsHttpEndpoint(string endpoint)
        {
            return endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        // Helper class for subscription data
        private class Subscription
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Evt { get; set; }
            public string Endpoint { get; set; }
        }
    }
}