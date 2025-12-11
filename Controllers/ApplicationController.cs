using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Web.Http;
using SOMIOD.Models;
using SOMIOD.Helpers;

namespace SOMIOD.Controllers
{
    [RoutePrefix("api/somiod")]
    public class ApplicationController : ApiController
    {
        private readonly string connectionString;

        public ApplicationController()
        {
            connectionString = SOMIOD.Properties.Settings.Default.ConnStr;
        }


        /// <summary>
        /// Creates a new application resource in the SOMIOD middleware
        /// </summary>
        /// <param name="app">Application object with resource_name (optional)</param>
        /// <returns>Created application with all properties</returns>
        /// <response code="201">Application created successfully</response>
        /// <response code="400">Invalid input - resource name contains invalid characters</response>
        /// <response code="409">Application with this name already exists</response>
        /// <remarks>
        /// Creates a new top-level application resource.
        /// 
        /// **cURL Command:**
        /// 
        ///     curl -X POST "https://localhost:44346/api/somiod" \
        ///          -H "Content-Type: application/json" \
        ///          -d "{\"resource_name\": \"smart-home\"}" -k
        ///     
        /// **Auto-generation:** If resource_name is omitted or empty, a unique name will be auto-generated:
        /// 
        ///     curl -X POST "https://localhost:44346/api/somiod" \
        ///          -H "Content-Type: application/json" \
        ///          -d "{}" -k
        ///     
        /// **Response (201 Created):**
        /// 
        ///     HTTP/1.1 201 Created
        ///     Location: https://localhost:44346/api/somiod/smart-home
        ///     
        ///     {
        ///        "id": 1,
        ///        "res_type": "application",
        ///        "resource_name": "smart-home",
        ///        "creation_datetime": "2025-01-15T10:30:45"
        ///     }
        /// </remarks>
        [HttpPost]
        [Route("")]
        public IHttpActionResult PostApplication([FromBody] Application app)
        {
            if (app == null)
            {
                app = new Application();
            }

            if (string.IsNullOrWhiteSpace(app.resource_name))
            {
                app.resource_name = ValidationHelper.GenerateUniqueResourceName("app");
            }

            if (!ValidationHelper.IsValidResourceName(app.resource_name))
            {
                return BadRequest("Resource name contains invalid characters. Use only letters, numbers, hyphens, and underscores.");
            }

            app.creation_datetime = DateTime.Now;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    if (DatabaseHelper.ApplicationExists(conn, app.resource_name))
                    {
                        return Content(HttpStatusCode.Conflict,
                            new
                            {
                                error = $"Application '{app.resource_name}' already exists",
                                res_type = "application"
                            });
                    }

                    string insertQuery = @"
                        INSERT INTO Applications (Name, CreationDateTime) 
                        VALUES (@Name, @CreationDateTime);
                        SELECT SCOPE_IDENTITY();";

                    int newId;
                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = app.resource_name;
                        insertCmd.Parameters.Add("@CreationDateTime", SqlDbType.DateTime).Value = app.creation_datetime;

                        newId = Convert.ToInt32(insertCmd.ExecuteScalar());
                    }

                    var response = new
                    {
                        id = newId,
                        res_type = app.res_type,
                        resource_name = app.resource_name,
                        creation_datetime = app.creation_datetime.ToString("yyyy-MM-ddTHH:mm:ss")
                    };

                    var locationUri = new Uri(Request.RequestUri, $"/api/somiod/{app.resource_name}");
                    return Created(locationUri, response);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in PostApplication: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while creating the application."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in PostApplication: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }

        /// <summary>
        /// Gets application by name OR discovers child resources (with somiod-discovery header)
        /// </summary>
        /// <param name="appName">Application resource-name</param>
        /// <returns>Application properties or list of child resource paths</returns>
        /// <response code="200">Application found or discovery successful</response>
        /// <response code="400">Invalid discovery type</response>
        /// <response code="404">Application not found</response>
        /// <remarks>
        /// **Get Application - cURL Command:**
        /// 
        ///     curl -X GET "https://localhost:44346/api/somiod/smart-home" -k
        ///     
        /// **Response (200 OK):**
        /// 
        ///     {
        ///        "id": 1,
        ///        "res_type": "application",
        ///        "resource_name": "smart-home",
        ///        "creation_datetime": "2025-01-15T10:30:45"
        ///     }
        ///     
        /// ---
        /// 
        /// **Discover Containers under Application - cURL Command:**
        /// 
        ///     curl -X GET "https://localhost:44346/api/somiod/smart-home" \
        ///          -H "somiod-discovery: container" -k
        ///     
        /// **Discover Content-Instances under Application - cURL Command:**
        /// 
        ///     curl -X GET "https://localhost:44346/api/somiod/smart-home" \
        ///          -H "somiod-discovery: content-instance" -k
        ///     
        /// **Discover Subscriptions under Application - cURL Command:**
        /// 
        ///     curl -X GET "https://localhost:44346/api/somiod/smart-home" \
        ///          -H "somiod-discovery: subscription" -k
        ///     
        /// **Discovery Response (200 OK):**
        /// 
        ///     [
        ///        "/api/somiod/smart-home/living-room",
        ///        "/api/somiod/smart-home/kitchen"
        ///     ]
        /// </remarks>
        [HttpGet]
        [Route("{appName}")]
        public IHttpActionResult GetApplicationOrDiscoverContainer(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return BadRequest("Application name is required");
            }

