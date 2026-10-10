using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Services
{
    public class ConnectSQL
    {
        public SqlConnection? conn = null;

        public bool Netgarment()
        {
            try
            {
                conn = new SqlConnection("Data Source=192.168.9.9;Initial Catalog=Netgarment;User ID=ypt;Password=ypt@dmin243; TrustServerCertificate=True");
                conn.Open();
                return true;
            }
            catch (Exception e)
            {
                // ใน WinUI ควรส่งข้อความ error กลับไปให้ ViewModel
                throw new Exception("Connection failed: " + e.Message);
            }
        }

        public bool YPTInnovation()
        {
            try
            {
                conn = new SqlConnection("Data Source=192.168.9.9;Initial Catalog=YPT_Innovation;User ID=ypt;Password=ypt@dmin243; TrustServerCertificate=True");
                conn.Open();
                return true;
            }
            catch (Exception e)
            {
                throw new Exception("Connection failed: " + e.Message);
            }
        }

        public bool TSDB_MAIN()
        {
            try
            {
                //conn = new SqlConnection("Data Source= 192.168.90.9; Initial Catalog=TSDB_Main; User ID=toi;  Password=toidb");
                conn = new SqlConnection("Data Source=192.168.90.9; Initial Catalog=TSDB_Main; User ID=toi; Password=toidb; TrustServerCertificate=True");
                conn.Open();
                return true;
            }
            catch (Exception e)
            {
                throw new Exception("Connection failed: " + e.Message);
            }
        }

        public void Close_Sql()
        {
            conn?.Close();
            conn?.Dispose();
        }
    }
}
