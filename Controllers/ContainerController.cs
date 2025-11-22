//==============================================================
// SOMIOD - Service Oriented Middleware for IoT and Open Data
// Course: Integração de Sistemas
// Year: 2025/2026
// 
// File: ContainersController.cs
// Description: RESTful API controller for Container resources
//              Implements CRUD + Discovery operations
//==============================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Web.Http;
using SOMIOD.Helpers;
using SOMIOD.Models;

namespace SOMIOD.Controllers
{
    [RoutePrefix("api/somiod/{appName}")]
    public class ContainersController : ApiController
    {
        private readonly string connectionString;

        public ContainersController()
        {
            connectionString = SOMIOD.Properties.Settings.Default.ConnStr;
        }
 

        /// <summary>
        /// Retrieves a specific container by resource-name
        /// </summary>
        /// <param name="appName">Parent application resource-name</param>
        /// <param name="containerName">Container resource-name</param>
        /// <returns>Container properties</returns>
        /// <response code="200">Container found and returned</response>
        /// <response code="400">Application or container name is missing</response>
        /// <response code="404">Container or parent application not found</response>
        /// <remarks>
        /// Retrieves a container resource by its unique resource-name within an application.
        /// 
        /// **Sample request:**
        /// 
        ///     GET /api/somiod/smart-home/living-room
        ///     Content-Type: application/json
        ///     
        /// **Sample response (200 OK):**
        /// 
        ///     {
        ///        "id": 1,
        ///        "res_type": "container",
        ///        "resource_name": "living-room",
        ///        "parent": "smart-home",
        ///        "creation_datetime": "2025-01-15T10:35:22"
        ///     }
        /// </remarks>
        [HttpGet]
        [Route("{containerName}")]
        public IHttpActionResult GetContainer(string appName, string containerName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return BadRequest("Application name is required in URL");
            }

            if (string.IsNullOrWhiteSpace(containerName))
            {
                return BadRequest("Container name is required in URL");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    string query = @"
                        SELECT c.Id, c.Name, c.CreationDateTime, a.Name as AppName
                        FROM Containers c
                        JOIN Applications a ON c.ParentId = a.Id
                        WHERE c.Name = @ContainerName AND a.Name = @AppName";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@ContainerName", SqlDbType.NVarChar, 255).Value = containerName;
                        cmd.Parameters.Add("@AppName", SqlDbType.NVarChar, 255).Value = appName;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var containerData = new
                                {
                                    id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    res_type = "container",
                                    resource_name = reader.GetString(reader.GetOrdinal("Name")),
                                    parent = reader.GetString(reader.GetOrdinal("AppName")),
                                    creation_datetime = reader.GetDateTime(reader.GetOrdinal("CreationDateTime"))
                                        .ToString("yyyy-MM-ddTHH:mm:ss")
                                };

