using System;
using System.Data;
using System.Data.SqlClient;

namespace SOMIOD.Helpers
{
    /// <summary>
    /// Database helper class for SOMIOD API
    /// Contains shared database query methods used across controllers
    /// </summary>
    public static class DatabaseHelper
    {
        /// <summary>
        /// Gets application ID by name
        /// </summary>
        /// <param name="conn">Open SQL connection</param>
        /// <param name="name">Application name</param>
        /// <returns>Application ID or -1 if not found</returns>
        public static int GetApplicationId(SqlConnection conn, string name)
        {
            string query = "SELECT Id FROM Applications WHERE Name = @Name";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = name;
                object result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : -1;
            }
        }

        /// <summary>
        /// Checks if an application with the given name exists
        /// </summary>
        /// <param name="conn">Open SQL connection</param>
        /// <param name="name">Application name to check</param>
        /// <returns>True if exists, false otherwise</returns>
        public static bool ApplicationExists(SqlConnection conn, string name)
        {
            string checkQuery = "SELECT COUNT(*) FROM Applications WHERE Name = @Name";
            using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
            {
                checkCmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = name;
                int count = (int)checkCmd.ExecuteScalar();
                return count > 0;
            }
        }

        /// <summary>
        /// Gets container ID by name and parent application ID
        /// </summary>
        /// <param name="conn">Open SQL connection</param>
        /// <param name="name">Container name</param>
        /// <param name="parentId">Parent application ID</param>
        /// <returns>Container ID or -1 if not found</returns>
        public static int GetContainerId(SqlConnection conn, string name, int parentId)
        {
            string query = "SELECT Id FROM Containers WHERE Name = @Name AND ParentId = @ParentId";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = name;
                cmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentId;
                object result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : -1;
            }
        }

        /// <summary>
        /// Checks if a container with the given name exists under a specific application
        /// </summary>
        /// <param name="conn">Open SQL connection</param>
        /// <param name="name">Container name to check</param>
        /// <param name="parentId">Parent application ID</param>
        /// <returns>True if exists, false otherwise</returns>
        public static bool ContainerExists(SqlConnection conn, string name, int parentId)
        {
            string checkQuery = "SELECT COUNT(*) FROM Containers WHERE Name = @Name AND ParentId = @ParentId";
            using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
            {
                checkCmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = name;
                checkCmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentId;
                int count = (int)checkCmd.ExecuteScalar();
                return count > 0;
            }
        }

        /// <summary>
        /// Gets content instance ID by name and parent container ID
        /// </summary>
        /// <param name="conn">Open SQL connection</param>
        /// <param name="name">Content instance name</param>
        /// <param name="parentId">Parent container ID</param>
        /// <returns>Content instance ID or -1 if not found</returns>
        public static int GetContentInstanceId(SqlConnection conn, string name, int parentId)
        {
            string query = "SELECT Id FROM ContentInstances WHERE Name = @Name AND ParentId = @ParentId";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = name;
                cmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentId;
                object result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : -1;
            }
        }

        /// <summary>
        /// Gets subscription ID by name and parent container ID
        /// </summary>
        /// <param name="conn">Open SQL connection</param>
        /// <param name="name">Subscription name</param>
        /// <param name="parentId">Parent container ID</param>
        /// <returns>Subscription ID or -1 if not found</returns>
        public static int GetSubscriptionId(SqlConnection conn, string name, int parentId)
        {
            string query = "SELECT Id FROM Subscriptions WHERE Name = @Name AND ParentId = @ParentId";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = name;
                cmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentId;
                object result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : -1;
            }
        }

        /// <summary>
        /// Checks if a subscription with the given name exists under a specific container
        /// </summary>
        /// <param name="conn">Open SQL connection</param>
        /// <param name="name">Subscription name to check</param>
        /// <param name="parentId">Parent container ID</param>
        /// <returns>True if exists, false otherwise</returns>
        public static bool SubscriptionExists(SqlConnection conn, string name, int parentId)
        {
            string checkQuery = "SELECT COUNT(*) FROM Subscriptions WHERE Name = @Name AND ParentId = @ParentId";
            using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
            {
                checkCmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = name;
                checkCmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentId;
                int count = (int)checkCmd.ExecuteScalar();
                return count > 0;
            }
        }

        /// <summary>
        /// Checks if a content instance with the given name exists under a specific container
        /// </summary>
        /// <param name="conn">Open SQL connection</param>
        /// <param name="name">Content instance name to check</param>
        /// <param name="parentId">Parent container ID</param>
        /// <returns>True if exists, false otherwise</returns>
        public static bool ContentInstanceExists(SqlConnection conn, string name, int parentId)
        {
            string checkQuery = "SELECT COUNT(*) FROM ContentInstances WHERE Name = @Name AND ParentId = @ParentId";
            using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
            {
                checkCmd.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = name;
                checkCmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentId;
                int count = (int)checkCmd.ExecuteScalar();
                return count > 0;
            }
        }
    }
}