            // Check for discovery header
            if (Request.Headers.Contains("somiod-discovery"))
            {
                return DiscoverChildResources(appName);
            }

            // Regular GET application
            return GetApplicationByName(appName);
        }

        private IHttpActionResult GetApplicationByName(string appName)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    string query = "SELECT Id, Name, CreationDateTime FROM Applications WHERE Name = @Name";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = appName;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return Ok(new
                                {
                                    id = reader.GetInt32(0),
                                    res_type = "application",
                                    resource_name = reader.GetString(1),
                                    creation_datetime = reader.GetDateTime(2).ToString("yyyy-MM-ddTHH:mm:ss")
                                });
                            }
                            else
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new { error = $"Application '{appName}' not found", res_type = "application" });
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error: {ex.Message}");
                    return InternalServerError(new Exception("Database error occurred."));
                }
            }
        }

        private IHttpActionResult DiscoverChildResources(string appName)
        {
            var discoveryType = Request.Headers.GetValues("somiod-discovery").FirstOrDefault()?.ToLower();

            var validTypes = new[] { "container", "content-instance", "subscription" };
            if (!validTypes.Contains(discoveryType))
            {
                return Content(HttpStatusCode.BadRequest,
                    new { error = $"Invalid discovery type '{discoveryType}'", valid_types = validTypes, res_type = "error" });
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    int appId = DatabaseHelper.GetApplicationId(conn, appName);
                    if (appId == -1)
                    {
                        return Content(HttpStatusCode.NotFound,
                            new { error = $"Application '{appName}' not found", res_type = "application" });
                    }

                    var paths = new List<string>();
                    string query = "";

                    switch (discoveryType)
                    {
                        case "container":
                            query = "SELECT Name FROM Containers WHERE ParentId = @AppId ORDER BY Name";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                cmd.Parameters.Add("@AppId", SqlDbType.Int).Value = appId;
                                using (SqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        paths.Add($"/api/somiod/{appName}/{reader.GetString(0)}");
                                    }
                                }
                            }
                            break;

                        case "content-instance":
                            query = @"SELECT c.Name, ci.Name FROM ContentInstances ci 
                                     JOIN Containers c ON ci.ParentId = c.Id 
                                     WHERE c.ParentId = @AppId ORDER BY c.Name, ci.Name";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                cmd.Parameters.Add("@AppId", SqlDbType.Int).Value = appId;
                                using (SqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        paths.Add($"/api/somiod/{appName}/{reader.GetString(0)}/{reader.GetString(1)}");
                                    }
                                }
                            }
                            break;

                        case "subscription":
                            query = @"SELECT c.Name, s.Name FROM Subscriptions s 
                                     JOIN Containers c ON s.ParentId = c.Id 
                                     WHERE c.ParentId = @AppId ORDER BY c.Name, s.Name";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                cmd.Parameters.Add("@AppId", SqlDbType.Int).Value = appId;
                                using (SqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        paths.Add($"/api/somiod/{appName}/{reader.GetString(0)}/subs/{reader.GetString(1)}");
                                    }
                                }
                            }
                            break;
                    }

                    return Ok(paths);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error: {ex.Message}");
                    return InternalServerError(new Exception("Database error occurred."));
                }
            }
        }

        /// <summary>
        /// Updates an existing application resource_name
        /// </summary>
        /// <param name="appName">Current resource_name of the application</param>
        /// <param name="app">Application object with new resource_name</param>
        /// <returns>Updated application properties</returns>
        /// <response code="200">Application updated successfully</response>
        /// <response code="400">Invalid input data or resource name</response>
        /// <response code="404">Application not found</response>
        /// <response code="409">New name conflicts with existing application</response>
        /// <remarks>
        /// Updates the resource_name of an existing application.
        /// 
        /// **cURL Command:**
        /// 
        ///     curl -X PUT "https://localhost:44346/api/somiod/smart-home" \
        ///          -H "Content-Type: application/json" \
        ///          -d "{\"resource_name\": \"smart-home-v2\"}" -k
        ///     
        /// **Response (200 OK):**
        /// 
        ///     {
        ///        "id": 1,
        ///        "res_type": "application",
        ///        "resource_name": "smart-home-v2",
        ///        "creation_datetime": "2025-01-15T10:30:45"
        ///     }
        ///     
        /// **Note:** The creation_datetime remains unchanged after update.
        /// </remarks>
        [HttpPut]
        [Route("{appName}")]
        public IHttpActionResult PutApplication(string appName, [FromBody] Application app)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return BadRequest("Application name is required in URL");
            }

            if (app == null || string.IsNullOrWhiteSpace(app.resource_name))
            {
                return BadRequest("Application object with resource_name is required");
            }

            if (!ValidationHelper.IsValidResourceName(app.resource_name))
            {
                return BadRequest("New resource name contains invalid characters.");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    string checkExistsQuery = "SELECT Id, CreationDateTime FROM Applications WHERE Name = @OldName";
                    int appId;
                    DateTime creationDateTime;

                    using (SqlCommand checkCmd = new SqlCommand(checkExistsQuery, conn))
                    {
                        checkCmd.Parameters.Add("@OldName", SqlDbType.NVarChar, 255).Value = appName;

                        using (SqlDataReader reader = checkCmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new
                                    {
                                        error = $"Application '{appName}' not found",
                                        res_type = "application"
                                    });
                            }

                            appId = reader.GetInt32(reader.GetOrdinal("Id"));
                            creationDateTime = reader.GetDateTime(reader.GetOrdinal("CreationDateTime"));
                        }
                    }

                    if (!appName.Equals(app.resource_name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (DatabaseHelper.ApplicationExists(conn, app.resource_name))
                        {
                            return Content(HttpStatusCode.Conflict,
                                new
                                {
                                    error = $"Application '{app.resource_name}' already exists",
                                    res_type = "application"
                                });
                        }
                    }

                    string updateQuery = "UPDATE Applications SET Name = @NewName WHERE Id = @Id";

                    using (SqlCommand updateCmd = new SqlCommand(updateQuery, conn))
                    {
                        updateCmd.Parameters.Add("@NewName", SqlDbType.NVarChar, 255).Value = app.resource_name;
                        updateCmd.Parameters.Add("@Id", SqlDbType.Int).Value = appId;

                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            return InternalServerError(new Exception("Update failed unexpectedly"));
                        }
                    }

                    var response = new
                    {
                        id = appId,
                        res_type = "application",
                        resource_name = app.resource_name,
                        creation_datetime = creationDateTime.ToString("yyyy-MM-ddTHH:mm:ss")
                    };

                    return Ok(response);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in PutApplication: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while updating the application."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in PutApplication: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }

        /// <summary>
        /// Deletes an application and all child resources (CASCADE)
        /// </summary>
        /// <param name="appName">The resource_name of the application to delete</param>
        /// <returns>Deletion confirmation with cascade statistics</returns>
        /// <response code="200">Application deleted successfully</response>
        /// <response code="400">Application name is missing</response>
        /// <response code="404">Application not found</response>
        /// <remarks>
        /// Deletes an application and ALL its child resources via CASCADE delete.
        /// 
        /// **cURL Command:**
        /// 
        ///     curl -X DELETE "https://localhost:44346/api/somiod/smart-home" -k
        ///     
        /// **Response (200 OK):**
        /// 
        ///     {
        ///        "message": "Application 'smart-home' deleted successfully",
        ///        "deleted_resource": "smart-home",
        ///        "res_type": "application",
        ///        "cascade_info": {
        ///            "containers_deleted": 3,
        ///            "note": "All child content-instances and subscriptions were also deleted"
        ///        }
        ///     }
        ///     
        /// **Warning:** This operation is irreversible. All containers, content-instances, 
        /// and subscriptions under this application will be permanently deleted.
        /// </remarks>
        [HttpDelete]
        [Route("{appName}")]
        public IHttpActionResult DeleteApplication(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return BadRequest("Application name is required");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    string checkQuery = @"
                        SELECT a.Id, a.Name,
                               (SELECT COUNT(*) FROM Containers WHERE ParentId = a.Id) as ContainerCount
                        FROM Applications a
                        WHERE a.Name = @Name";

                    int appId;
                    string actualName;
                    int containerCount;

                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = appName;

                        using (SqlDataReader reader = checkCmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new
                                    {
                                        error = $"Application '{appName}' not found",
                                        res_type = "application"
                                    });
                            }

                            appId = reader.GetInt32(reader.GetOrdinal("Id"));
                            actualName = reader.GetString(reader.GetOrdinal("Name"));
                            containerCount = reader.GetInt32(reader.GetOrdinal("ContainerCount"));
                        }
                    }

                    string deleteQuery = "DELETE FROM Applications WHERE Id = @Id";

                    using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, conn))
                    {
                        deleteCmd.Parameters.Add("@Id", SqlDbType.Int).Value = appId;
                        int rowsAffected = deleteCmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            return InternalServerError(new Exception("Delete failed unexpectedly"));
                        }
                    }

                    var response = new
                    {
                        message = $"Application '{actualName}' deleted successfully",
                        deleted_resource = actualName,
                        res_type = "application",
                        cascade_info = new
                        {
                            containers_deleted = containerCount,
                            note = "All child content-instances and subscriptions were also deleted"
                        }
                    };

                    return Ok(response);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in DeleteApplication: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while deleting the application."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in DeleteApplication: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }


        /// <summary>
        /// Discovers all resources in the SOMIOD middleware (global discovery)
        /// </summary>
        /// <returns>Array of paths to all resources of the specified type</returns>
        /// <response code="200">Discovery successful - returns array of resource paths</response>
        /// <response code="400">Missing or invalid somiod-discovery header</response>
        /// <remarks>
        /// Discovers resources globally. Requires the somiod-discovery header.
        /// 
        /// **Discover All Applications - cURL Command:**
        /// 
        ///     curl -X GET "https://localhost:44346/api/somiod" \
        ///          -H "somiod-discovery: application" -k
        ///     
        /// **Response (200 OK):**
        /// 
        ///     [
        ///        "/api/somiod/smart-home",
        ///        "/api/somiod/office-automation",
        ///        "/api/somiod/warehouse-monitoring"
        ///     ]
        ///     
        /// ---
        /// 
        /// **Discover All Containers (Global) - cURL Command:**
        /// 
        ///     curl -X GET "https://localhost:44346/api/somiod" \
        ///          -H "somiod-discovery: container" -k
        ///     
        /// **Response (200 OK):**
        /// 
        ///     [
        ///        "/api/somiod/smart-home/living-room",
        ///        "/api/somiod/smart-home/kitchen",
        ///        "/api/somiod/office-automation/meeting-room"
        ///     ]
        ///     
        /// ---
        /// 
        /// **Discover All Content-Instances (Global) - cURL Command:**
        /// 
        ///     curl -X GET "https://localhost:44346/api/somiod" \
        ///          -H "somiod-discovery: content-instance" -k
        ///     
        /// ---
        /// 
        /// **Discover All Subscriptions (Global) - cURL Command:**
        /// 
        ///     curl -X GET "https://localhost:44346/api/somiod" \
        ///          -H "somiod-discovery: subscription" -k
        ///     
        /// **Note:** Returns empty array [] if no resources exist.
        /// 
        /// **Valid discovery types:** application | container | content-instance | subscription
        /// </remarks>
        [HttpGet]
        [Route("")]
        public IHttpActionResult DiscoverApplications()
        {
            if (!Request.Headers.Contains("somiod-discovery"))
            {
                return Content(HttpStatusCode.BadRequest,
                    new
                    {
                        error = "Discovery header required. Add 'somiod-discovery: <type>' header.",
                        hint = "GET all without discovery header is not supported.",
                        valid_types = new[] { "application", "container", "content-instance", "subscription" },
                        res_type = "error"
                    });
            }

            var discoveryValues = Request.Headers.GetValues("somiod-discovery");
            string discoveryType = discoveryValues.FirstOrDefault()?.ToLower();

            var validTypes = new[] { "application", "container", "content-instance", "subscription" };
            if (!validTypes.Contains(discoveryType))
            {
                return Content(HttpStatusCode.BadRequest,
                    new
                    {
                        error = $"Invalid discovery type '{discoveryType}' at this endpoint.",
                        valid_types = validTypes,
                        res_type = "error"
                    });
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    var paths = new List<string>();
                    string query = "";

                    switch (discoveryType)
                    {
                        case "application":
                            query = "SELECT Name FROM Applications ORDER BY Name";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                using (SqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        string name = reader.GetString(0);
                                        paths.Add($"/api/somiod/{name}");
                                    }
                                }
                            }
                            break;

                        case "container":
                            query = @"SELECT a.Name AS AppName, c.Name AS ContainerName 
                                     FROM Containers c 
                                     JOIN Applications a ON c.ParentId = a.Id 
                                     ORDER BY a.Name, c.Name";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                using (SqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        string appName = reader.GetString(0);
                                        string containerName = reader.GetString(1);
                                        paths.Add($"/api/somiod/{appName}/{containerName}");
                                    }
                                }
                            }
                            break;

                        case "content-instance":
                            query = @"SELECT a.Name AS AppName, c.Name AS ContainerName, ci.Name AS ContentInstanceName 
                                     FROM ContentInstances ci 
                                     JOIN Containers c ON ci.ParentId = c.Id 
                                     JOIN Applications a ON c.ParentId = a.Id 
                                     ORDER BY a.Name, c.Name, ci.Name";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                using (SqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        string appName = reader.GetString(0);
                                        string containerName = reader.GetString(1);
                                        string contentInstanceName = reader.GetString(2);
                                        paths.Add($"/api/somiod/{appName}/{containerName}/{contentInstanceName}");
                                    }
                                }
                            }
                            break;

                        case "subscription":
                            query = @"SELECT a.Name AS AppName, c.Name AS ContainerName, s.Name AS SubscriptionName 
                                     FROM Subscriptions s 
                                     JOIN Containers c ON s.ParentId = c.Id 
                                     JOIN Applications a ON c.ParentId = a.Id 
                                     ORDER BY a.Name, c.Name, s.Name";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                using (SqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        string appName = reader.GetString(0);
                                        string containerName = reader.GetString(1);
                                        string subscriptionName = reader.GetString(2);
                                        paths.Add($"/api/somiod/{appName}/{containerName}/subs/{subscriptionName}");
                                    }
                                }
                            }
                            break;
                    }

                    return Ok(paths);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in DiscoverApplications: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred during discovery."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in DiscoverApplications: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }


        /// <summary>
        /// Creates a new container under a specific application
        /// </summary>
        /// <param name="appName">Parent application resource_name</param>
        /// <param name="container">Container object with resource_name (optional)</param>
        /// <returns>Created container with all properties</returns>
        /// <response code="201">Container created successfully</response>
        /// <response code="400">Invalid input - resource name contains invalid characters</response>
        /// <response code="404">Parent application not found</response>
        /// <response code="409">Container with this name already exists under this application</response>
        /// <remarks>
        /// Creates a new container resource under an existing application.
        /// 
        /// **cURL Command:**
        /// 
        ///     curl -X POST "https://localhost:44346/api/somiod/smart-home" \
        ///          -H "Content-Type: application/json" \
        ///          -d "{\"resource_name\": \"living-room\"}" -k
        ///     
        /// **Auto-generation:** If resource_name is omitted, a unique name will be auto-generated:
        /// 
        ///     curl -X POST "https://localhost:44346/api/somiod/smart-home" \
        ///          -H "Content-Type: application/json" \
        ///          -d "{}" -k
        ///     
        /// **Response (201 Created):**
        /// 
        ///     HTTP/1.1 201 Created
        ///     Location: https://localhost:44346/api/somiod/smart-home/living-room
        ///     
        ///     {
        ///        "id": 1,
        ///        "res_type": "container",
        ///        "resource_name": "living-room",
        ///        "parent": "smart-home",
        ///        "creation_datetime": "2025-01-15T10:35:22"
        ///     }
        /// </remarks>
        [HttpPost]
        [Route("{appName}")]
        public IHttpActionResult PostContainer(string appName, [FromBody] Container container)
        {
            if (string.IsNullOrWhiteSpace(appName))
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

                    if (DatabaseHelper.ContainerExists(conn, container.resource_name, parentId))
                    {
                        return Content(HttpStatusCode.Conflict,
                            new
                            {
                                error = $"Container '{container.resource_name}' already exists under application '{appName}'",
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
                        parent = appName,
                        creation_datetime = container.creation_datetime.ToString("yyyy-MM-ddTHH:mm:ss")
                    };

                    var locationUri = new Uri(Request.RequestUri, $"/api/somiod/{appName}/{container.resource_name}");
                    return Created(locationUri, response);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error in PostContainer: {ex.Message}");
                    return InternalServerError(new Exception("An error occurred while creating the container."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in PostContainer: {ex.Message}");
                    return InternalServerError(ex);
                }
            }
        }
    }
}