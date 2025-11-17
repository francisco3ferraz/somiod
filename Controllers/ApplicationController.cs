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
    [RoutePrefix("api/somiod/applications")]
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
        /// <param name="app">Application object containing resource-name (optional - will auto-generate if empty)</param>
        /// <returns>Created application with auto-generated fields</returns>
        /// <response code="201">Application created successfully</response>
        /// <response code="400">Invalid input data</response>
        /// <response code="409">Application with this name already exists</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /api/somiod
        ///     {
        ///        "resource_name": "smart-home"
        ///     }
        ///     
        /// If resource_name is omitted, a unique name will be auto-generated
        /// </remarks>
        [HttpPost]
        [Route("")]
        public IHttpActionResult PostApplication([FromBody] Application app)
        {
            if (app == null)
            {
                app = new Application();
            }

            // Auto-generate resource_name if empty (Requirement)
            if (string.IsNullOrWhiteSpace(app.resource_name))
            {
                app.resource_name = ValidationHelper.GenerateUniqueResourceName("app");
            }

            if (!ValidationHelper.IsValidResourceName(app.resource_name))
            {
                return BadRequest("Resource name contains invalid characters. Use only letters, numbers, hyphens, and underscores.");
            }

            // Set creation datetime in ISO format (Requirement)
            app.creation_datetime = DateTime.Now;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    if (DatabaseHelper.ApplicationExists(conn, app.resource_name))
                    {
                        return Content(HttpStatusCode.Conflict,
                            new { 
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

                    // Prepare response with full properties (Requirement)
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
                    return InternalServerError(new Exception("An error occurred while creating the application. Please try again."));
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }
        }


        /// <summary>
        /// Retrieves a specific application resource by name
        /// </summary>
        /// <param name="name">The resource-name of the application</param>
        /// <returns>Application properties</returns>
        /// <response code="200">Application found and returned</response>
        /// <response code="404">Application not found</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     GET /api/somiod/smart-home
        ///     
        /// </remarks>
        [HttpGet]
        [Route("{name}")]
        public IHttpActionResult GetApplication(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("Application name is required");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    string query = "SELECT Id, Name, CreationDateTime FROM Applications WHERE Name = @Name";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = name;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var application = new
                                {
                                    id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    res_type = "application",
                                    resource_name = reader.GetString(reader.GetOrdinal("Name")),
                                    creation_datetime = reader.GetDateTime(reader.GetOrdinal("CreationDateTime"))
                                        .ToString("yyyy-MM-ddTHH:mm:ss")
                                };

                                return Ok(application);
                            }
                            else
                            {
                                return Content(HttpStatusCode.NotFound, 
                                    new { 
                                        error = $"Application '{name}' not found",
                                        res_type = "application"
                                    });
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    return InternalServerError(new Exception("An error occurred while retrieving the application."));
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }
        }


        /// <summary>
        /// Updates an existing application resource
        /// </summary>
        /// <param name="name">Current resource-name of the application</param>
        /// <param name="app">Application object with updated resource-name</param>
        /// <returns>Updated application properties</returns>
        /// <response code="200">Application updated successfully</response>
        /// <response code="404">Application not found</response>
        /// <response code="409">New name conflicts with existing application</response>
        /// <response code="400">Invalid input data</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     PUT /api/somiod/old-name
        ///     {
        ///        "resource_name": "new-name"
        ///     }
        ///     
        /// </remarks>
        [HttpPut]
        [Route("{name}")]
        public IHttpActionResult PutApplication(string name, [FromBody] Application app)
        {
            if (string.IsNullOrWhiteSpace(name))
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
                        checkCmd.Parameters.Add("@OldName", SqlDbType.NVarChar, 255).Value = name;

                        using (SqlDataReader reader = checkCmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new { 
                                        error = $"Application '{name}' not found",
                                        res_type = "application"
                                    });
                            }

                            appId = reader.GetInt32(reader.GetOrdinal("Id"));
                            creationDateTime = reader.GetDateTime(reader.GetOrdinal("CreationDateTime"));
                        }
                    }

                    if (!name.Equals(app.resource_name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (DatabaseHelper.ApplicationExists(conn, app.resource_name))
                        {
                            return Content(HttpStatusCode.Conflict,
                                new { 
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
                    return InternalServerError(new Exception("An error occurred while updating the application."));
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }
        }


        /// <summary>
        /// Deletes an application resource and all its child resources (containers, content-instances, subscriptions)
        /// </summary>
        /// <param name="name">The resource-name of the application to delete</param>
        /// <returns>Success confirmation or error</returns>
        /// <response code="200">Application deleted successfully</response>
        /// <response code="404">Application not found</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     DELETE /api/somiod/smart-home
        ///     
        /// Warning: This will CASCADE delete all child resources (containers, content-instances, subscriptions)
        /// </remarks>
        [HttpDelete]
        [Route("{name}")]
        public IHttpActionResult DeleteApplication(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
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
                        checkCmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = name;

                        using (SqlDataReader reader = checkCmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                return Content(HttpStatusCode.NotFound,
                                    new { 
                                        error = $"Application '{name}' not found",
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
                    return InternalServerError(new Exception("An error occurred while deleting the application."));
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }
        }
    }
}