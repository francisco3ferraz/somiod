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
        /// Retrieves a specific content-instance by resource-name
        /// </summary>
        /// <param name="appName">Parent application resource-name</param>
        /// <param name="containerName">Parent container resource-name</param>
        /// <param name="contentName">Content-instance resource-name</param>
        /// <returns>Content-instance properties</returns>
        /// <response code="200">Content-instance found and returned</response>
        /// <response code="400">Application, container, or content-instance name is missing</response>
        /// <response code="404">Content-instance, container, or application not found</response>
        [HttpGet]
        [Route("{contentName}")]
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
        /// Updates an existing content-instance
        /// </summary>
        /// <param name="appName">Parent application resource-name</param>
        /// <param name="containerName">Parent container resource-name</param>
        /// <param name="contentName">Current content-instance resource-name</param>
        /// <param name="contentInstance">ContentInstance object with updated properties</param>
        /// <returns>Updated content-instance properties</returns>
        /// <response code="200">Content-instance updated successfully</response>
        /// <response code="400">Invalid input data</response>
        /// <response code="404">Content-instance, container, or application not found</response>
        /// <response code="409">New name conflicts with existing content-instance</response>
        [HttpPut]
        [Route("{contentName}")]
        public IHttpActionResult PutContentInstance(string appName, string containerName, string contentName, [FromBody] ContentInstance contentInstance)
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

            if (contentInstance == null)
            {
                return BadRequest("ContentInstance object is required");
            }

            // If resource_name not provided in body, keep the current name
            if (string.IsNullOrWhiteSpace(contentInstance.resource_name))
            {
                contentInstance.resource_name = contentName;
            }

            if (!ValidationHelper.IsValidResourceName(contentInstance.resource_name))
            {
                return BadRequest("New resource name contains invalid characters.");
            }

            // Validate content_type if provided
            if (!string.IsNullOrWhiteSpace(contentInstance.content_type) && 
                !ValidationHelper.IsValidContentType(contentInstance.content_type))
            {
                return BadRequest("Invalid content_type format.");
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

                    // Get existing content-instance
                    string checkQuery = @"
                        SELECT ci.Id, ci.Name, ci.ContentType, ci.Content, ci.CreationDateTime
                        FROM ContentInstances ci
                        WHERE ci.Name = @OldName AND ci.ParentId = @ParentId";

                    int contentId;
                    DateTime creationDateTime;
                    string existingContentType;
                    string existingContent;

                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.Add("@OldName", SqlDbType.NVarChar, 255).Value = contentName;
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
                            creationDateTime = reader.GetDateTime(reader.GetOrdinal("CreationDateTime"));
                            existingContentType = reader.GetString(reader.GetOrdinal("ContentType"));
                            existingContent = reader.GetString(reader.GetOrdinal("Content"));
                        }
                    }

                    // Check for name conflict if name is changing
                    if (!contentName.Equals(contentInstance.resource_name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (DatabaseHelper.ContentInstanceExists(conn, contentInstance.resource_name, containerId))
                        {
                            return Content(HttpStatusCode.Conflict,
                                new
                                {
                                    error = $"Content-instance '{contentInstance.resource_name}' already exists under container '{containerName}'",
                                    res_type = "content-instance"
                                });
                        }
                    }

                    // Use existing values if not provided in update
                    string newContentType = string.IsNullOrWhiteSpace(contentInstance.content_type) 
                        ? existingContentType 
                        : contentInstance.content_type;
                    
                    string newContent = contentInstance.content ?? existingContent;

                    string updateQuery = @"
                        UPDATE ContentInstances 
                        SET Name = @NewName, ContentType = @ContentType, Content = @Content 
                        WHERE Id = @Id";

                    using (SqlCommand updateCmd = new SqlCommand(updateQuery, conn))
                    {
                        updateCmd.Parameters.Add("@NewName", SqlDbType.NVarChar, 255).Value = contentInstance.resource_name;
                        updateCmd.Parameters.Add("@ContentType", SqlDbType.NVarChar, 255).Value = newContentType;
                        updateCmd.Parameters.Add("@Content", SqlDbType.NVarChar).Value = newContent;
                        updateCmd.Parameters.Add("@Id", SqlDbType.Int).Value = contentId;

                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            return InternalServerError(new Exception("Update failed unexpectedly"));
                        }
                    }

                    var response = new
                    {
                        id = contentId,
                        res_type = "content-instance",
                        resource_name = contentInstance.resource_name,
                        parent = containerName,
                        content_type = newContentType,
                        content = newContent,
                        creation_datetime = creationDateTime.ToString("yyyy-MM-ddTHH:mm:ss")
                    };

                    return Ok(response);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in PutContentInstance: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while updating the content-instance."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in PutContentInstance: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }

        /// <summary>
        /// Deletes a content-instance
        /// </summary>
        /// <param name="appName">Parent application resource-name</param>
        /// <param name="containerName">Parent container resource-name</param>
        /// <param name="contentName">Content-instance resource-name to delete</param>
        /// <returns>Deletion confirmation</returns>
        /// <response code="200">Content-instance deleted successfully</response>
        /// <response code="400">Application, container, or content-instance name is missing</response>
        /// <response code="404">Content-instance, container, or application not found</response>
        [HttpDelete]
        [Route("{contentName}")]
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

                    // Check if content-instance exists
                    string checkQuery = @"
                        SELECT ci.Id, ci.Name
                        FROM ContentInstances ci
                        WHERE ci.Name = @ContentName AND ci.ParentId = @ParentId";

                    int contentId;
                    string actualName;

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
                        }
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
