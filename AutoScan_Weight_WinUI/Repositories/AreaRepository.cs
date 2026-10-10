using AutoScan_Weight_WinUI.Helps;
using AutoScan_Weight_WinUI.Models;
using AutoScan_Weight_WinUI.Services;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Transactions;
using static CommunityToolkit.WinUI.Animations.Expressions.ExpressionValues;

namespace AutoScan_Weight_WinUI.Repositories
{
    internal class AreaRepository
    {
        SqlDataReader? RS;
        private readonly ConnectSQL _db = new ConnectSQL();
        string sqlstr = string.Empty;

        public List<AreaModel> GetAreas()
        {
            List<AreaModel> areas = new();

            try 
            {
                if (_db.YPTInnovation())
                {
                    sqlstr = "SELECT a.AreaNo,a.AreaName,a.WarehouseName,a.Status,a.InputDate,a.PalletNo, b.GroupKey " +
                        ", b.GroupType , b.CartonCount , b.MaxCapacity, b.PalletStatus " +
                        "FROM FG_Warehouse_Area a  " +
                        "left join FG_Pallet b on a.AreaNo = b.AreaNo and a.PalletNo = b.PalletNo " +
                        "ORDER BY AreaName asc ";

                    SqlCommand cmd = new(sqlstr, _db.conn);

                    SqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var area = new AreaModel()
                        {
                            AreaNo = reader["AreaNo"].ToString(),
                            AreaName = reader["AreaName"].ToString(),
                            WarehouseName = reader["WarehouseName"].ToString(),
                            Status = reader["Status"].ToString(),
                            PalletsNo = reader["PalletNo"].ToString(),
                            GroupKey = reader["GroupKey"].ToString(),
                            GroupType = reader["GroupType"].ToString(),
                            CartonCount = reader["CartonCount"].ToString(),
                            MaxCapital = reader["MaxCapacity"].ToString(),
                            PalletStatus = reader["PalletStatus"].ToString()
                        };

                        
                        // คำนวณตำแหน่ง
                        AreaLayoutEngine.SetPosition(area);
                        
                        // เพิ่มลง List
                        areas.Add(area);
                    }
                    reader.Close();
                }           
            }
            finally
            {
                _db.Close_Sql();
            }
            return areas;

        }

        public bool SwapArea(AreaModel source, AreaModel target , string sourceAreaNo , string sourcePalletNo , string targetAreaNo , string targetPalletNo)
        { 
            if(!_db.YPTInnovation())
                return false;

            SqlTransaction transaction = _db.conn.BeginTransaction();

           

            try
            {
                // =====================================
                // 1. ตรวจสอบ Source
                // =====================================
                if(!CheckAreaVersion(sourceAreaNo,sourcePalletNo,_db.conn,transaction))
                {
                    transaction.Rollback();
                    System.Diagnostics.Debug.WriteLine($"Concurrency conflict: {source.AreaNo}");

                    return false;

                }

                // =====================================
                // 2. ตรวจสอบ Target
                // =====================================

                if (!CheckAreaVersion(sourceAreaNo, sourcePalletNo, _db.conn, transaction))
                {
                    transaction.Rollback();
                    System.Diagnostics.Debug.WriteLine($"Concurrency conflict: {target.AreaNo}");

                    return false;
                }



                // =====================================
                // 3. Update FG_Warehouse_Area
                // =====================================
                UpdateArea(source, _db.conn, transaction);

                UpdateArea(target, _db.conn, transaction);

                // =====================================
                // 4. Update FG_PalletDetail AreaNo
                // =====================================
                UpdateAreaNo(source, _db.conn, transaction);
                UpdateAreaNo(target, _db.conn, transaction);

                // =====================================
                // 5. Commit
                // =====================================
                transaction.Commit();

                return true;

            }
            catch
            {
                transaction.Rollback(); 
                return false;               
            }
            finally
            {
                _db.Close_Sql();
            }
        
        }

        private void UpdateArea(AreaModel area, SqlConnection conn, SqlTransaction tran)
        {

            DateTime AdDate = DateTime.Now;
            string Time_Now = AdDate.ToString("yyyy-MM-dd HH:mm:ss.fff");

            sqlstr = @"UPDATE FG_Warehouse_Area " +
                   "SET AreaNo = @AreaNo , AreaName = @AreaName  , " +
                   "Status = @Status , InputDate = @TimeNow , PalletNo = @PalletNo " +
                   "where AreaNo = @AreaNo ";

            SqlCommand cmd = new(sqlstr, _db.conn, tran);

            cmd.Parameters.AddWithValue("@AreaNo", area.AreaNo);
            cmd.Parameters.AddWithValue("@AreaName", area.AreaName);
            cmd.Parameters.AddWithValue("@WarehouesName", area.WarehouseName);
            cmd.Parameters.AddWithValue("@Status", area.Status);
            cmd.Parameters.AddWithValue("@TimeNow", Time_Now);
            cmd.Parameters.AddWithValue("@PalletNo", area.PalletsNo);
            cmd.ExecuteNonQuery();

        }