                                return Ok(containerData);
                            }
                            else
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new
                                    {
                                        error = $"Container '{containerName}' not found under application '{appName}'",
                                        res_type = "container"
                                    });
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in GetContainer: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while retrieving the container."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetContainer: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }

        /// <summary>
        /// Updates an existing container resource-name
        /// </summary>
        /// <param name="appName">Parent application resource-name</param>
        /// <param name="containerName">Current container resource-name</param>
        /// <param name="container">Container object with new resource-name</param>
        /// <returns>Updated container properties</returns>
        /// <response code="200">Container updated successfully</response>
        /// <response code="400">Invalid input data or resource name</response>
        /// <response code="404">Container or parent application not found</response>
        /// <response code="409">New name conflicts with existing container</response>
        /// <remarks>
        /// Updates the resource-name of an existing container.
        /// 
        /// **Sample request:**
        /// 
        ///     PUT /api/somiod/smart-home/living-room
        ///     Content-Type: application/json
        ///     
        ///     {
        ///        "resource_name": "main-living-room"
        ///     }
        ///     
        /// **Sample response (200 OK):**
        /// 
        ///     {
        ///        "id": 1,
        ///        "res_type": "container",
        ///        "resource_name": "main-living-room",
        ///        "parent": "smart-home",
        ///        "creation_datetime": "2025-01-15T10:35:22"
        ///     }
        ///     
        /// **Note:** The creation_datetime remains unchanged after update.
        /// </remarks>
        [HttpPut]
        [Route("{containerName}")]
        public IHttpActionResult PutContainer(string appName, string containerName, [FromBody] Container container)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return BadRequest("Application name is required in URL");
            }

            if (string.IsNullOrWhiteSpace(containerName))
            {
                return BadRequest("Container name is required in URL");
            }

            if (container == null || string.IsNullOrWhiteSpace(container.resource_name))
            {
                return BadRequest("Container object with resource_name is required");
            }

            if (!ValidationHelper.IsValidResourceName(container.resource_name))
            {
                return BadRequest("New resource name contains invalid characters.");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    int parentId = DatabaseHelper.GetApplicationId(conn, appName);
                    if (parentId == -1)
                    {
                        return Content(HttpStatusCode.NotFound,
                            new
                            {
                                error = $"Parent application '{appName}' not found",
                                res_type = "application"
                            });
                    }

                    string checkQuery = @"
                        SELECT c.Id, c.CreationDateTime
                        FROM Containers c
                        WHERE c.Name = @OldName AND c.ParentId = @ParentId";

                    int containerId;
                    DateTime creationDateTime;

                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.Add("@OldName", SqlDbType.NVarChar, 255).Value = containerName;
                        checkCmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentId;

                        using (SqlDataReader reader = checkCmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new
                                    {
                                        error = $"Container '{containerName}' not found under application '{appName}'",
                                        res_type = "container"
                                    });
                            }

                            containerId = reader.GetInt32(reader.GetOrdinal("Id"));
                            creationDateTime = reader.GetDateTime(reader.GetOrdinal("CreationDateTime"));
                        }
                    }

                    if (!containerName.Equals(container.resource_name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (DatabaseHelper.ContainerExists(conn, container.resource_name, parentId))
                        {
                            return Content(HttpStatusCode.Conflict,
                                new
                                {
                                    error = $"Container '{container.resource_name}' already exists under application '{appName}'",
                                    res_type = "container"
                                });
                        }
                    }

                    string updateQuery = "UPDATE Containers SET Name = @NewName WHERE Id = @Id";

                    using (SqlCommand updateCmd = new SqlCommand(updateQuery, conn))
                    {
                        updateCmd.Parameters.Add("@NewName", SqlDbType.NVarChar, 255).Value = container.resource_name;
                        updateCmd.Parameters.Add("@Id", SqlDbType.Int).Value = containerId;

                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            return InternalServerError(new Exception("Update failed unexpectedly"));
                        }
                    }

                    var response = new
                    {
                        id = containerId,
                        res_type = "container",
                        resource_name = container.resource_name,
                        parent = appName,
                        creation_datetime = creationDateTime.ToString("yyyy-MM-ddTHH:mm:ss")
                    };

                    return Ok(response);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in PutContainer: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while updating the container."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in PutContainer: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }

        /// <summary>
        /// Deletes a container and all child resources (CASCADE)
        /// </summary>
        /// <param name="appName">Parent application resource-name</param>
        /// <param name="containerName">Container resource-name to delete</param>
        /// <returns>Deletion confirmation with cascade statistics</returns>
        /// <response code="200">Container deleted successfully</response>
        /// <response code="400">Application or container name is missing</response>
        /// <response code="404">Container or parent application not found</response>
        /// <remarks>
        /// Deletes a container and ALL its child resources via CASCADE delete.
        /// 
        /// **Sample request:**
        /// 
        ///     DELETE /api/somiod/smart-home/living-room
        ///     
        /// **Sample response (200 OK):**
        /// 
        ///     {
        ///        "message": "Container 'living-room' deleted successfully",
        ///        "deleted_resource": "living-room",
        ///        "parent": "smart-home",
        ///        "res_type": "container",
        ///        "cascade_info": {
        ///            "content_instances_deleted": 5,
        ///            "subscriptions_deleted": 2
        ///        }
        ///     }
        ///     
        /// **Warning:** This operation is irreversible. All content-instances and 
        /// subscriptions under this container will be permanently deleted.
        /// </remarks>
        [HttpDelete]
        [Route("{containerName}")]
        public IHttpActionResult DeleteContainer(string appName, string containerName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return BadRequest("Application name is required");
            }

            if (string.IsNullOrWhiteSpace(containerName))
            {
                return BadRequest("Container name is required");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    int parentId = DatabaseHelper.GetApplicationId(conn, appName);
                    if (parentId == -1)
                    {
                        return Content(HttpStatusCode.NotFound,
                            new
                            {
                                error = $"Parent application '{appName}' not found",
                                res_type = "application"
                            });
                    }

                    string checkQuery = @"
                        SELECT c.Id, c.Name,
                               (SELECT COUNT(*) FROM ContentInstances WHERE ParentId = c.Id) as ContentCount,
                               (SELECT COUNT(*) FROM Subscriptions WHERE ParentId = c.Id) as SubCount
                        FROM Containers c
                        WHERE c.Name = @ContainerName AND c.ParentId = @ParentId";

                    int containerId;
                    string actualName;
                    int contentCount;
                    int subCount;

                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.Add("@ContainerName", SqlDbType.NVarChar, 255).Value = containerName;
                        checkCmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentId;

                        using (SqlDataReader reader = checkCmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new
                                    {
                                        error = $"Container '{containerName}' not found under application '{appName}'",
                                        res_type = "container"
                                    });
                            }

                            containerId = reader.GetInt32(reader.GetOrdinal("Id"));
                            actualName = reader.GetString(reader.GetOrdinal("Name"));
                            contentCount = reader.GetInt32(reader.GetOrdinal("ContentCount"));
                            subCount = reader.GetInt32(reader.GetOrdinal("SubCount"));
                        }
                    }

                    string deleteQuery = "DELETE FROM Containers WHERE Id = @Id";

                    using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, conn))
                    {
                        deleteCmd.Parameters.Add("@Id", SqlDbType.Int).Value = containerId;
                        int rowsAffected = deleteCmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            return InternalServerError(new Exception("Delete failed unexpectedly"));
                        }
                    }

                    var response = new
                    {
                        message = $"Container '{actualName}' deleted successfully",
                        deleted_resource = actualName,
                        parent = appName,
                        res_type = "container",
                        cascade_info = new
                        {
                            content_instances_deleted = contentCount,
                            subscriptions_deleted = subCount
                        }
                    };

                    return Ok(response);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in DeleteContainer: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while deleting the container."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in DeleteContainer: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }
    }
}