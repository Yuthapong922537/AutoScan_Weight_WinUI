using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Services
{
    public class UserService
    {
        string sqlstr = string.Empty;
        public Models.User? GetUser(string username)
        {
            Models.User? result = null;
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())

                {
                    sqlstr = "Select YPTUser , YPTName , UserGroup ,Factory " +
                           "From [Netgarment].[dbo].[YPTUser] " +
                           "Where YPTUser = @user ";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.AddWithValue("@user", username);

                    SqlDataReader reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        result = new Models.User
                        {
                            YPTUser = reader["YPTUser"]?.ToString(),
                            YPTName = reader["YPTName"]?.ToString(),
                            UserGroup = reader["UserGroup"]?.ToString(),
                            Factory = reader["Factory"]?.ToString()
                        };

                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error connecting to database: " + ex.Message);
                return null;
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return result;

        }
    }
}
