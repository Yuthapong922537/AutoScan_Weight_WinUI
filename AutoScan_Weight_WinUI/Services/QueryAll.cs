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
    public class QueryAll
    {
        SqlDataReader? RS;
        string sqlstr = string.Empty;


        public List<DataCarton> CheckRowInQA(string barcode)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "Select Distinct a.OrderNo, a.PackNo, a.FinalDest, a.Carton, b.BarcodeCarton " +
                            "From Packing_ScanCarton a " +
                            "left join [YPT_Innovation].dbo.[QA_ScanCarton] b on a.BarcodeCarton = b.BarcodeCarton " +
                            "where a.BarcodeCarton = @barcode ";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@barcode", SqlDbType.NVarChar, 11).Value = barcode;
                    RS = cmd.ExecuteReader();

                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            BarcodeCarton = RS["BarcodeCarton"].ToString() ?? string.Empty
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

        public List<DataCarton> GetDataCarton(string barcode)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = @"with cteLotref (LotRef,OrderNo,FinalDest) as 
                                (select DISTINCT shpcvshp.LotRef,shpcvshp.OrderNo,a.PkExtStyleDesc1 
                                from shpstyle a 
                                left Join shpcvshp On a.docno = shpcvshp.DocNo And a.StyleID = shpcvshp.StyleID 
                                left join Scan_Order_Out_New d on shpcvshp.DocNo = d.PackNo and shpcvshp.OrderNo = d.OrderNo and a.PkExtStyleDesc1 = d.FinalDest 
                                 Where d.BarcodeCarton = @barcode),
                                 cte_weight as 
                                (
                                select distinct a.OrderNo,a.PackNo,a.FinalDest,a.ScanCarton,isnull(b.Weight,0) as Weight,isnull (nullif (b.KeepingArea,''),'รอเก็บ') as KeepingArea ,c.LotRef 
                                from Scan_Order_Out_New a 
                                left join Scan_order_Weight  b on a.OrderNo = b.OrderNo and a.PackNo = b.PackNo and a.FinalDest = b.FinalDest and a.ScanCarton = b.ScanCarton 
                                left join cteLotref c on a.OrderNo = c.OrderNo and a.FinalDest = c.FinalDest 
                                where a.BarcodeCarton = @barcode
                                )
                                select a.* ,b.InvoiceNo
                                from cte_weight a
                                left join Scan_Order_InoutStatus b on a.OrderNo = b.OrderNo and a.PackNo = b.PackNo and a.FinalDest = b.FinalDest and a.ScanCarton = b.ScanCarton";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@barcode", SqlDbType.NVarChar, 11).Value = barcode;
                    RS = cmd.ExecuteReader();

                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Carton = RS["ScanCarton"].ToString() ?? string.Empty,
                            Weight = RS["Weight"].ToString() ?? string.Empty,
                            KeepingArea = RS["KeepingArea"].ToString() ?? string.Empty,
                            LotRef = RS["LotRef"].ToString() ?? string.Empty,
                            InvoiceNo = RS["InvoiceNo"].ToString() ?? string.Empty
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

        public List<DataCarton> GetDataDetail(string barcode)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "with cteLotref (LotRef,OrderNo,FinalDest) as " +
                            "(select DISTINCT shpcvshp.LotRef,shpcvshp.OrderNo,a.PkExtStyleDesc1 " +
                            "from shpstyle a left Join shpcvshp On a.docno = shpcvshp.DocNo And a.StyleID = shpcvshp.StyleID " +
                            "left join Scan_Order_Out_New d on shpcvshp.DocNo = d.PackNo and shpcvshp.OrderNo = d.OrderNo and a.PkExtStyleDesc1 = d.FinalDest " +
                            "Where d.BarcodeCarton = @barcode ) " +
                            "select distinct a.OrderNo,a.PackNo,a.FinalDest,a.ScanCarton,a.Size,a.Color,a.Qty,isnull(b.Weight,0) as Weight,isnull (nullif (b.KeepingArea,''),'รอเก็บ') as KeepingArea ,c.LotRef " +
                            "from Scan_Order_Out_New a left join Scan_order_Weight  b on a.OrderNo = b.OrderNo and a.PackNo = b.PackNo and a.FinalDest = b.FinalDest and a.ScanCarton = b.ScanCarton " +
                            "left join cteLotref c on a.OrderNo = c.OrderNo and a.FinalDest = c.FinalDest " +
                            "where a.BarcodeCarton = @barcode ";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@barcode", SqlDbType.NVarChar, 11).Value = barcode;
                    RS = cmd.ExecuteReader();

                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Carton = RS["ScanCarton"].ToString() ?? string.Empty,
                            Weight = RS["Weight"].ToString() ?? string.Empty,
                            KeepingArea = RS["KeepingArea"].ToString() ?? string.Empty,
                            Size = RS["Size"].ToString() ?? string.Empty,
                            Color = RS["Color"].ToString() ?? string.Empty,
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

        public List<DataCarton> GetPackOf(string barcode)
        {

            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "SELECT * FROM Packing_ScanBarcodeCarton  " +
                        "where BarcodeCarton = @barcode";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@barcode", SqlDbType.NVarChar, 11).Value = barcode;
                    RS = cmd.ExecuteReader();

                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            PackNo = RS["PackNo"].ToString() ?? string.Empty
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
        public List<DataCarton> GetrowOfAll(string PackNo)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "select * from Packing_ScanBarcodeCarton  " +
                        "where PackNo = @PackNo";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 11).Value = PackNo;
                    RS = cmd.ExecuteReader();



                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            PackNo = RS["PackNo"].ToString() ?? string.Empty
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

        public List<DataCarton> GetCartonInstore(string orderNo, string packNo, string finalDest)
        {

            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "Select distinct OrderNo , PackNo , FinalDest , ScanCarton , Weight " +
                             "from Scan_order_Weight " +
                             "where OrderNo = @OrderNo and PackNo = @PackNo and FinalDest = @FinalDest and Weight <> '0' ";


                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 14).Value = orderNo;
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 20).Value = packNo;
                    cmd.Parameters.Add("@FinalDest", SqlDbType.NVarChar, 60).Value = finalDest;
                    RS = cmd.ExecuteReader();

                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
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

        public List<DataCarton> UpdateArea(string orderNo, string packNo, string finalDest, string scanCarton, string keepingArea)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();
            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "Update Scan_order_Weight set KeepingArea = @KeepingArea " +
                             "where OrderNo = @OrderNo and PackNo = @PackNo and FinalDest = @FinalDest and ScanCarton = @ScanCarton";
                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 14).Value = orderNo;
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 20).Value = packNo;
                    cmd.Parameters.Add("@FinalDest", SqlDbType.NVarChar, 60).Value = finalDest;
                    cmd.Parameters.Add("@ScanCarton", SqlDbType.NVarChar, 10).Value = scanCarton;
                    cmd.Parameters.Add("@KeepingArea", SqlDbType.NVarChar, 20).Value = keepingArea;
                    RS = cmd.ExecuteReader();
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

        public List<DataCarton> CheckWeight(string orderNo, string packNo, string finalDest, string Carton)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();
            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "Select * from Scan_order_Weight " +
                             "where OrderNo = @OrderNo and PackNo = @PackNo and FinalDest = @FinalDest and ScanCarton = @Carton ";
                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 14).Value = orderNo;
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 20).Value = packNo;
                    cmd.Parameters.Add("@FinalDest", SqlDbType.NVarChar, 60).Value = finalDest;
                    cmd.Parameters.Add("@Carton", SqlDbType.NVarChar, 10).Value = Carton;
                    
                    RS = cmd.ExecuteReader();

                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Carton = RS["ScanCarton"].ToString() ?? string.Empty,
                            Weight = RS["Weight"].ToString() ?? string.Empty,
                            KeepingArea = RS["KeepingArea"].ToString() ?? string.Empty
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

        public List<DataCarton> InsertWeight(string orderNo, string packNo, string finalDest, string carton, string weight, string keepingArea)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();
            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "Insert into Scan_order_Weight (OrderNo, PackNo, FinalDest, ScanCarton, Weight,ScanOUTDate,ScanTimeDate,KeepingArea) " +
                        "values (@OrderNo, @PackNo, @FinalDest, @ScanCarton, @Weight, format(getdate(),'yyyy-MM-dd'), cast(format(getdate(),'HH:mm:ss') as nvarchar ), @KeepingArea)";
                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 14).Value = orderNo;
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 20).Value = packNo;
                    cmd.Parameters.Add("@FinalDest", SqlDbType.NVarChar, 60).Value = finalDest;
                    cmd.Parameters.Add("@ScanCarton", SqlDbType.NVarChar, 10).Value = carton;
                    cmd.Parameters.Add("@Weight", SqlDbType.NVarChar, 20).Value = weight;
                    //cmd.Parameters.Add("@ScanOUTDate", SqlDbType.DateTime).Value = DateTime.Now;
                    //cmd.Parameters.Add("@ScanTimeDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@KeepingArea", SqlDbType.NVarChar, 20).Value = keepingArea;
                    RS = cmd.ExecuteReader();
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

        public List<DataCarton> UpdateWeight(string orderNo, string packNo, string finalDest, string carton, string weight, string keepingArea)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();
            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "Update Scan_order_Weight set Weight = @Weight " +
                        "where OrderNo = @OrderNo and PackNo = @PackNo and FinalDest = @FinalDest and ScanCarton = @ScanCarton";
                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 14).Value = orderNo;
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 20).Value = packNo;
                    cmd.Parameters.Add("@FinalDest", SqlDbType.NVarChar, 60).Value = finalDest;
                    cmd.Parameters.Add("@ScanCarton", SqlDbType.NVarChar, 10).Value = carton;
                    cmd.Parameters.Add("@Weight", SqlDbType.NVarChar, 20).Value = weight;
                    
                    RS = cmd.ExecuteReader();
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

        public List<DataCarton> GetDairy()
        { 
        List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            string Date_now = DateTime.Now.ToString("yyyy-MM-dd");

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "SELECT * FROM [Netgarment].[dbo].[Scan_order_Weight] "+
                        "where ScanOUTDate between '"+ Date_now + " 00:00:00.000' and '" + Date_now + " 23:59:59.999' order by ScanTimeDate ASC";
                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Carton = RS["ScanCarton"].ToString() ?? string.Empty,
                            Weight = RS["Weight"].ToString() ?? string.Empty,
                            KeepingArea = RS["KeepingArea"].ToString() ?? string.Empty
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
            } return result;
        }

        public List<DataCarton> GetOrderNo()
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();
            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "select DISTINCT OrderNo , PackNo , FinalDest , ScanCarton " +
                            "from Scan_order_InoutStatus " +
                            "Where OrderNo Like 'YP%' order by OrderNo DESC";
                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    
                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Carton = RS["ScanCarton"].ToString() ?? string.Empty
                            
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

        public List<DataCarton> GetFinal_Select(string OrderNo , string PackNo , string FinalDest)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "with cteLotref (LotRef,OrderNo,FinalDest) as " +
                        "(select DISTINCT shpcvshp.LotRef,shpcvshp.OrderNo,a.PkExtStyleDesc1 " +
                        "from shpstyle a left Join shpcvshp On a.docno = shpcvshp.DocNo And a.StyleID = shpcvshp.StyleID " +
                        "left join Scan_Order_Out_New d on shpcvshp.DocNo = d.PackNo and shpcvshp.OrderNo = d.OrderNo " +
                        "and a.PkExtStyleDesc1 = d.FinalDest Where d.PackNo = @PackNo) " +
                        "select distinct a.OrderNo,a.PackNo,a.FinalDest,a.ScanCarton ,isnull(b.Weight,0) as Weight " +
                        ",isnull (nullif (b.KeepingArea,''),'รอเก็บ') as KeepingArea ,c.LotRef " +
                        "from Scan_Order_Out_New a left join Scan_order_Weight  b on a.OrderNo = b.OrderNo and a.PackNo = b.PackNo " +
                        "and a.FinalDest = b.FinalDest and a.ScanCarton = b.ScanCarton " +
                        "left join cteLotref c on a.OrderNo = c.OrderNo and a.FinalDest = c.FinalDest " +
                        "where  a.OrderNo = @OrderNo and a.packNo = @PackNo and a.FinalDest = @FinalDest order by ScanCarton ";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 14).Value = OrderNo;
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 20).Value = PackNo;
                    cmd.Parameters.Add("@FinalDest", SqlDbType.NVarChar, 60).Value = FinalDest;

                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Carton = RS["ScanCarton"].ToString() ?? string.Empty,
                            Weight = RS["Weight"].ToString() ?? string.Empty,
                            KeepingArea = RS["KeepingArea"].ToString() ?? string.Empty,
                            LotRef = RS["LotRef"].ToString() ??string.Empty

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

        public List<DataCarton> GetCarton_Select(string OrderNo, string PackNo, string FinalDest, string Cartons)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = @"with cteLotref (LotRef,OrderNo,FinalDest) as 
                                (
                                select DISTINCT shpcvshp.LotRef,shpcvshp.OrderNo,a.PkExtStyleDesc1 
                                from shpstyle a 
                                left Join shpcvshp On a.docno = shpcvshp.DocNo And a.StyleID = shpcvshp.StyleID 
                                left join Scan_Order_Out_New d on shpcvshp.DocNo = d.PackNo and shpcvshp.OrderNo = d.OrderNo 
                                and a.PkExtStyleDesc1 = d.FinalDest 
                                Where d.PackNo = @PackNo
                                ),
                                cte_weight as 
                                (
                                select distinct a.OrderNo,a.PackNo,a.FinalDest,a.ScanCarton,isnull(b.Weight,0) as Weight ,isnull (nullif (b.KeepingArea,''),'รอเก็บ') as KeepingArea ,c.LotRef 
                                from Scan_Order_Out_New a 
                                left join Scan_order_Weight  b on a.OrderNo = b.OrderNo and a.PackNo = b.PackNo and a.FinalDest = b.FinalDest and a.ScanCarton = b.ScanCarton 
                                left join cteLotref c on a.OrderNo = c.OrderNo and a.FinalDest = c.FinalDest 
                                where  a.OrderNo = @OrderNo and a.packNo = @PackNo and a.FinalDest = @FinalDest and a.Scancarton = @Cartons
                                )
                                select a.* ,b.InvoiceNo
                                from cte_weight a
                                left join Scan_Order_InoutStatus b on a.OrderNo = b.OrderNo and a.PackNo = b.PackNo and a.FinalDest = b.FinalDest and a.ScanCarton = b.ScanCarton";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 14).Value = OrderNo;
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 20).Value = PackNo;
                    cmd.Parameters.Add("@FinalDest", SqlDbType.NVarChar, 60).Value = FinalDest;
                    cmd.Parameters.Add("@Cartons" , SqlDbType.NVarChar, 10).Value = Cartons;

                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Carton = RS["ScanCarton"].ToString() ?? string.Empty,
                            Weight = RS["Weight"].ToString() ?? string.Empty,
                            KeepingArea = RS["KeepingArea"].ToString() ?? string.Empty,
                            LotRef = RS["LotRef"].ToString() ?? string.Empty,
                            InvoiceNo = RS["InvoiceNo"].ToString() ?? string.Empty

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

        public List<DataCarton> GetDataDetail_Select(string OrderNo, string PackNo, string FinalDest, string Carton)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "with cteLotref (LotRef,OrderNo,FinalDest) as " +
                        "(select DISTINCT shpcvshp.LotRef,shpcvshp.OrderNo,a.PkExtStyleDesc1 " +
                        "from shpstyle a " +
                        "left Join shpcvshp On a.docno = shpcvshp.DocNo And a.StyleID = shpcvshp.StyleID " +
                        "left join Scan_Order_Out_New d on shpcvshp.DocNo = d.PackNo and shpcvshp.OrderNo = d.OrderNo and a.PkExtStyleDesc1 = d.FinalDest " +
                        "Where d.BarcodeCarton = '') " +
                        "select distinct a.OrderNo,a.PackNo,a.FinalDest,a.ScanCarton,a.Size,a.Color,a.Qty,isnull(b.Weight,0) as Weight,isnull (nullif (b.KeepingArea,''),'รอเก็บ') as KeepingArea ,c.LotRef " +
                        "from Scan_Order_Out_New a " +
                        "left join Scan_order_Weight  b on a.OrderNo = b.OrderNo and a.PackNo = b.PackNo and a.FinalDest = b.FinalDest and a.ScanCarton = b.ScanCarton " +
                        "left join cteLotref c on a.OrderNo = c.OrderNo and a.FinalDest = c.FinalDest " +
                        "where a.OrderNo = @OrderNo and a.PackNo = @PackNo and a.FinalDest = @FinalDest and a.Scancarton = @Carton ";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 14).Value = OrderNo;
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 20).Value = PackNo;
                    cmd.Parameters.Add("@FinalDest", SqlDbType.NVarChar, 60).Value = FinalDest;
                    cmd.Parameters.Add("@Carton", SqlDbType.NVarChar, 10).Value = Carton;

                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Carton = RS["ScanCarton"].ToString() ?? string.Empty,
                            Size = RS["Size"].ToString() ?? string.Empty,
                            Color = RS["Color"].ToString() ?? string.Empty,
                            Qty = RS["Qty"].ToString() ?? string.Empty,
                            Weight = RS["Weight"].ToString() ?? string.Empty,
                            KeepingArea = RS["KeepingArea"].ToString() ?? string.Empty
                            
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

        public List<DataCarton> GetAlrady_Select(string OrderNo, string PackNo, string FinalDest)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "Select distinct OrderNo , PackNo , FinalDest , ScanCarton , Weight " +
                            "from Scan_order_Weight " +
                            "where OrderNo = @OrderNo and PackNo = @PackNo and FinalDest = @FinalDest and Weight <> '0' ";


                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 14).Value = OrderNo;
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 20).Value = PackNo;
                    cmd.Parameters.Add("@FinalDest", SqlDbType.NVarChar, 60).Value = FinalDest;

                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Carton = RS["ScanCarton"].ToString() ?? string.Empty,                           
                            Weight = RS["Weight"].ToString() ?? string.Empty
                            

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

        public List<DataCarton> GetBarcode_Select(string OrderNo, string PackNo, string FinalDest, string Carton)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "select a.BarcodeCarton , b.PackNo , a.Carton , (Select count(*) from Packing_ScanBarcodeCarton c where c.PackNo = b.PackNo) as Qty " +
                            "from Packing_ScanCarton a " +
                            "left join Packing_ScanBarcodeCarton b on a.BarcodeCarton = b.BarcodeCarton " +
                            "where a.OrderNo = @OrderNo and a.PackNo = @PackNo and a.FinalDest = @FinalDest and a.Carton = @Carton";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 14).Value = OrderNo;
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 20).Value = PackNo;
                    cmd.Parameters.Add("@FinalDest", SqlDbType.NVarChar, 60).Value = FinalDest;
                    cmd.Parameters.Add("@Carton", SqlDbType.NVarChar, 10).Value = Carton;

                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            BarcodeCarton = RS["BarcodeCarton"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            Qty = RS["Qty"].ToString() ?? string.Empty,
                            Carton = RS["Carton"].ToString() ?? string.Empty

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

        public List<DataCarton> GetCarton_Delete(string order, string pack, string final, string carton)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "select DISTINCT OrderNo , PackNo , FinalDest , ScanCarton , Weight , KeepingArea " +
                            "from Scan_order_Weight " +
                            "where  OrderNo = @OrderNo and packNo = @PackNo and FinalDest = @FinalDest and ScanCarton = @carton ";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 14).Value = order;
                    cmd.Parameters.Add("@PackNo", SqlDbType.NVarChar, 20).Value = pack;
                    cmd.Parameters.Add("@FinalDest", SqlDbType.NVarChar, 60).Value = final;
                    cmd.Parameters.Add("@carton" , SqlDbType.NVarChar,10).Value = carton;

                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                            PackNo = RS["PackNo"].ToString() ?? string.Empty,
                            FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                            Carton = RS["ScanCarton"].ToString() ?? string.Empty,
                            Weight = RS["Weight"].ToString() ?? string.Empty,
                            KeepingArea = RS["KeepingArea"].ToString() ?? string.Empty


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

        public List<DataCarton> Delete_carton(string order , string pack , string final , string carton)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try 
            {
                if (connectSQL.Netgarment())
                {
                    sqlstr = "update Scan_order_Weight " +
                        "SET Weight = '0' , KeepingArea = NULL " +
                        "Where OrderNo = @order and PackNo = @pack and FinalDest = @final and ScanCarton = @carton ";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@order", SqlDbType.NVarChar, 14).Value = order;
                    cmd.Parameters.Add("@pack", SqlDbType.NVarChar, 20).Value = pack;
                    cmd.Parameters.Add("@final", SqlDbType.NVarChar, 60).Value = final;
                    cmd.Parameters.Add("@carton", SqlDbType.NVarChar, 10).Value = carton;
                    

                    RS = cmd.ExecuteReader();
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

        public List<DataCarton> getPalletNo(string barcode)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    sqlstr = "select a.PalletNo , c.AreaName " +
                        "from FG_PalletDetail a " +
                        "left join FG_Pallet b on a.PalletNo = b.PalletNo " +
                        "left join FG_Warehouse_Area c on b.AreaNo = c.AreaNo " +
                        "where a.BarcodeCarton = @barcode";


                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@barcode", SqlDbType.NVarChar, 50).Value = barcode;

                    RS = cmd.ExecuteReader();
                    while (RS.Read())
                    {
                        result.Add(new DataCarton
                        {
                            PalletNo = RS["PalletNo"].ToString() ?? string.Empty,
                            KeepingArea = RS["AreaName"].ToString() ?? string.Empty
                            


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

        public List<DataCarton> UpdatePlaceStatus(string barcode)
        {
            List<DataCarton> result = new List<DataCarton>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    sqlstr = "update FG_PalletDetail " +
                        "SET PlaceStatus = 'PLACED' " +
                        "Where BarcodeCarton = @Barcode ";

                    SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                    cmd.Parameters.Add("@Barcode", SqlDbType.NVarChar, 14).Value = barcode;


                    RS = cmd.ExecuteReader();
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
