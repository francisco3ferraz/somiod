using System;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Web.Http;
using SOMIOD.Helpers;
using SOMIOD.Models;

namespace SOMIOD.Controllers
{
    [RoutePrefix("api/somiod/{appName}/{containerName}")]
    public class ContentInstanceController : ApiController
    {
        private readonly string connectionString;

        public ContentInstanceController()
        {
            connectionString = SOMIOD.Properties.Settings.Default.ConnStr;
        }

        /// <summary>
        /// Retrieves a specific content-instance by resource_name
        /// </summary>
        /// <param name="appName">Parent application resource_name</param>
        /// <param name="containerName">Parent container resource_name</param>
        /// <param name="contentName">Content-instance resource_name</param>
        /// <returns>Content-instance properties including content data</returns>
        /// <response code="200">Content-instance found and returned</response>
        /// <response code="400">Application, container, or content-instance name is missing</response>
        /// <response code="404">Content-instance, container, or application not found</response>
        /// <remarks>
        /// Retrieves a content-instance resource by its unique resource_name within a container.
        /// 
        /// **cURL Command:**
        /// 
        ///     curl -X GET "https://localhost:44346/api/somiod/smart-home/living-room/temperature-1" -k
        ///     
        /// **Response (200 OK):**
        /// 
        ///     {
        ///        "id": 1,
        ///        "res_type": "content-instance",
        ///        "resource_name": "temperature-1",
        ///        "parent": "living-room",
        ///        "content_type": "application/json",
        ///        "content": "{\"value\": 23.5, \"unit\": \"celsius\"}",
        ///        "creation_datetime": "2025-01-15T10:40:00"
        ///     }
        ///     
        /// **Error Response (404 Not Found):**
        /// 
        ///     {
        ///        "error": "Content-instance 'temperature-1' not found under container 'living-room' in application 'smart-home'",
        ///        "res_type": "content-instance"
        ///     }
        /// </remarks>
        [HttpGet]
        [Route("{contentName:regex(^(?!subs$)[A-Za-z0-9_-]+$)}")]
        public IHttpActionResult GetContentInstance(string appName, string containerName, string contentName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return BadRequest("Application name is required in URL");
            }

            if (string.IsNullOrWhiteSpace(containerName))
            {
                return BadRequest("Container name is required in URL");
            }

            if (string.IsNullOrWhiteSpace(contentName))
            {
                return BadRequest("Content-instance name is required in URL");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    string query = @"
                        SELECT ci.Id, ci.Name, ci.ContentType, ci.Content, ci.CreationDateTime, c.Name as ContainerName
                        FROM ContentInstances ci
                        JOIN Containers c ON ci.ParentId = c.Id
                        JOIN Applications a ON c.ParentId = a.Id
                        WHERE ci.Name = @ContentName AND c.Name = @ContainerName AND a.Name = @AppName";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@ContentName", SqlDbType.NVarChar, 255).Value = contentName;
                        cmd.Parameters.Add("@ContainerName", SqlDbType.NVarChar, 255).Value = containerName;
                        cmd.Parameters.Add("@AppName", SqlDbType.NVarChar, 255).Value = appName;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var contentData = new
                                {
                                    id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    res_type = "content-instance",
                                    resource_name = reader.GetString(reader.GetOrdinal("Name")),
                                    parent = reader.GetString(reader.GetOrdinal("ContainerName")),
                                    content_type = reader.GetString(reader.GetOrdinal("ContentType")),
                                    content = reader.GetString(reader.GetOrdinal("Content")),
                                    creation_datetime = reader.GetDateTime(reader.GetOrdinal("CreationDateTime"))
                                        .ToString("yyyy-MM-ddTHH:mm:ss")
                                };