        private void UpdateAreaNo(AreaModel area, SqlConnection conn, SqlTransaction tran)
        {

            sqlstr = @"UPDATE FG_Pallet " +
                "SET AreaNo = @AreaNo " +
                "where PalletNo = @PalletNo ";

            SqlCommand cmd = new(sqlstr, _db.conn, tran);

            cmd.Parameters.AddWithValue("@AreaNo", area.AreaNo);
            cmd.Parameters.AddWithValue("@PalletNo", area.PalletsNo);
            cmd.ExecuteNonQuery();



        }

        public List<AreaModel> GetAreaByOrder(string orderNo)
        {
            List<AreaModel> result = new List<AreaModel>();

            if (_db.YPTInnovation())
            {

                sqlstr = @"select DISTINCT c.AreaNo , c.AreaName  " +
               "from FG_PalletDetail a " +
               "left join FG_PalletDetail_Order b on a.BarcodeCarton = b.BarcodeCarton " +
               "left join FG_Warehouse_Area c on a.PalletNo = c.PalletNo " +
               "where b.OrderNo = @OrderNo and InvoiceNo is null and CancelledDate is null";

                SqlCommand cmd = new SqlCommand(sqlstr, _db.conn);
                cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 50).Value = orderNo;


                RS = cmd.ExecuteReader();
                while (RS.Read())
                {
                    result.Add(new AreaModel
                    {
                        AreaNo = RS["AreaNo"].ToString() ?? string.Empty,
                        AreaName = RS["AreaName"].ToString() ?? string.Empty,
                        

                    });
                }
            }     
                _db.Close_Sql();
            
