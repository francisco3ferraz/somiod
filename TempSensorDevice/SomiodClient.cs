using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace TempSensorDevice
{
    /// <summary>
    /// Client library for interacting with SOMIOD middleware
    /// Handles all HTTP requests to the RESTful API
    /// </summary>
    public class SomiodClient
    {
        private readonly string baseUrl;
        private readonly HttpClient httpClient;

        public SomiodClient(string somiodBaseUrl)
        {
            baseUrl = somiodBaseUrl.TrimEnd('/');
            
            // Create HttpClient with SSL certificate bypass for localhost development
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            
            httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Creates a new application resource in SOMIOD
        /// </summary>
        public dynamic CreateApplication(string resourceName)
        {
            try
            {
                var body = new { resource_name = resourceName };
                var json = JsonConvert.SerializeObject(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = httpClient.PostAsync($"{baseUrl}/api/somiod", content).Result;

                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    // Application already exists - fetch it instead
                    Console.WriteLine($"    ⚠ Application '{resourceName}' already exists, retrieving...");
                    return GetApplication(resourceName);
                }

                response.EnsureSuccessStatusCode();
                string responseJson = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<dynamic>(responseJson);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create application: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets an existing application by name
        /// </summary>
        public dynamic GetApplication(string appName)
        {
            try
            {
                var response = httpClient.GetAsync($"{baseUrl}/api/somiod/{appName}").Result;
                response.EnsureSuccessStatusCode();
                
                string json = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<dynamic>(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get application: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new container under an application
        /// </summary>
        public dynamic CreateContainer(string appName, string containerName)
        {
            try
            {
                var body = new { resource_name = containerName };
                var json = JsonConvert.SerializeObject(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = httpClient.PostAsync($"{baseUrl}/api/somiod/{appName}", content).Result;

                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    Console.WriteLine($"    ⚠ Container '{containerName}' already exists");
                    return GetContainer(appName, containerName);
                }

                response.EnsureSuccessStatusCode();
                string responseJson = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<dynamic>(responseJson);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create container: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets an existing container
        /// </summary>
        public dynamic GetContainer(string appName, string containerName)
        {
            try
            {
                var response = httpClient.GetAsync($"{baseUrl}/api/somiod/{appName}/{containerName}").Result;
                response.EnsureSuccessStatusCode();
                
                string json = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<dynamic>(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get container: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a subscription on a container
        /// </summary>
        public dynamic CreateSubscription(string appName, string containerName, string subName, int evt, string endpoint)
        {
            try
            {
                var body = new
                {
                    resource_name = subName,
                    evt = evt,
                    endpoint = endpoint
                };

                var json = JsonConvert.SerializeObject(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = httpClient.PostAsync(
                    $"{baseUrl}/api/somiod/{appName}/{containerName}/subs",
                    content
                ).Result;

                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    Console.WriteLine($"    ⚠ Subscription '{subName}' already exists");
                    return GetSubscription(appName, containerName, subName);
                }

                response.EnsureSuccessStatusCode();
                string responseJson = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<dynamic>(responseJson);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create subscription: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets an existing subscription
        /// </summary>
        public dynamic GetSubscription(string appName, string containerName, string subName)
        {
            try
            {
                var response = httpClient.GetAsync(
                    $"{baseUrl}/api/somiod/{appName}/{containerName}/subs/{subName}"
                ).Result;
                
                response.EnsureSuccessStatusCode();
                string json = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<dynamic>(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get subscription: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a content-instance in a container
        /// </summary>
        public dynamic CreateContentInstance(string appName, string containerName, string contentName, string contentType, string contentData)
        {
            try
            {
                var body = new
                {
                    resource_name = contentName,
                    content_type = contentType,
                    content = contentData
                };

                var json = JsonConvert.SerializeObject(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = httpClient.PostAsync(
                    $"{baseUrl}/api/somiod/{appName}/{containerName}",
                    content
                ).Result;

                response.EnsureSuccessStatusCode();
                string responseJson = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<dynamic>(responseJson);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create content-instance: {ex.Message}");
            }
        }

        /// <summary>
        /// Discovers all applications
        /// </summary>
        public List<string> DiscoverApplications()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/somiod");
                request.Headers.Add("somiod-discovery", "application");

                var response = httpClient.SendAsync(request).Result;
                response.EnsureSuccessStatusCode();

                string json = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<List<string>>(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to discover applications: {ex.Message}");
            }
        }

        /// <summary>
        /// Discovers content-instances in a container
        /// </summary>
        public List<string> DiscoverContentInstances(string appName, string containerName)
        {
            try
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{baseUrl}/api/somiod/{appName}/{containerName}"
                );
                request.Headers.Add("somiod-discovery", "content-instance");

                var response = httpClient.SendAsync(request).Result;
                response.EnsureSuccessStatusCode();

                string json = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<List<string>>(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to discover content-instances: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes an application and all its children
        /// </summary>
        public void DeleteApplication(string appName)
        {
            try
            {
                var response = httpClient.DeleteAsync($"{baseUrl}/api/somiod/{appName}").Result;
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete application: {ex.Message}");
            }
        }
    }
}
