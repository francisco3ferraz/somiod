using System;
using System.Text;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Newtonsoft.Json;

namespace TempSensorDevice
{
    /// <summary>
    /// MQTT client for receiving notifications from SOMIOD middleware
    /// </summary>
    public class NotificationListener
    {
        private MqttClient mqttClient;
        private string brokerAddress;
        private int brokerPort;
        private bool isConnected;

        /// <summary>
        /// Event fired when a notification is received
        /// Parameters: (topic, payload)
        /// </summary>
        public event Action<string, string> OnNotificationReceived;

        public NotificationListener(string broker, int port = 1883)
        {
            brokerAddress = broker;
            brokerPort = port;
            isConnected = false;
        }

        /// <summary>
        /// Connects to MQTT broker and subscribes to topic
        /// </summary>
        /// <param name="topicFilter">MQTT topic to subscribe (e.g., "api/somiod/app/#")</param>
        public void Start(string topicFilter)
        {
            try
            {
                // Create MQTT client
                mqttClient = new MqttClient(brokerAddress, brokerPort, false, null, null, MqttSslProtocols.None);

                // Generate unique client ID
                string clientId = $"temp-sensor-{Guid.NewGuid().ToString().Substring(0, 8)}";

                // Connect to broker
                mqttClient.Connect(clientId);

                if (mqttClient.IsConnected)
                {
                    isConnected = true;

                    // Subscribe to message received event
                    mqttClient.MqttMsgPublishReceived += MqttClient_MqttMsgPublishReceived;

                    // Subscribe to topic
                    mqttClient.Subscribe(new string[] { topicFilter }, new byte[] { 0 });

                    Console.WriteLine($"  ✓ Connected to MQTT broker: {brokerAddress}:{brokerPort}");
                    Console.WriteLine($"  ✓ Subscribed to topic: {topicFilter}");
                }
                else
                {
                    throw new Exception("Failed to connect to MQTT broker");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"MQTT connection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Disconnects from MQTT broker
        /// </summary>
        public void Stop()
        {
            try
            {
                if (mqttClient != null && isConnected)
                {
                    mqttClient.Disconnect();
                    isConnected = false;
                    Console.WriteLine("  ✓ Disconnected from MQTT broker");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error during MQTT disconnect: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for incoming MQTT messages
        /// </summary>
        private void MqttClient_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            try
            {
                string topic = e.Topic;
                string payload = Encoding.UTF8.GetString(e.Message);

                // Trigger event for consumers
                OnNotificationReceived?.Invoke(topic, payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing MQTT message: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if listener is connected
        /// </summary>
        public bool IsConnected => isConnected && mqttClient != null && mqttClient.IsConnected;
    }
}
