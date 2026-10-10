using AutoScan_Weight_WinUI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Services
{
    public class QueryPallet
    {
        SqlDataReader? RS;
        string sqlstr = string.Empty;

        public List<PalletDetailModel> GetPalletDetails(string pallet)
        {

            List<PalletDetailModel> result = new List<PalletDetailModel>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    sqlstr = @"With cte_Master as 
                                (
                                Select a.PalletDetailID ,a.PalletNo ,a.DocNo ,b.StyleID ,b.OrderNo ,b.Style ,b.CustPORef ,b.LotRef ,b.FinalDest
                                ,a.Warehouse ,a.BarcodeCarton ,a.ScanCarton ,a.InvoiceNo ,a.Customer ,a.SeqInPallet ,a.PlaceStatus ,a.CreateDate
                                From FG_PalletDetail a 
                                Left join FG_PalletDetail_Order b on  a.BarcodeCarton = b.BarcodeCarton 
                                Where PalletNo = @PalletNo and a.InvoiceNo is null and a.CancelledDate is  null 
                                ),
                                cte_Qty as 
                                (
                                Select a.PalletDetailID ,a.PalletNo ,a.DocNo ,a.StyleID ,a.OrderNo ,a.Style ,a.CustPORef ,a.LotRef ,a.FinalDest
                                ,a.Warehouse ,a.BarcodeCarton ,a.ScanCarton ,a.InvoiceNo ,a.Customer ,a.SeqInPallet ,a.PlaceStatus ,a.CreateDate,
                                Sum(b.Qty) as Qty
                                From cte_Master a
                                Left join Netgarment.dbo.Packing_ScanOrder b On a.OrderNo = b.OrderNo and a.FinalDest = b.FinalDest and a.ScanCarton = b.Carton and a.Style = b.Style
                                Left join Netgarment.dbo.Packing_ScanDoc c On b.PackNo = c.PackNo and a.DocNo = c.PackingList
                                Group by a.PalletDetailID ,a.PalletNo ,a.DocNo ,a.StyleID ,a.OrderNo ,a.Style ,a.CustPORef ,a.LotRef ,a.FinalDest
                                ,a.Warehouse ,a.BarcodeCarton ,a.ScanCarton ,a.InvoiceNo ,a.Customer ,a.SeqInPallet ,a.PlaceStatus ,a.CreateDate
                                )
                                Select * From cte_Qty Order by BarcodeCarton asc";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@PalletNo", SqlDbType.NVarChar, 50).Value = pallet;


                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new PalletDetailModel
                        {
                            PalletID = RS["PalletDetailID"].ToString() ?? string.Empty,
                            PalletNo = RS["PalletNo"].ToString() ?? string.Empty,
                            DocNo = RS["DocNo"].ToString() ?? string.Empty,
                            StyleID = RS["StyleID"].ToString() ?? string.Empty,
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            Style = RS["Style"].ToString() ?? string.Empty,
                            CustPORef = RS["CustPORef"].ToString() ?? string.Empty,
                            LotRef = RS["LotRef"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Warehouse = RS["Warehouse"].ToString() ?? string.Empty,
                            BarcodeCarton = RS["BarcodeCarton"].ToString() ?? string.Empty,
                            Scancarton = RS["Scancarton"].ToString() ?? string.Empty,
                            InvoiceNo = RS["InvoiceNo"].ToString() ?? string.Empty,
                            Customer = RS["Customer"].ToString() ?? string.Empty,
                            SeqlnPallet = RS["SeqInPallet"].ToString() ?? string.Empty,
                            PlaceStatus = RS["PlaceStatus"].ToString() ?? string.Empty,
                            CreateDate = RS["CreateDate"] != DBNull.Value ? Convert.ToDateTime(RS["CreateDate"]) : DateTime.MinValue,
                            Qty = RS["Qty"].ToString() ?? string.Empty
                        });
                    }

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
            return result;


        }

        public List<PalletDetailModel> GetOrderNOAll()
        {

            List<PalletDetailModel> result = new List<PalletDetailModel>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    sqlstr = "select a.PalletDetailID , a.PalletNo, a.DocNo ,b.StyleID ,b.OrderNo ,b.Style , b.CustPORef , b.LotRef , b.FinalDest , " +
                        "a.Warehouse , a.BarcodeCarton ,a.ScanCarton ,a.InvoiceNo ,a.Customer ,a.SeqInPallet ,a.PlaceStatus ,a.CreateDate " +
                        "from FG_PalletDetail a " +
                        "left join FG_PalletDetail_Order b on  a.BarcodeCarton = b.BarcodeCarton " +
                        "where a.InvoiceNo is null and a.CancelledDate is null";


                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);



                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new PalletDetailModel
                        {
                            PalletID = RS["PalletDetailID"].ToString() ?? string.Empty,
                            PalletNo = RS["PalletNo"].ToString() ?? string.Empty,
                            DocNo = RS["DocNo"].ToString() ?? string.Empty,
                            StyleID = RS["StyleID"].ToString() ?? string.Empty,
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            Style = RS["Style"].ToString() ?? string.Empty,
                            CustPORef = RS["CustPORef"].ToString() ?? string.Empty,
                            LotRef = RS["LotRef"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Warehouse = RS["Warehouse"].ToString() ?? string.Empty,
                            BarcodeCarton = RS["BarcodeCarton"].ToString() ?? string.Empty,
                            Scancarton = RS["Scancarton"].ToString() ?? string.Empty,
                            InvoiceNo = RS["InvoiceNo"].ToString() ?? string.Empty,
                            Customer = RS["Customer"].ToString() ?? string.Empty,
                            SeqlnPallet = RS["SeqInPallet"].ToString() ?? string.Empty,
                            PlaceStatus = RS["PlaceStatus"].ToString() ?? string.Empty,
                            CreateDate = RS["CreateDate"] != DBNull.Value ? Convert.ToDateTime(RS["CreateDate"]) : DateTime.MinValue

                        });
                    }

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
            return result;


        }

        public List<PalletAndQTY> GetPalletDetailsAndQTY(string pallet)
        {
            List<PalletAndQTY> result = new List<PalletAndQTY>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    sqlstr = "Select a.PalletDetailID , a.PalletNo, a.DocNo ,b.StyleID ,b.OrderNo ,b.Style , b.CustPORef , b.LotRef , b.FinalDest , a.Warehouse , a.BarcodeCarton ,a.ScanCarton ,a.InvoiceNo ,a.Customer ,a.SeqInPallet ,a.PlaceStatus ,a.CreateDate, Sum(c.Qty) as Qty " +
                        "From YPT_Innovation.dbo.FG_PalletDetail a  " +
                        "Left join YPT_Innovation.dbo.FG_PalletDetail_Order b on  a.BarcodeCarton = b.BarcodeCarton  " +
                        "Left join Netgarment.dbo.Packing_ScanOrder c on b.OrderNo = c.OrderNo and a.ScanCarton = c.Carton and b.FinalDest = c.FinalDest and b.Style = c.Style " +
                        "Where PalletNo = @PalletNo and InvoiceNo is null " +
                        "Group by a.PalletDetailID , a.PalletNo, a.DocNo ,b.StyleID ,b.OrderNo ,b.Style , b.CustPORef , b.LotRef , b.FinalDest , a.Warehouse , a.BarcodeCarton ,a.ScanCarton ,a.InvoiceNo ,a.Customer ,a.SeqInPallet ,a.PlaceStatus ,a.CreateDate";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@PalletNo", SqlDbType.NVarChar, 50).Value = pallet;


                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new PalletAndQTY
                        {
                            PalletID = RS["PalletDetailID"].ToString() ?? string.Empty,
                            PalletNo = RS["PalletNo"].ToString() ?? string.Empty,
                            DocNo = RS["DocNo"].ToString() ?? string.Empty,
                            StyleID = RS["StyleID"].ToString() ?? string.Empty,
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            Style = RS["Style"].ToString() ?? string.Empty,
                            CustPORef = RS["CustPORef"].ToString() ?? string.Empty,
                            LotRef = RS["LotRef"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Warehouse = RS["Warehouse"].ToString() ?? string.Empty,
                            BarcodeCarton = RS["BarcodeCarton"].ToString() ?? string.Empty,
                            Scancarton = RS["Scancarton"].ToString() ?? string.Empty,
                            InvoiceNo = RS["InvoiceNo"].ToString() ?? string.Empty,
                            Customer = RS["Customer"].ToString() ?? string.Empty,
                            SeqlnPallet = RS["SeqInPallet"].ToString() ?? string.Empty,
                            PlaceStatus = RS["PlaceStatus"].ToString() ?? string.Empty,
                            CreateDate = RS["CreateDate"] != DBNull.Value ? Convert.ToDateTime(RS["CreateDate"]) : DateTime.MinValue,
                            QTY = RS["Qty"].ToString() ?? string.Empty

                        });
                    }

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
            return result;


        }
        public List<PalletDetailModel> ShowPalletDetails(string PackNo)
        {
            List<PalletDetailModel> result = new List<PalletDetailModel>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    sqlstr = @"SELECT *
                            from FG_PalletDetail
                            where DocNo = @PalletNo Order by PalletNo ASC";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@PalletNo", SqlDbType.NVarChar, 50).Value = PackNo;


                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new PalletDetailModel
                        {
                            PalletID = RS["PalletDetailID"].ToString() ?? string.Empty,
                            PalletNo = RS["PalletNo"].ToString() ?? string.Empty,
                            DocNo = RS["DocNo"].ToString() ?? string.Empty,
                            Warehouse = RS["Warehouse"].ToString() ?? string.Empty,
                            BarcodeCarton = RS["BarcodeCarton"].ToString() ?? string.Empty,
                            Scancarton = RS["Scancarton"].ToString() ?? string.Empty,
                            InvoiceNo = RS["InvoiceNo"].ToString() ?? string.Empty,
                            Customer = RS["Customer"].ToString() ?? string.Empty,
                            PlaceStatus = RS["PlaceStatus"].ToString() ?? string.Empty,
                            CreateDate = RS["CreateDate"] != DBNull.Value ? Convert.ToDateTime(RS["CreateDate"]) : DateTime.MinValue,

                        });
                    }

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
            return result;

        }

        public List<PalletDetailModel> Orderwithdraw()
        {
            List<PalletDetailModel> result = new List<PalletDetailModel>();
            ConnectSQL connectSQL = new ConnectSQL();
            try
            {
                if (connectSQL.YPTInnovation())
                {
                    sqlstr = @"SELECT a.* , b.ScanCarton , b.DocNo
                            from FG_PalletDetail_Order  a  
                            left join FG_PalletDetail b on a.BarcodeCarton = b.BarcodeCarton
                            where b.InvoiceNo is null and b.CancelledDate is null
                            Order by a.OrderNo ASC ";
                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);

                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new PalletDetailModel
                        {
                            BarcodeCarton = RS["BarcodeCarton"].ToString() ?? string.Empty,
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            DocNo = RS["DocNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Scancarton = RS["ScanCarton"].ToString() ?? string.Empty,


                        });
                    }



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
            return result;
        }
    }
}
          
