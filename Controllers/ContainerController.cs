using SOMIOD.Helpers;
using SOMIOD.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace SOMIOD.Controllers
{
    [RoutePrefix("api/somiod/{parentApp}")]
    public class ContainersController : ApiController
    {
        private readonly string connectionString;

        public ContainersController()
        {
            connectionString = SOMIOD.Properties.Settings.Default.ConnStr;
        }

        /// <summary>
        /// Creates a new container resource under a specific application
        /// </summary>
        /// <param name="parentApp">The parent application resource-name</param>
        /// <param name="container">Container object containing resource-name (optional - will auto-generate if empty)</param>
        /// <returns>Created container with auto-generated fields</returns>
        /// <response code="201">Container created successfully</response>
        /// <response code="400">Invalid input data</response>
        /// <response code="404">Parent application not found</response>
        /// <response code="409">Container with this name already exists under this application</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /api/somiod/smart-home
        ///     {
        ///        "resource_name": "living-room"
        ///     }
        ///     
        /// If resource_name is omitted, a unique name will be auto-generated
        /// </remarks>
        [HttpPost]
        [Route("")]
        public IHttpActionResult PostContainer(string parentApp, [FromBody] Container container)
        {
            if (string.IsNullOrWhiteSpace(parentApp))
            {
                return BadRequest("Parent application name is required in URL");
            }

            if (container == null)
            {
                container = new Container();
            }

            if (string.IsNullOrWhiteSpace(container.resource_name))
            {
                container.resource_name = ValidationHelper.GenerateUniqueResourceName("container");
            }

            if (!ValidationHelper.IsValidResourceName(container.resource_name))
            {
                return BadRequest("Resource name contains invalid characters. Use only letters, numbers, hyphens, and underscores.");
            }

            container.creation_datetime = DateTime.Now;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    int parentId = DatabaseHelper.GetApplicationId(conn, parentApp);
                    if (parentId == -1)
                    {
                        return Content(HttpStatusCode.NotFound,
                            new
                            {
                                error = $"Parent application '{parentApp}' not found",
                                res_type = "application"
                            });
                    }

                    if (DatabaseHelper.ContainerExists(conn, container.resource_name, parentId))
                    {
                        return Content(HttpStatusCode.Conflict,
                            new
                            {
                                error = $"Container '{container.resource_name}' already exists under application '{parentApp}'",
                                res_type = "container"
                            });
                    }

                    string insertQuery = @"
                        INSERT INTO Containers (Name, ParentId, CreationDateTime) 
                        VALUES (@Name, @ParentId, @CreationDateTime);
                        SELECT SCOPE_IDENTITY();";

                    int newId;
                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = container.resource_name;
                        insertCmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentId;
                        insertCmd.Parameters.Add("@CreationDateTime", SqlDbType.DateTime).Value = container.creation_datetime;

                        newId = Convert.ToInt32(insertCmd.ExecuteScalar());
                    }

                    var response = new
                    {
                        id = newId,
                        res_type = container.res_type,
                        resource_name = container.resource_name,
                        parent = parentApp,
                        creation_datetime = container.creation_datetime.ToString("yyyy-MM-ddTHH:mm:ss")
                    };

                    var locationUri = new Uri(Request.RequestUri, $"/api/somiod/{parentApp}/{container.resource_name}");
                    return Created(locationUri, response);
                }
                catch (SqlException ex)
                {
                    return InternalServerError(new Exception("An error occurred while creating the container. Please try again."));
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }
        }

        /// <summary>
        /// Retrieves a specific container resource by name OR discovers resources under an application
        /// </summary>
        /// <param name="parentApp">The parent application resource-name</param>
        /// <response code="200">Container found and returned</response>
        /// <response code="404">Container or parent application not found</response>
        /// <remarks>
        /// Sample request (GET specific container):
        /// 
        ///     GET /api/somiod/smart-home/living-room
        ///     
        /// </remarks>
        [HttpGet]
        [Route("{containerName}")]
        public IHttpActionResult GetContainer(string parentApp, string containerName = null)
        {

            if (string.IsNullOrWhiteSpace(parentApp))
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

                    // Verify hierarchy and get container data
                    string query = @"
                        SELECT c.Id, c.Name, c.CreationDateTime, a.Name as parentApp
                        FROM Containers c
                        JOIN Applications a ON c.ParentId = a.Id
                        WHERE c.Name = @ContainerName AND a.Name = @parentApp";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@ContainerName", SqlDbType.NVarChar, 255).Value = containerName;
                        cmd.Parameters.Add("@parentApp", SqlDbType.NVarChar, 255).Value = parentApp;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var containerData = new
                                {
                                    id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    res_type = "container",
                                    resource_name = reader.GetString(reader.GetOrdinal("Name")),
                                    parent = reader.GetString(reader.GetOrdinal("parentApp")),
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
                                        error = $"Container '{containerName}' not found under application '{parentApp}'",
                                        res_type = "container"
                                    });
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    return InternalServerError(new Exception("An error occurred while retrieving the container."));
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }
        }

        /// <summary>
        /// Updates an existing container resource
        /// </summary>
        /// <param name="parentApp">Parent application resource-name</param>
        /// <param name="containerName">Current container resource-name</param>
        /// <param name="container">Container object with updated resource-name</param>
        /// <returns>Updated container properties</returns>
        /// <response code="200">Container updated successfully</response>
        /// <response code="404">Container or parent application not found</response>
        /// <response code="409">New name conflicts with existing container</response>
        /// <response code="400">Invalid input data</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     PUT /api/somiod/smart-home/living-room
        ///     {
        ///        "resource_name": "main-living-room"
        ///     }
        ///     
        /// </remarks>
        [HttpPut]
        [Route("{containerName}")]
        public IHttpActionResult PutContainer(string parentApp, string containerName, [FromBody] Container container)
        {
            if (string.IsNullOrWhiteSpace(parentApp))
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

                    int parentId = DatabaseHelper.GetApplicationId(conn, parentApp);
                    if (parentId == -1)
                    {
                        return Content(HttpStatusCode.NotFound,
                            new
                            {
                                error = $"Parent application '{parentApp}' not found",
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
                                        error = $"Container '{containerName}' not found under application '{parentApp}'",
                                        res_type = "container"
                                    });
                            }

                            containerId = reader.GetInt32(reader.GetOrdinal("Id"));
                            creationDateTime = reader.GetDateTime(reader.GetOrdinal("CreationDateTime"));
                        }
                    }

                    // Check if new name conflicts with another container (only if name is changing)
                    if (!containerName.Equals(container.resource_name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (DatabaseHelper.ContainerExists(conn, container.resource_name, parentId))
                        {
                            return Content(HttpStatusCode.Conflict,
                                new
                                {
                                    error = $"Container '{container.resource_name}' already exists under application '{parentApp}'",
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
                        parent = parentApp,
                        creation_datetime = creationDateTime.ToString("yyyy-MM-ddTHH:mm:ss")
                    };

                    return Ok(response);
                }
                catch (SqlException ex)
                {
                    return InternalServerError(new Exception("An error occurred while updating the container."));
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }
        }

        /// <summary>
        /// Deletes a container resource and all its child resources (content-instances, subscriptions)
        /// </summary>
        /// <param name="parentApp">Parent application resource-name</param>
        /// <param name="containerName">The container resource-name to delete</param>
        /// <returns>Success confirmation or error</returns>
        /// <response code="200">Container deleted successfully</response>
        /// <response code="404">Container or parent application not found</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     DELETE /api/somiod/smart-home/living-room
        ///     
        /// Warning: This will CASCADE delete all child resources (content-instances, subscriptions)
        /// </remarks>
        [HttpDelete]
        [Route("{containerName}")]
        public IHttpActionResult DeleteContainer(string parentApp, string containerName)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(parentApp))
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

                    // Step 1: Verify parent application exists
                    int parentId = DatabaseHelper.GetApplicationId(conn, parentApp);
                    if (parentId == -1)
                    {
                        return Content(HttpStatusCode.NotFound,
                            new
                            {
                                error = $"Parent application '{parentApp}' not found",
                                res_type = "application"
                            });
                    }

                    // Step 2: Check if container exists and get statistics
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
                                        error = $"Container '{containerName}' not found under application '{parentApp}'",
                                        res_type = "container"
                                    });
                            }

                            containerId = reader.GetInt32(reader.GetOrdinal("Id"));
                            actualName = reader.GetString(reader.GetOrdinal("Name"));
                            contentCount = reader.GetInt32(reader.GetOrdinal("ContentCount"));
                            subCount = reader.GetInt32(reader.GetOrdinal("SubCount"));
                        }
                    }

                    // Step 3: Delete the container (CASCADE will handle children)
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

                    // Step 4: Prepare detailed response
                    var response = new
                    {
                        message = $"Container '{actualName}' deleted successfully",
                        deleted_resource = actualName,
                        parent = parentApp,
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
                    return InternalServerError(new Exception("An error occurred while deleting the container."));
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }
        }


        /// <summary>
        /// Discovers all containers under a specific application
        /// </summary>
        /// <param name="parentApp">Parent application name</param>
        /// <returns>List of paths to containers</returns>
        /// <response code="200">Discovery successful</response>
        /// <response code="400">Missing or invalid discovery header</response>
        /// <response code="404">Parent application not found</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     GET /api/somiod/smart-home
        ///     Headers:
        ///       somiod-discovery: container
        ///     
        /// Returns: ["/api/somiod/smart-home/living-room", 
        ///           "/api/somiod/smart-home/bedroom"]
        /// </remarks>
        [HttpGet]
        [Route("")]
        public IHttpActionResult DiscoverContainers(string parentApp)
        {
            // Check for somiod-discovery header
            if (!Request.Headers.Contains("somiod-discovery"))
            {
                return Content(HttpStatusCode.BadRequest,
                    new
                    {
                        error = "Discovery header required. Use 'somiod-discovery: container'",
                        res_type = "error"
                    });
            }

            // Get discovery type from header
            var discoveryValues = Request.Headers.GetValues("somiod-discovery");
            string discoveryType = discoveryValues.FirstOrDefault()?.ToLower();

            // Validate discovery type
            if (discoveryType != "container")
            {
                return Content(HttpStatusCode.BadRequest,
                    new
                    {
                        error = $"Invalid discovery type '{discoveryType}'. Expected 'container'",
                        res_type = "error"
                    });
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    // Verify parent application exists
                    int parentId = DatabaseHelper.GetApplicationId(conn, parentApp);
                    if (parentId == -1)
                    {
                        return Content(HttpStatusCode.NotFound,
                            new
                            {
                                error = $"Application '{parentApp}' not found",
                                res_type = "application"
                            });
                    }

                    var paths = new List<string>();

                    string query = @"
                SELECT c.Name as ContainerName
                FROM Containers c
                WHERE c.ParentId = @ParentId
                ORDER BY c.Name";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentId;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string containerName = reader.GetString(0);
                                paths.Add($"/api/somiod/{parentApp}/{containerName}");
                            }
                        }
                    }

                    return Ok(paths);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in DiscoverContainers: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred during resource discovery."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in DiscoverContainers: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }
    }
}