                                return Ok(contentData);
                            }
                            else
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new
                                    {
                                        error = $"Content-instance '{contentName}' not found under container '{containerName}' in application '{appName}'",
                                        res_type = "content-instance"
                                    });
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in GetContentInstance: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while retrieving the content-instance."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetContentInstance: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }

        /// <summary>
        /// Deletes a content-instance and triggers deletion notifications
        /// </summary>
        /// <param name="appName">Parent application resource_name</param>
        /// <param name="containerName">Parent container resource_name</param>
        /// <param name="contentName">Content-instance resource_name to delete</param>
        /// <returns>Deletion confirmation</returns>
        /// <response code="200">Content-instance deleted successfully</response>
        /// <response code="400">Application, container, or content-instance name is missing</response>
        /// <response code="404">Content-instance, container, or application not found</response>
        /// <remarks>
        /// Deletes a content-instance resource. This triggers notifications to all matching 
        /// subscriptions (evt=2 deletion) with the full resource data before deletion.
        /// 
        /// **cURL Command:**
        /// 
        ///     curl -X DELETE "https://localhost:44346/api/somiod/smart-home/living-room/temperature-1" -k
        ///     
        /// **Response (200 OK):**
        /// 
        ///     {
        ///        "message": "Content-instance 'temperature-1' deleted successfully",
        ///        "deleted_resource": "temperature-1",
        ///        "parent": "living-room",
        ///        "res_type": "content-instance"
        ///     }
        ///     
        /// **Notification Payload (sent to subscribers):**
        /// 
        ///     {
        ///        "subscription_name": "my-listener",
        ///        "event_type": "deletion",
        ///        "resource_name": "temperature-1",
        ///        "container_path": "api/somiod/smart-home/living-room",
        ///        "timestamp": "2025-01-15T10:45:00",
        ///        "resource": {
        ///            "id": 1,
        ///            "res_type": "content-instance",
        ///            "resource_name": "temperature-1",
        ///            "parent": "living-room",
        ///            "content_type": "application/json",
        ///            "content": "{\"value\": 23.5, \"unit\": \"celsius\"}",
        ///            "creation_datetime": "2025-01-15T10:40:00"
        ///        }
        ///     }
        ///     
        /// **Note:** The full resource data is included in notifications so subscribers 
        /// can access the content before it's permanently deleted.
        /// </remarks>
        [HttpDelete]
        [Route("{contentName:regex(^(?!subs$)[A-Za-z0-9_-]+$)}")]
        public IHttpActionResult DeleteContentInstance(string appName, string containerName, string contentName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return BadRequest("Application name is required");
            }

            if (string.IsNullOrWhiteSpace(containerName))
            {
                return BadRequest("Container name is required");
            }

            if (string.IsNullOrWhiteSpace(contentName))
            {
                return BadRequest("Content-instance name is required");
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

                    // Fetch full content-instance data BEFORE deletion (for notification)
                    string checkQuery = @"
                        SELECT ci.Id, ci.Name, ci.ContentType, ci.Content, ci.CreationDateTime
                        FROM ContentInstances ci
                        WHERE ci.Name = @ContentName AND ci.ParentId = @ParentId";

                    int contentId;
                    string actualName;
                    object resourceData = null;

                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.Add("@ContentName", SqlDbType.NVarChar, 255).Value = contentName;
                        checkCmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = containerId;

                        using (SqlDataReader reader = checkCmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new
                                    {
                                        error = $"Content-instance '{contentName}' not found under container '{containerName}'",
                                        res_type = "content-instance"
                                    });
                            }

                            contentId = reader.GetInt32(reader.GetOrdinal("Id"));
                            actualName = reader.GetString(reader.GetOrdinal("Name"));

                            // Build full resource data for notification
                            resourceData = new
                            {
                                id = contentId,
                                res_type = "content-instance",
                                resource_name = actualName,
                                parent = containerName,
                                content_type = reader.GetString(reader.GetOrdinal("ContentType")),
                                content = reader.GetString(reader.GetOrdinal("Content")),
                                creation_datetime = reader.GetDateTime(reader.GetOrdinal("CreationDateTime"))
                                    .ToString("yyyy-MM-ddTHH:mm:ss")
                            };
                        }
                    }

                    // Trigger notifications BEFORE deletion with full resource data (don't let notification failures affect the API response)
                    try
                    {
                        string containerPath = $"api/somiod/{appName}/{containerName}";
                        NotificationService.TriggerNotifications(
                            containerId,
                            2, // evt=2 (deletion)
                            contentName,
                            containerPath,
                            resourceData
                        );
                    }
                    catch (Exception notifEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Notification error (non-fatal): {notifEx.Message}");
                    }

                    string deleteQuery = "DELETE FROM ContentInstances WHERE Id = @Id";

                    using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, conn))
                    {
                        deleteCmd.Parameters.Add("@Id", SqlDbType.Int).Value = contentId;
                        int rowsAffected = deleteCmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            return InternalServerError(new Exception("Delete failed unexpectedly"));
                        }
                    }

                    var response = new
                    {
                        message = $"Content-instance '{actualName}' deleted successfully",
                        deleted_resource = actualName,
                        parent = containerName,
                        res_type = "content-instance"
                    };

                    return Ok(response);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in DeleteContentInstance: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while deleting the content-instance."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in DeleteContentInstance: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }
    }
}