using SOMIOD.Helpers;
using SOMIOD.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace SOMIOD.Controllers
{
    [RoutePrefix("api/somiod/{appName}/{containerName}/subs")]
    public class SubscriptionController : ApiController
    {
        private readonly string connectionString;

        public SubscriptionController()
        {
            connectionString = SOMIOD.Properties.Settings.Default.ConnStr;
        }

        /// <summary>
        /// Creates a new subscription under a specific container
        /// </summary>
        /// <param name="appName">Parent application resource-name</param>
        /// <param name="containerName">Parent container resource-name</param>
        /// <param name="subscription">Subscription object with properties</param>
        /// <returns>Created subscription with all properties</returns>
        /// <response code="201">Subscription created successfully</response>
        /// <response code="400">Invalid input - missing required fields or invalid event type</response>
        /// <response code="404">Parent container or application not found</response>
        /// <response code="409">Subscription with this name already exists</response>
        [HttpPost]
        [Route("")]
        public IHttpActionResult PostSubscription(string appName, string containerName, [FromBody] Subscription subscription)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return BadRequest("Application name is required in URL");
            }

            if (string.IsNullOrWhiteSpace(containerName))
            {
                return BadRequest("Container name is required in URL");
            }

            if (subscription == null)
            {
                return BadRequest("Subscription object is required");
            }

            // Auto-generate resource_name if not provided
            if (string.IsNullOrWhiteSpace(subscription.resource_name))
            {
                subscription.resource_name = ValidationHelper.GenerateUniqueResourceName("sub");
            }

            if (!ValidationHelper.IsValidResourceName(subscription.resource_name))
            {
                return BadRequest("Resource name contains invalid characters. Use only letters, numbers, hyphens, and underscores.");
            }

            // Validate event type (1=creation, 2=deletion)
            if (!ValidationHelper.IsValidEventType(subscription.evt))
            {
                return BadRequest("Invalid event type. Use 1 (creation) or 2 (deletion)");
            }

            // Validate endpoint
            if (string.IsNullOrWhiteSpace(subscription.endpoint))
            {
                return BadRequest("Endpoint is required");
            }

            subscription.creation_datetime = DateTime.Now;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    // Verify parent application exists
                    int appId = DatabaseHelper.GetApplicationId(conn, appName);
                    if (appId == -1)
                    {
                        return Content(HttpStatusCode.NotFound,
                            new
                            {
                                error = $"Parent application '{appName}' not found",
                                res_type = "application"
                            });
                    }

                    // Verify parent container exists
                    int containerId = DatabaseHelper.GetContainerId(conn, containerName, appId);
                    if (containerId == -1)
                    {
                        return Content(HttpStatusCode.NotFound,
                            new
                            {
                                error = $"Parent container '{containerName}' not found under application '{appName}'",
                                res_type = "container"
                            });
                    }

                    // Check if subscription already exists
                    if (DatabaseHelper.SubscriptionExists(conn, subscription.resource_name, containerId))
                    {
                        return Content(HttpStatusCode.Conflict,
                            new
                            {
                                error = $"Subscription '{subscription.resource_name}' already exists under container '{containerName}'",
                                res_type = "subscription"
                            });
                    }

                    string insertQuery = @"
                INSERT INTO Subscriptions (Name, Event, Endpoint, ParentId, CreationDateTime) 
                VALUES (@Name, @Event, @Endpoint, @ParentId, @CreationDateTime);
                SELECT SCOPE_IDENTITY();";

                    int newId;
                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = subscription.resource_name;
                        insertCmd.Parameters.Add("@Event", SqlDbType.Int).Value = subscription.evt;
                        insertCmd.Parameters.Add("@Endpoint", SqlDbType.NVarChar, 500).Value = subscription.endpoint;
                        insertCmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = containerId;
                        insertCmd.Parameters.Add("@CreationDateTime", SqlDbType.DateTime).Value = subscription.creation_datetime;

                        newId = Convert.ToInt32(insertCmd.ExecuteScalar());
                    }

                    var response = new
                    {
                        id = newId,
                        res_type = subscription.res_type,
                        resource_name = subscription.resource_name,
                        parent = containerName,
                        evt = subscription.evt,
                        endpoint = subscription.endpoint,
                        creation_datetime = subscription.creation_datetime.ToString("yyyy-MM-ddTHH:mm:ss")
                    };

                    var locationUri = new Uri(Request.RequestUri, $"/api/somiod/{appName}/{containerName}/subs/{subscription.resource_name}");
                    return Created(locationUri, response);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in PostSubscription: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while creating the subscription."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in PostSubscription: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }

        /// <summary>
        /// Retrieves a specific subscription by resource-name
        /// </summary>
        /// <param name="appName">Parent application resource-name</param>
        /// <param name="containerName">Parent container resource-name</param>
        /// <param name="subName">Subscription resource-name</param>
        /// <returns>Subscription properties</returns>
        /// <response code="200">Subscription found and returned</response>
        /// <response code="400">Application, container, or subscription name is missing</response>
        /// <response code="404">Subscription, container, or application not found</response>
        [HttpGet]
        [Route("{subName}")]
        public IHttpActionResult GetSubscription(string appName, string containerName, string subName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return BadRequest("Application name is required in URL");
            }

            if (string.IsNullOrWhiteSpace(containerName))
            {
                return BadRequest("Container name is required in URL");
            }

            if (string.IsNullOrWhiteSpace(subName))
            {
                return BadRequest("Subscription name is required in URL");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    string query = @"
                SELECT s.Id, s.Name, s.Event, s.Endpoint, s.CreationDateTime, c.Name as ContainerName
                FROM Subscriptions s
                JOIN Containers c ON s.ParentId = c.Id
                JOIN Applications a ON c.ParentId = a.Id
                WHERE s.Name = @SubName AND c.Name = @ContainerName AND a.Name = @AppName";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@SubName", SqlDbType.NVarChar, 255).Value = subName;
                        cmd.Parameters.Add("@ContainerName", SqlDbType.NVarChar, 255).Value = containerName;
                        cmd.Parameters.Add("@AppName", SqlDbType.NVarChar, 255).Value = appName;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var subscriptionData = new
                                {
                                    id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    res_type = "subscription",
                                    resource_name = reader.GetString(reader.GetOrdinal("Name")),
                                    parent = reader.GetString(reader.GetOrdinal("ContainerName")),
                                    evt = reader.GetInt32(reader.GetOrdinal("Event")),
                                    endpoint = reader.GetString(reader.GetOrdinal("Endpoint")),
                                    creation_datetime = reader.GetDateTime(reader.GetOrdinal("CreationDateTime"))
                                        .ToString("yyyy-MM-ddTHH:mm:ss")
                                };

                                return Ok(subscriptionData);
                            }
                            else
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new
                                    {
                                        error = $"Subscription '{subName}' not found under container '{containerName}' in application '{appName}'",
                                        res_type = "subscription"
                                    });
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in GetSubscription: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while retrieving the subscription."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetSubscription: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }

        /// <summary>
        /// Deletes a subscription
        /// </summary>
        /// <param name="appName">Parent application resource-name</param>
        /// <param name="containerName">Parent container resource-name</param>
        /// <param name="subName">Subscription resource-name to delete</param>
        /// <returns>Deletion confirmation</returns>
        /// <response code="200">Subscription deleted successfully</response>
        /// <response code="400">Application, container, or subscription name is missing</response>
        /// <response code="404">Subscription, container, or application not found</response>
        [HttpDelete]
        [Route("{subName}")]
        public IHttpActionResult DeleteSubscription(string appName, string containerName, string subName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return BadRequest("Application name is required");
            }

            if (string.IsNullOrWhiteSpace(containerName))
            {
                return BadRequest("Container name is required");
            }

            if (string.IsNullOrWhiteSpace(subName))
            {
                return BadRequest("Subscription name is required");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    // Verify parent application exists
                    int appId = DatabaseHelper.GetApplicationId(conn, appName);
                    if (appId == -1)
                    {
                        return Content(HttpStatusCode.NotFound,
                            new
                            {
                                error = $"Parent application '{appName}' not found",
                                res_type = "application"
                            });
                    }

                    // Verify parent container exists
                    int containerId = DatabaseHelper.GetContainerId(conn, containerName, appId);
                    if (containerId == -1)
                    {
                        return Content(HttpStatusCode.NotFound,
                            new
                            {
                                error = $"Parent container '{containerName}' not found under application '{appName}'",
                                res_type = "container"
                            });
                    }

                    // Check if subscription exists
                    string checkQuery = @"
                SELECT s.Id, s.Name
                FROM Subscriptions s
                WHERE s.Name = @SubName AND s.ParentId = @ParentId";

                    int subId;
                    string actualName;

                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.Add("@SubName", SqlDbType.NVarChar, 255).Value = subName;
                        checkCmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = containerId;

                        using (SqlDataReader reader = checkCmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new
                                    {
                                        error = $"Subscription '{subName}' not found under container '{containerName}'",
                                        res_type = "subscription"
                                    });
                            }

                            subId = reader.GetInt32(reader.GetOrdinal("Id"));
                            actualName = reader.GetString(reader.GetOrdinal("Name"));
                        }
                    }

                    string deleteQuery = "DELETE FROM Subscriptions WHERE Id = @Id";

                    using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, conn))
                    {
                        deleteCmd.Parameters.Add("@Id", SqlDbType.Int).Value = subId;
                        int rowsAffected = deleteCmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            return InternalServerError(new Exception("Delete failed unexpectedly"));
                        }
                    }

                    var response = new
                    {
                        message = $"Subscription '{actualName}' deleted successfully",
                        deleted_resource = actualName,
                        parent = containerName,
                        res_type = "subscription"
                    };

                    return Ok(response);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in DeleteSubscription: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while deleting the subscription."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in DeleteSubscription: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }
    }
}
