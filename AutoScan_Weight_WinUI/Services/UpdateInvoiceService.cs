using AutoScan_Weight_WinUI.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;

namespace AutoScan_Weight_WinUI.Services
{
    public class UpdateInvoiceService
    {
        SqlDataReader? RS;
        string sqlstr = string.Empty;
        public void UpdateInvoice()
        {
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "UPDATE a " +
                           "SET a.InvoiceNo = b.InvoiceNo " +
                           "FROM YPT_Innovation.dbo.FG_PalletDetail a " +
                           "INNER JOIN Netgarment.dbo.Scan_Order_InoutStatus b " +
                           "ON a.BarcodeCarton = b.BarcodeCarton " +
                           "WHERE b.InvoiceNo IS NOT NULL;";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);

                    cmd.ExecuteNonQuery();


                }
            
            
          
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                connectSQL.Close_Sql();
            }



        }
    }
}