                return result;
        }

        public List<PalletDetailModel> GetDocNoByOrder(string orderNo , string docNo)
        {
            List<PalletDetailModel> result = new List<PalletDetailModel>();

            if (_db.YPTInnovation())
            {

                sqlstr = @"select DISTINCT c.AreaNo , c.AreaName , b.OrderNo , a.DocNo , b.FinalDest " +
               "from FG_PalletDetail a " +
               "left join FG_PalletDetail_Order b on a.BarcodeCarton = b.BarcodeCarton " +
               "left join FG_Warehouse_Area c on a.PalletNo = c.PalletNo " +
               "where b.OrderNo = @OrderNo and a.DocNo = @DocNo ";

                SqlCommand cmd = new SqlCommand(sqlstr, _db.conn);
                cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 50).Value = orderNo;
                cmd.Parameters.Add("@DocNo",SqlDbType.NVarChar,50).Value = docNo;


                RS = cmd.ExecuteReader();
                while (RS.Read())
                {
                    result.Add(new PalletDetailModel
                    {
                        AreaNo = RS["AreaNo"].ToString() ?? string.Empty,
                        AreaName = RS["AreaName"].ToString() ?? string.Empty,
                        OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                        DocNo = RS["DocNo"].ToString() ?? string.Empty,
                        FinalDest = RS["FinalDest"].ToString()?? string.Empty
                        


                    });
                }
            }
            _db.Close_Sql();

            return result;
        }

        public List<PalletDetailModel> GetFinalDestByOrder(string orderNo, string docNo , string final)
        {
            List<PalletDetailModel> result = new List<PalletDetailModel>();

            if (_db.YPTInnovation())
            {

                sqlstr = @"select DISTINCT c.AreaNo , c.AreaName , b.OrderNo , a.DocNo , b.FinalDest , a.ScanCarton " +
               "from FG_PalletDetail a " +
               "left join FG_PalletDetail_Order b on a.BarcodeCarton = b.BarcodeCarton " +
               "left join FG_Warehouse_Area c on a.PalletNo = c.PalletNo " +
               "where b.OrderNo = @OrderNo and a.DocNo = @DocNo and b.FinalDest = @Finaldest";

                SqlCommand cmd = new SqlCommand(sqlstr, _db.conn);
                cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 50).Value = orderNo;
                cmd.Parameters.Add("@DocNo", SqlDbType.NVarChar, 50).Value = docNo;
                cmd.Parameters.Add("@Finaldest",SqlDbType.NVarChar,50).Value = final;


                RS = cmd.ExecuteReader();
                while (RS.Read())
                {
                    result.Add(new PalletDetailModel
                    {
                        AreaNo = RS["AreaNo"].ToString() ?? string.Empty,
                        AreaName = RS["AreaName"].ToString() ?? string.Empty,
                        OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                        DocNo = RS["DocNo"].ToString() ?? string.Empty,
                        FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                        Scancarton = RS["ScanCarton"].ToString() ?? string.Empty


                    });
                }
            }
            _db.Close_Sql();

            return result;
        }

        public List<PalletDetailModel> GetScancartonByOrder(string orderNo, string docNo, string final , string carton)
        {

            List<PalletDetailModel> result = new List<PalletDetailModel>();

            if (_db.YPTInnovation())
            {

                sqlstr = @"select DISTINCT c.AreaNo , c.AreaName , b.OrderNo , a.DocNo , b.FinalDest , a.ScanCarton " +
               "from FG_PalletDetail a " +
               "left join FG_PalletDetail_Order b on a.BarcodeCarton = b.BarcodeCarton " +
               "left join FG_Warehouse_Area c on a.PalletNo = c.PalletNo " +
               "where b.OrderNo = @OrderNo and a.DocNo = @DocNo and b.FinalDest = @Finaldest and a.ScanCarton = @ScanCarton";

                SqlCommand cmd = new SqlCommand(sqlstr, _db.conn);
                cmd.Parameters.Add("@OrderNo", SqlDbType.NVarChar, 50).Value = orderNo;
                cmd.Parameters.Add("@DocNo", SqlDbType.NVarChar, 50).Value = docNo;
                cmd.Parameters.Add("@Finaldest", SqlDbType.NVarChar, 50).Value = final;
                cmd.Parameters.Add("@ScanCarton",SqlDbType.NVarChar,50).Value = carton;


                RS = cmd.ExecuteReader();
                while (RS.Read())
                {
                    result.Add(new PalletDetailModel
                    {
                        AreaNo = RS["AreaNo"].ToString() ?? string.Empty,
                        AreaName = RS["AreaName"].ToString() ?? string.Empty,
                        OrderNo = RS["OrderNo"].ToString() ?? string.Empty,
                        DocNo = RS["DocNo"].ToString() ?? string.Empty,
                        FinalDest = RS["FinalDest"].ToString() ?? string.Empty,
                        Scancarton = RS["ScanCarton"].ToString() ?? string.Empty


                    });
                }
            }
            _db.Close_Sql();

            return result;


        }

        public List<PalletDetailModel> GetPackingList(string packing)
        {
            List<PalletDetailModel> result = new List<PalletDetailModel>();

            if (_db.YPTInnovation())
            {

                sqlstr = @"select DISTINCT c.AreaNo , c.AreaName, a.DocNo ,d.GroupType , d.GroupKey  " +
               "from FG_PalletDetail a  " +
               "left join FG_PalletDetail_Order b on a.BarcodeCarton = b.BarcodeCarton  " +
               "left join FG_Warehouse_Area c on a.PalletNo = c.PalletNo " +
               "left join FG_Pallet d on a.PalletNo = d.PalletNo " +
               "where a.DocNo = @DocNo ";

                SqlCommand cmd = new SqlCommand(sqlstr, _db.conn);
                cmd.Parameters.Add("@DocNo", SqlDbType.NVarChar, 50).Value = packing;
                


                RS = cmd.ExecuteReader();
                while (RS.Read())
                {
                    result.Add(new PalletDetailModel
                    {
                        AreaNo = RS["AreaNo"].ToString() ?? string.Empty,
                        AreaName = RS["AreaName"].ToString() ?? string.Empty,
                        DocNo = RS["DocNo"].ToString() ?? string.Empty,
                        GroupType = RS["GroupType"].ToString() ?? string.Empty,
                        GroupKey = RS["GroupKey"].ToString() ?? string.Empty
                        
                    });
                }
            }
            _db.Close_Sql();

            return result;

        }

        private bool CheckAreaVersion(string areaNo , string palletNo , SqlConnection conn , SqlTransaction transaction)
        {
            sqlstr = @"SELECT PalletNo From FG_Warehouse_Area Where AreaNo = @AreaNo ";

            using SqlCommand cmd = new SqlCommand(sqlstr, conn,transaction);

            cmd.Parameters.Add("@AreaNo",SqlDbType.NVarChar,50).Value = areaNo;

            object? result = cmd.ExecuteScalar();

            string dbPallet = result?.ToString()?? string.Empty;

            palletNo ??= string.Empty;

            return string.Equals(dbPallet, palletNo , StringComparison.OrdinalIgnoreCase); 
        
        
        }


    }
}
