using Microsoft.Data.SqlClient;
using AutoScan_Weight_WinUI.Models;
using System.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Services;

public class PalletOptimizerService
{
    // =====================================================================
    // ENTRY POINT
    // =====================================================================

    /// <summary>
    /// Optimize Carton ของ DocNo ที่ระบุ ลง Pallet ตามเงื่อนไขทั้งหมด
    ///
    /// Grain สำคัญที่ต้องเข้าใจก่อน:
    ///   - Query ต้นทางคืนมาเป็น "1 แถว = 1 Order-line" (1 กล่องอาจมีได้หลายแถว
    ///     ถ้ามีของจากมากกว่า 1 Order ปะปนกัน)
    ///   - หน่วยที่ใช้จัดลง Pallet จริงคือ "กล่อง" (BarcodeCarton) ไม่ใช่แถว -
    ///     ต้อง Group แถวตาม BarcodeCarton ก่อนเสมอ (ดู BuildCartonGroups)
    ///   - DocNo/Warehouse/BarcodeCarton เป็นจริงระดับกล่อง (ค่าเดียวเสมอ)
    ///     ส่วน OrderNo/FinalDest/StyleID/Style/CustPORef/LotRef เป็นระดับ
    ///     Order-line (มีได้หลายค่าในกล่องเดียวกัน)
    ///   - กฎ: ถ้ากล่องมีหลาย FinalDest ได้ ต้องมี Warehouse กำกับเสมอ ดังนั้น
    ///     GroupKey (Warehouse ก่อน ไม่งั้น fallback FinalDest) ของ 1 กล่อง
    ///     จะมีค่าเดียวเสมอ ไม่ว่าข้างในจะมีกี่ Order-line ก็ตาม - ถ้าตรวจพบว่า
    ///     ไม่เป็นไปตามกฎนี้ (ข้อมูลต้นทางผิดปกติ) จะกันกล่องนั้นออกจากการ
    ///     Optimize อัตโนมัติ ไปอยู่ใน OptimizeResult.ConflictingCartons แทน
    ///
    /// Logic การจัด Pallet (เหมือนเดิม แต่หน่วยเปลี่ยนจาก Row เป็น CartonGroup):
    ///   1) แยกกลุ่มตาม GroupKey
    ///   2) ในแต่ละ GroupKey แยกย่อยตาม DocNo
    ///        - MaxCapacity ของแต่ละ Chunk คำนวณสดจาก Customer ของกล่องในกลุ่มนั้น
    ///          ผ่าน PalletProvisioning.GetMaxCapacity(customer) - Customer='OD' ได้ 30
    ///          นอกนั้นได้ 35 (ข้อมูลจริง: GroupKey ผูกกับ Customer แบบ 1:1 อยู่แล้ว
    ///          เพราะ Warehouse/FinalDest แต่ละค่าเป็นของ Customer เดียวเสมอ ดังนั้น
    ///          ทุกกล่องใน docGroup เดียวกันควรมี Customer เดียวกัน ใช้ค่าแรกได้เลย)
    ///        - ถ้า DocNo นั้นมีกล่อง > MaxCapacity -> หัก Chunk ละ MaxCapacity สร้าง
    ///          Pallet ใหม่เฉพาะของ DocNo นั้น เศษที่เหลือไปต่อที่ Best-fit
    ///        - ถ้า DocNo นั้นมีกล่อง <= MaxCapacity ไปที่ Best-fit ตรงๆ
    ///   3) Best-fit: หา Pallet OPEN ที่ GroupKey ตรงกัน เหลือพื้นที่น้อยที่สุดก่อน
    ///      ถ้าไม่มีเลยค่อยสร้าง/Recycle ผ่าน PalletProvisioning.CreateNewPallet
    ///
    /// Idempotent: BarcodeCarton ที่ Optimize ไปแล้วจะถูกข้าม ไม่ซ้ำซ้อน
    /// </summary>
    public OptimizeResult OptimizePallets(string docNo)
    {
        var result = new OptimizeResult { DocNo = docNo };

        if (string.IsNullOrWhiteSpace(docNo))
        {
            App.DialogService.ShowMessageAsync("Failed", "DocNo is required");
            //MessageBox.Show("DocNo is required", "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return result;
        }

        var rows = LoadCartonInput(docNo);
        if (rows.Count == 0)
            return result; // ไม่มีข้อมูลให้ Optimize

        // 1) Group แถว (Order-line) ตาม BarcodeCarton ให้เป็น "กล่องจริง" +
        //    Validate กฎ GroupKey ต้องค่าเดียวต่อกล่อง - กล่องที่ผิดกฎถูกกันออก
        var cartonGroups = BuildCartonGroups(rows, out List<ConflictingCarton> conflicts);
        result.ConflictingCartons = conflicts;
        result.MultiOrderCartonCount = cartonGroups.Count(g => g.OrderLines.Count > 1);

        if (cartonGroups.Count == 0)
            return result;

        ConnectSQL connectSQL = new ConnectSQL();
        SqlTransaction tran = null;

        try
        {
            if (connectSQL.YPTInnovation())
            {
                SqlConnection conn = connectSQL.conn;
                tran = conn.BeginTransaction(IsolationLevel.Serializable);

                // 2) กันข้อมูลซ้ำ - กล่องที่เคย Optimize ไปแล้วให้ตัดออก
                var newCartons = ExcludeAlreadyOptimized(conn, tran, cartonGroups);
                result.TotalCartonSkipped = cartonGroups.Count - newCartons.Count;
                result.TotalCartonProcessed = newCartons.Count;

                if (newCartons.Count > 0)
                {
                    // 3) จัดกลุ่มตาม GroupKey ก่อน (Warehouse ถ้ามี ไม่งั้นใช้ FinalDest)
                    var groupKeyGroups = newCartons
                        .GroupBy(c => c.GroupKey ?? string.Empty)
                        .ToList();

                    foreach (var gkGroup in groupKeyGroups)
                    {
                        if (string.IsNullOrWhiteSpace(gkGroup.Key))
                            continue; // ไม่ควรเกิดแล้วเพราะ BuildCartonGroups กรองออกไปก่อนหน้านี้

                        string groupKey = gkGroup.Key;
                        string groupType = gkGroup.First().GroupType;

                        // 4) แยกย่อยตาม DocNo เพื่อเช็คเงื่อนไข > MaxCapacity
                        var docGroups = gkGroup.GroupBy(c => c.DocNo);

                        foreach (var docGroup in docGroups)
                        {
                            var cartonList = docGroup.ToList();

                            // MaxCapacity คำนวณสดจาก Customer ของกล่องในกลุ่มนี้ (Local
                            // Variable เท่านั้น - ไม่มี State ค้างข้าม docGroup/Thread)
                            int maxCapacity = PalletProvisioning.GetMaxCapacity(cartonList[0].Customer);

                            int total = cartonList.Count;
                            int fullChunkCount = total / maxCapacity;
                            int remainder = total % maxCapacity;
                            int idx = 0;

                            // 4.1) ถ้าเกิน MaxCapacity -> สร้าง Pallet ใหม่ "เฉพาะ DocNo นี้" ทุก Chunk ละ maxCapacity
                            for (int chunk = 0; chunk < fullChunkCount; chunk++)
                            {
                                var pallet = PalletProvisioning.CreateNewPallet(conn, tran, groupKey, groupType, maxCapacity);

                                result.AreaReservations.Add(new AreaReservation
                                {
                                    PalletNo = pallet.PalletNo,
                                    AreaNo = pallet.AreaNo,
                                    WarehouseName = groupKey
                                });

                                for (int seq = 1; seq <= maxCapacity; seq++)
                                {
                                    var carton = cartonList[idx++];
                                    InsertPalletDetail(conn, tran, pallet.PalletNo, carton, seq);

                                    result.Assignments.Add(new PalletAssignment
                                    {
                                        PalletNo = pallet.PalletNo,
                                        BarcodeCarton = carton.BarcodeCarton,
                                        GroupKey = groupKey,
                                        SeqInPallet = seq
                                    });
                                }

                                // เต็มพอดี -> Pallet นี้จะถูก Mark เป็น FULL ทันที
                                PalletProvisioning.UpdatePalletCount(conn, tran, pallet.PalletNo, maxCapacity, maxCapacity);
                            }

                            // 4.2) เศษที่เหลือ (< maxCapacity) หรือกรณี DocNo <= MaxCapacity ทั้งหมด
                            //      -> ใช้ Best-fit หา Pallet OPEN ที่เหลือพื้นที่น้อยที่สุดก่อน
                            if (remainder > 0)
                            {
                                var queue = new Queue<CartonGroup>(cartonList.Skip(idx));

                                while (queue.Count > 0)
                                {
                                    var pallet = GetBestFitOpenPallet(conn, tran, groupKey, groupType, maxCapacity);

                                    if (pallet.AreaNo != null)
                                    {
                                        result.AreaReservations.Add(new AreaReservation
                                        {
                                            PalletNo = pallet.PalletNo,
                                            AreaNo = pallet.AreaNo,
                                            WarehouseName = groupKey
                                        });
                                    }

                                    int seq = pallet.CartonCount + 1;

                                    while (pallet.RemainingSpace > 0 && queue.Count > 0)
                                    {
                                        var carton = queue.Dequeue();
                                        InsertPalletDetail(conn, tran, pallet.PalletNo, carton, seq);

                                        result.Assignments.Add(new PalletAssignment
                                        {
                                            PalletNo = pallet.PalletNo,
                                            BarcodeCarton = carton.BarcodeCarton,
                                            GroupKey = groupKey,
                                            SeqInPallet = seq
                                        });

                                        seq++;
                                        pallet.CartonCount++;
                                    }

                                    PalletProvisioning.UpdatePalletCount(conn, tran, pallet.PalletNo, pallet.CartonCount, pallet.MaxCapacity);
                                }
                            }
                        }
                    }
                }

                tran.Commit();
            }
            else
            {
                App.DialogService.ShowMessageAsync("Failed", "ไม่สามารถเชื่อมต่อฐานข้อมูล YPT_Innovation ได้");
                //MessageBox.Show("ไม่สามารถเชื่อมต่อฐานข้อมูล YPT_Innovation ได้", "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            tran?.Rollback();
            App.DialogService.ShowMessageAsync("Failed", "Optimize Pallet Failed : " + ex.Message);
            ///MessageBox.Show("Optimize Pallet Failed : " + ex.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            connectSQL.Close_Sql();
        }

        return result;
    }

    // =====================================================================
    // STEP: โหลดข้อมูลจาก Query ต้นทาง (Database Netgarment)
    // คืนมาเป็น "1 แถว = 1 Order-line" ยังไม่ Group เป็นกล่อง
    // =====================================================================
    public List<CartonInput> LoadCartonInput(string docNo)
    {
        var result = new List<CartonInput>();
        ConnectSQL connectSQL = new ConnectSQL();

        try
        {
            if (connectSQL.Netgarment())
            {
                string sqlstr = @"
                                with cte_Master as
                                (
                                    Select Distinct a.PackNo, a.Packinglist, a.Customer, b.OrderNo, b.FinalDest, b.Carton as ScanCarton, c.BarcodeCarton, b.StyleID, b.Style, d.CustPORef, d.LotRef, e.PkExtStyleDesc2 as Warehouse, f.InvoiceNo
                                    From Netgarment.dbo.Packing_ScanDoc a
                                    Left join Netgarment.dbo.Packing_ScanOrder b on a.PackNo = b.PackNo
                                    Left join Netgarment.dbo.Packing_ScanBarcodeCarton c on a.PackNo = c.PackNo and b.BoxNo = c.BoxNo
                                    Left join Netgarment.dbo.shpcvshp d on a.Packinglist = d.DocNo and b.StyleID = d.StyleID and b.OrderNo = d.OrderNo and b.Style = d.Style
                                    Left join Netgarment.dbo.shpstyle e on a.Packinglist = e.DocNo and b.StyleID = e.StyleID and b.Style = e.Style and e.PkExtStyleDesc1 = b.FinalDest
                                    Left join Netgarment.dbo.Scan_Order_InoutStatus f on b.OrderNo = f.OrderNo and a.Packinglist = f.PackNo and b.FinalDest = f.FinalDest and b.Carton = f.ScanCarton and c.BarcodeCarton = f.BarcodeCarton
                                    where a.Packinglist = @DocNo and f.InvoiceNo is null and c.BarcodeCarton is not null and f.InvoiceNo is null and c.BarcodeCarton is not null
                                )
                                Select Packinglist as DocNo, StyleID, OrderNo, Style, CustPORef, LotRef, FinalDest, Warehouse, Customer, BarcodeCarton, ScanCarton, InvoiceNo
                                From cte_Master
                                Order by OrderNo, FinalDest, ScanCarton asc";

                SqlCommand cmd = new SqlCommand(sqlstr, connectSQL.conn);
                cmd.Parameters.AddWithValue("@DocNo", docNo);

                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new CartonInput
                    {
                        DocNo = reader["DocNo"] as string,
                        StyleID = reader["StyleID"] as string,
                        OrderNo = reader["OrderNo"] as string,
                        Style = reader["Style"] as string,
                        CustPORef = reader["CustPORef"] as string,
                        LotRef = reader["LotRef"] as string,
                        FinalDest = reader["FinalDest"] as string,
                        Warehouse = reader["Warehouse"] as string,
                        BarcodeCarton = reader["BarcodeCarton"] as string,
                        ScanCarton = reader["ScanCarton"] as string,
                        InvoiceNo = reader["InvoiceNo"] as string,
                        Customer = reader["Customer"] as string
                    });
                }
                reader.Close();
            }
        }
        catch (Exception ex)
        {
            App.DialogService.ShowMessageAsync("Failed", "Query Failed : " + ex.Message);
            //MessageBox.Show("Query Failed : " + ex.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            connectSQL.Close_Sql();
        }

        return result;
    }

    // =====================================================================
    // STEP: Group แถว (Order-line) ตาม BarcodeCarton ให้เป็น "กล่องจริง"
    // + Validate กฎ GroupKey ต้องมีค่าเดียวต่อกล่อง
    //
    // กฎ (ตามที่ยืนยันแล้ว):
    //   - ถ้ามี Warehouse -> ต้องเป็นค่าเดียวกันทั้งกล่องเสมอ (100%) ใช้เป็น
    //     GroupKey ได้เลย ไม่ว่าจะมีกี่ FinalDest/OrderNo ต่างกันข้างในก็ตาม
    //   - ถ้าไม่มี Warehouse เลย -> FinalDest ต้องเป็นค่าเดียวกันทั้งกล่อง
    //     (กฎ: จะมีหลาย FinalDest ได้ก็ต่อเมื่อมี Warehouse กำกับเท่านั้น)
    //     ถ้าไม่ตรงตามนี้ ถือว่าข้อมูลต้นทางผิดปกติ -> กันออกเป็น Conflict
    // =====================================================================
    private List<CartonGroup> BuildCartonGroups(List<CartonInput> rows, out List<ConflictingCarton> conflicts)
    {
        conflicts = new List<ConflictingCarton>();
        var result = new List<CartonGroup>();

        var byBarcode = rows.GroupBy(r => r.BarcodeCarton, StringComparer.OrdinalIgnoreCase);

        foreach (var g in byBarcode)
        {
            var rowsInGroup = g.ToList();

            var warehouses = rowsInGroup
                .Select(r => r.Warehouse)
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string groupKey;
            string groupType;

            if (warehouses.Count > 1)
            {
                conflicts.Add(new ConflictingCarton
                {
                    BarcodeCarton = g.Key,
                    Reason = $"พบ Warehouse มากกว่า 1 ค่าในกล่องเดียวกัน (ผิดกฎ): {string.Join(", ", warehouses)}"
                });
                continue;
            }

            if (warehouses.Count == 1)
            {
                groupKey = warehouses[0];
                groupType = "Warehouse";
            }
            else
            {
                var finalDests = rowsInGroup
                    .Select(r => r.FinalDest)
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Select(f => f.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (finalDests.Count > 1)
                {
                    conflicts.Add(new ConflictingCarton
                    {
                        BarcodeCarton = g.Key,
                        Reason = $"ไม่มี Warehouse และพบ FinalDest มากกว่า 1 ค่าในกล่องเดียวกัน (ผิดกฎ): {string.Join(", ", finalDests)}"
                    });
                    continue;
                }

                if (finalDests.Count == 0)
                {
                    conflicts.Add(new ConflictingCarton
                    {
                        BarcodeCarton = g.Key,
                        Reason = "ไม่มีทั้ง Warehouse และ FinalDest ไม่สามารถจัดกลุ่มได้"
                    });
                    continue;
                }

                groupKey = finalDests[0];
                groupType = "FinalDest";
            }

            var first = rowsInGroup[0];
            var cartonGroup = new CartonGroup
            {
                DocNo = first.DocNo,
                BarcodeCarton = first.BarcodeCarton,
                ScanCarton = first.ScanCarton,
                Warehouse = first.Warehouse,
                Customer = first.Customer,
                InvoiceNo = first.InvoiceNo,
                GroupKey = groupKey,
                GroupType = groupType
            };

            // Dedupe เฉพาะกรณีแถวซ้ำเป๊ะทุก Field (Join Fan-out ซ้ำจริงๆ ไม่ใช่
            // Order-line ที่ต่างกัน) - ไม่ทิ้ง Order-line ที่ต่างกันจริง
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rowsInGroup)
            {
                string key = string.Join("|", row.OrderNo, row.FinalDest, row.StyleID, row.Style, row.CustPORef, row.LotRef);
                if (seen.Add(key))
                {
                    cartonGroup.OrderLines.Add(new OrderLine
                    {
                        OrderNo = row.OrderNo,
                        FinalDest = row.FinalDest,
                        StyleID = row.StyleID,
                        Style = row.Style,
                        CustPORef = row.CustPORef,
                        LotRef = row.LotRef
                    });
                }
            }

            result.Add(cartonGroup);
        }

        return result;
    }

    // =====================================================================
    // STEP: กันการ Optimize ซ้ำ (Idempotency) - เช็คระดับกล่อง (BarcodeCarton)
    // =====================================================================
    private List<CartonGroup> ExcludeAlreadyOptimized(
        SqlConnection conn, SqlTransaction tran, List<CartonGroup> cartons)
    {
        if (cartons.Count == 0) return cartons;

        var barcodes = cartons.Select(c => c.BarcodeCarton).Distinct().ToList();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var paramNames = barcodes.Select((b, i) => "@b" + i).ToList();
        string sql = $@"SELECT BarcodeCarton FROM dbo.FG_PalletDetail WITH (NOLOCK)
                             WHERE BarcodeCarton IN ({string.Join(",", paramNames)})";

        SqlCommand cmd = new SqlCommand(sql, conn, tran);
        for (int i = 0; i < barcodes.Count; i++)
            cmd.Parameters.AddWithValue(paramNames[i], barcodes[i]);

        SqlDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
            existing.Add(reader.GetString(0));
        reader.Close();

        return cartons.Where(c => !existing.Contains(c.BarcodeCarton)).ToList();
    }

    // =====================================================================
    // STEP: Best-fit - หา Pallet OPEN ที่ "เหลือพื้นที่น้อยที่สุด" ก่อน
    //       ถ้าไม่มีเลย -> สร้าง/Recycle ใบใหม่ผ่าน PalletProvisioning
    // =====================================================================
    private PalletInfo GetBestFitOpenPallet(
        SqlConnection conn, SqlTransaction tran, string groupKey, string groupType, int maxCapacity)
    {
        string selectSql = @"
                SELECT TOP 1 PalletNo, CartonCount, MaxCapacity
                FROM dbo.FG_Pallet WITH (UPDLOCK, ROWLOCK)
                WHERE GroupKey = @GroupKey
                  AND PalletStatus = 'OPEN'
                  AND CartonCount < MaxCapacity
                ORDER BY (MaxCapacity - CartonCount) ASC, PalletNo ASC";

        SqlCommand cmd = new SqlCommand(selectSql, conn, tran);
        cmd.Parameters.AddWithValue("@GroupKey", groupKey);

        SqlDataReader reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var info = new PalletInfo
            {
                PalletNo = reader.GetString(0),
                CartonCount = reader.GetInt32(1),
                MaxCapacity = reader.GetInt32(2)
            };
            reader.Close();
            return info;
        }
        reader.Close();

        return PalletProvisioning.CreateNewPallet(conn, tran, groupKey, groupType, maxCapacity);
    }

    // =====================================================================
    // STEP: บันทึกกล่องลง Pallet (ระดับกล่อง) + Order-line ทั้งหมดข้างใน
    // =====================================================================
    private void InsertPalletDetail(
        SqlConnection conn, SqlTransaction tran, string palletNo, CartonGroup c, int seq)
    {
        string sql = @"
                INSERT INTO dbo.FG_PalletDetail
                (PalletNo, DocNo, BarcodeCarton, ScanCarton, Warehouse, Customer, InvoiceNo, SeqInPallet, PlaceStatus, CreateDate)
                VALUES
                (@PalletNo, @DocNo, @BarcodeCarton, @ScanCarton, @Warehouse, @Customer, @InvoiceNo, @SeqInPallet, 'RESERVED', GETDATE())";

        SqlCommand cmd = new SqlCommand(sql, conn, tran);
        cmd.Parameters.AddWithValue("@PalletNo", palletNo);
        cmd.Parameters.AddWithValue("@DocNo", (object)c.DocNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BarcodeCarton", c.BarcodeCarton);
        cmd.Parameters.AddWithValue("@ScanCarton", (object)c.ScanCarton ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Warehouse", (object)c.Warehouse ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Customer", (object)c.Customer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@InvoiceNo", (object)c.InvoiceNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SeqInPallet", seq);
        cmd.ExecuteNonQuery();

        InsertPalletDetailOrders(conn, tran, c.BarcodeCarton, c.OrderLines);
    }

    // =====================================================================
    // STEP: บันทึก Order-line ทั้งหมดของกล่องนี้ลง FG_PalletDetail_Order
    // (1 กล่อง อาจมีได้หลายแถว)
    // =====================================================================
    private void InsertPalletDetailOrders(
        SqlConnection conn, SqlTransaction tran, string barcodeCarton, List<OrderLine> orderLines)
    {
        string sql = @"
                INSERT INTO dbo.FG_PalletDetail_Order
                (BarcodeCarton, OrderNo, FinalDest, StyleID, Style, CustPORef, LotRef, CreateDate)
                VALUES
                (@BarcodeCarton, @OrderNo, @FinalDest, @StyleID, @Style, @CustPORef, @LotRef, GETDATE())";

        foreach (var line in orderLines)
        {
            SqlCommand cmd = new SqlCommand(sql, conn, tran);
            cmd.Parameters.AddWithValue("@BarcodeCarton", barcodeCarton);
            cmd.Parameters.AddWithValue("@OrderNo", (object)line.OrderNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FinalDest", (object)line.FinalDest ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StyleID", (object)line.StyleID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Style", (object)line.Style ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CustPORef", (object)line.CustPORef ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LotRef", (object)line.LotRef ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }










    /// <summary>
    /// Service สำหรับหน้าจอ "Move Carton" - เป็น Sub Program ของ PalletOptimize
    /// ให้หน้างานยืดหยุ่นจัดการ Case พิเศษ (เช่น ลูกค้าขอด่วนเฉพาะบาง Item)
    /// นอกเหนือจากที่ Optimize จัดให้อัตโนมัติ
    ///
    /// Flow การใช้งาน:
    ///   1) GetMovableCartons()              -> ComboBox1 (เลือก BarcodeCarton)
    ///   2) GetCartonLocation(barcode)        -> Detail Box (ตำแหน่งปัจจุบัน)
    ///   3) GetAvailableDestinationPallets()  -> ComboBox2 (เลือก Pallet ปลายทาง
    ///                                            รวมตัวเลือก "สร้าง Pallet ใหม่")
    ///   4) MoveCarton(barcode, destPallet)   -> ปุ่ม Save (ยืนยันการย้าย)
    /// </summary>
    public class CartonMoveService
    {
        /// <summary>
        /// Flag ควบคุมว่าจะ Reset PlaceStatus จาก PLACED กลับเป็น RESERVED ตอน Move
        /// หรือไม่ (ดูคำอธิบายเต็มที่จุดใช้งานใน MoveCarton)
        ///
        /// ปัจจุบัน = false เพราะ Business ตอนนี้: มี User ที่มีสิทธิ์ Move Carton
        /// ได้เพียงคนเดียว (ควบคุมผ่านสิทธิ์การเข้าใช้งานของระบบ Warehouse อยู่แล้ว)
        /// และ Action "กด Move ในระบบ" กับ "ยกของจริง" เกิดขึ้นพร้อมกันเสมอ แม้จะสั่ง
        /// ผ่านผู้ช่วยก็ยังถือเป็นคำสั่ง Move ทันที ไม่มีช่วงเวลาที่ข้อมูลกับของจริง
        /// จะไม่ตรงกันค้างไว้ - จึงไม่จำเป็นต้อง Reset ให้ไปรอ Program Track มา
        /// Confirm ซ้ำอีกชั้น
        ///
        /// ในอนาคตถ้ามีการเพิ่ม Program Track แยกต่างหาก (สแกนยืนยันการวางจริงที่
        /// หน้างาน อาจมีผู้ปฏิบัติงานหลายคน หรือมีช่วงเวลาห่างระหว่าง Move ในระบบ
        /// กับยกของจริง) ให้เปลี่ยนค่านี้เป็น true ได้ทันที โดยไม่ต้องแก้ Logic อื่น
        /// เพิ่มเลย (UpdateCartonPallet รองรับไว้แล้ว)
        /// </summary>
        private const bool RESET_PLACE_STATUS_ON_MOVE = false;
        // =====================================================================
        // ComboBox 1: รายการ Carton ที่ย้ายได้ (RESERVED และ PLACED - ยังไม่ Release)
        // =====================================================================
        public List<MovableCartonItem> GetMovableCartons(string searchText = null)
        {
            var result = new List<MovableCartonItem>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    string sql = @"
                        SELECT BarcodeCarton, DocNo, PalletNo
                        FROM dbo.FG_PalletDetail
                        WHERE PlaceStatus IN ('RESERVED', 'PLACED')
                          AND CancelledDate IS NULL";
                          //AND ReleasedDate IS NULL";

                    if (!string.IsNullOrWhiteSpace(searchText))
                        sql += " AND BarcodeCarton LIKE @SearchText + '%'";

                    sql += " ORDER BY BarcodeCarton";

                    SqlCommand cmd = new SqlCommand(sql, connectSQL.conn);
                    if (!string.IsNullOrWhiteSpace(searchText))
                        cmd.Parameters.AddWithValue("@SearchText", searchText);

                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        result.Add(new MovableCartonItem
                        {
                            BarcodeCarton = reader["BarcodeCarton"] as string,
                            DocNo = reader["DocNo"] as string,
                            PalletNo = reader["PalletNo"] as string
                        });
                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                App.DialogService.ShowMessageAsync("Failed", "Query Failed : " + ex.Message);
                //MessageBox.Show("Query Failed : " + ex.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return result;
        }

        // =====================================================================
        // Detail Box: ตำแหน่งปัจจุบันของ Carton ที่เลือก (รวม Order-line ทั้งหมด
        // ที่อยู่ในกล่องนี้ เพราะ 1 กล่องอาจมีของจากมากกว่า 1 Order)
        // =====================================================================
        public CartonLocationInfo GetCartonLocation(string barcodeCarton)
        {
            CartonLocationInfo info = null;
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    string sql = @"
                        SELECT pd.BarcodeCarton, pd.DocNo, pd.Warehouse, pd.Customer, pd.PlaceStatus,
                               p.PalletNo, p.GroupKey, p.CartonCount, p.MaxCapacity,
                               p.AreaNo, wa.AreaName, wa.WarehouseName
                        FROM dbo.FG_PalletDetail pd
                        JOIN dbo.FG_Pallet p ON pd.PalletNo = p.PalletNo
                        LEFT JOIN dbo.FG_Warehouse_Area wa ON p.AreaNo = wa.AreaNo
                        WHERE pd.BarcodeCarton = @BarcodeCarton";

                    SqlCommand cmd = new SqlCommand(sql, connectSQL.conn);
                    cmd.Parameters.AddWithValue("@BarcodeCarton", barcodeCarton);

                    SqlDataReader reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        info = new CartonLocationInfo
                        {
                            BarcodeCarton = reader["BarcodeCarton"] as string,
                            DocNo = reader["DocNo"] as string,
                            Warehouse = reader["Warehouse"] as string,
                            Customer = reader["Customer"] as string,
                            PlaceStatus = reader["PlaceStatus"] as string,
                            PalletNo = reader["PalletNo"] as string,
                            GroupKey = reader["GroupKey"] as string,
                            CartonCount = Convert.ToInt32(reader["CartonCount"]),
                            MaxCapacity = Convert.ToInt32(reader["MaxCapacity"]),
                            AreaNo = reader["AreaNo"] as string,
                            AreaName = reader["AreaName"] as string,
                            WarehouseAreaName = reader["WarehouseName"] as string
                        };
                    }
                    reader.Close();

                    // ดึง Order-line ทั้งหมดที่อยู่ในกล่องนี้แยกต่างหาก (1-ต่อ-หลาย)
                    if (info != null)
                    {
                        string orderSql = @"
                            SELECT OrderNo, FinalDest, StyleID, Style, CustPORef, LotRef
                            FROM dbo.FG_PalletDetail_Order
                            WHERE BarcodeCarton = @BarcodeCarton
                            ORDER BY PalletDetailOrderID ASC";

                        SqlCommand orderCmd = new SqlCommand(orderSql, connectSQL.conn);
                        orderCmd.Parameters.AddWithValue("@BarcodeCarton", barcodeCarton);

                        SqlDataReader orderReader = orderCmd.ExecuteReader();
                        while (orderReader.Read())
                        {
                            info.OrderLines.Add(new OrderLine
                            {
                                OrderNo = orderReader["OrderNo"] as string,
                                FinalDest = orderReader["FinalDest"] as string,
                                StyleID = orderReader["StyleID"] as string,
                                Style = orderReader["Style"] as string,
                                CustPORef = orderReader["CustPORef"] as string,
                                LotRef = orderReader["LotRef"] as string
                            });
                        }
                        orderReader.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                App.DialogService.ShowMessageAsync("Failed", "Query Failed : " + ex.Message);
               // MessageBox.Show("Query Failed : " + ex.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return info;
        }

        // =====================================================================
        // ComboBox 2: Pallet ปลายทางที่เลือกได้ (GroupKey ตรงกัน, ยังไม่เต็ม, ไม่ใช่ใบเดิม)
        //             + ตัวเลือก "สร้าง Pallet ใหม่" ต่อท้าย ถ้ายังมี Warehouse Area ว่าง
        // =====================================================================
        public List<DestinationPalletOption> GetAvailableDestinationPallets(string groupKey, string sourcePalletNo, string customer)
        {
            var result = new List<DestinationPalletOption>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    // 1) Pallet ที่มีอยู่แล้วและยังรับของเพิ่มได้ (MaxCapacity ที่โชว์คือ
                    //    ค่าจริงที่ถูก Set ไว้ตอนสร้าง/Recycle Pallet ใบนั้นแล้ว ไม่ต้อง
                    //    คำนวณใหม่ตรงนี้ - GroupKey ผูกกับ Customer แบบ 1:1 อยู่แล้วตาม
                    //    ข้อมูลจริง จึงไม่มีทาง Pallet ใบเดียวกันมี Customer ปนกัน)
                    string sql = @"
                        SELECT p.PalletNo, p.CartonCount, p.MaxCapacity,
                               (p.MaxCapacity - p.CartonCount) AS RemainingSpace,
                               p.AreaNo, wa.AreaName
                        FROM dbo.FG_Pallet p
                        LEFT JOIN dbo.FG_Warehouse_Area wa ON p.AreaNo = wa.AreaNo
                        WHERE p.GroupKey = @GroupKey
                          AND p.PalletStatus = 'OPEN'
                          AND p.CartonCount < p.MaxCapacity
                          AND p.PalletNo <> @SourcePalletNo
                        ORDER BY p.PalletNo ASC";

                    SqlCommand cmd = new SqlCommand(sql, connectSQL.conn);
                    cmd.Parameters.AddWithValue("@GroupKey", groupKey);
                    cmd.Parameters.AddWithValue("@SourcePalletNo", sourcePalletNo);

                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        result.Add(new DestinationPalletOption
                        {
                            PalletNo = reader["PalletNo"] as string,
                            CartonCount = Convert.ToInt32(reader["CartonCount"]),
                            MaxCapacity = Convert.ToInt32(reader["MaxCapacity"]),
                            RemainingSpace = Convert.ToInt32(reader["RemainingSpace"]),
                            AreaNo = reader["AreaNo"] as string,
                            AreaName = reader["AreaName"] as string,
                            IsNewPalletOption = false
                        });
                    }
                    reader.Close();

                    // 2) เพิ่มตัวเลือก "สร้าง Pallet ใหม่" ถ้ายังมี Warehouse Area ว่าง (Status='Ready')
                    //    ไม่ให้ User เลือก Area เอง - ระบบจะจอง Area แรกที่ว่างให้อัตโนมัติตอนกด Save
                    //    MaxCapacity ของตัวเลือกนี้คำนวณจาก Customer ของ Carton ที่กำลังจะย้าย
                    string checkAreaSql = @"
                        SELECT COUNT(*) FROM dbo.FG_Warehouse_Area WHERE Status = 'Ready'";

                    SqlCommand checkCmd = new SqlCommand(checkAreaSql, connectSQL.conn);
                    int readyAreaCount = (int)checkCmd.ExecuteScalar();

                    if (readyAreaCount > 0)
                    {
                        int maxCapacity = PalletProvisioning.GetMaxCapacity(customer);

                        result.Add(new DestinationPalletOption
                        {
                            PalletNo = DestinationPalletOption.NEW_PALLET_SENTINEL,
                            CartonCount = 0,
                            MaxCapacity = maxCapacity,
                            RemainingSpace = maxCapacity,
                            AreaNo = null,
                            AreaName = "(จะจองอัตโนมัติ)",
                            IsNewPalletOption = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                App.DialogService.ShowMessageAsync("Failed", "Query Failed : " + ex.Message);
                //MessageBox.Show("Query Failed : " + ex.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return result;
        }

        // =====================================================================
        // ปุ่ม Save: ยืนยันการย้าย Carton (Transaction เดียวทั้งหมด)
        // destinationPalletNo อาจเป็น PalletNo จริง หรือ DestinationPalletOption.NEW_PALLET_SENTINEL
        // =====================================================================
        public MoveCartonResult MoveCarton(string barcodeCarton, string destinationPalletNo, string moveBy = null)
        {
            var result = new MoveCartonResult
            {
                BarcodeCarton = barcodeCarton,
                ToPalletNo = destinationPalletNo,
                Success = false
            };

            if (string.IsNullOrWhiteSpace(barcodeCarton) || string.IsNullOrWhiteSpace(destinationPalletNo))
            {
                result.Message = "กรุณาระบุ Barcode Carton และ Pallet ปลายทาง";
                return result;
            }

            bool isNewPallet = destinationPalletNo == DestinationPalletOption.NEW_PALLET_SENTINEL;

            ConnectSQL connectSQL = new ConnectSQL();
            SqlTransaction tran = null;

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    SqlConnection conn = connectSQL.conn;
                    tran = conn.BeginTransaction(IsolationLevel.Serializable);

                    // 1) หา Pallet ต้นทางปัจจุบันของ Carton พร้อม Lock แถวไว้
                    string sourcePalletNo = GetCurrentPalletNoForUpdate(
                        conn, tran, barcodeCarton, out string placeStatus, out string cartonGroupKey,
                        out string groupType, out string cartonCustomer);

                    if (sourcePalletNo == null)
                    {
                        result.Message = $"ไม่พบ Carton '{barcodeCarton}' ในระบบ หรือยังไม่ผ่านการ Optimize";
                        tran.Rollback();
                        return result;
                    }

                    if (!isNewPallet && string.Equals(sourcePalletNo, destinationPalletNo, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Message = "Pallet ต้นทางและปลายทางเป็นใบเดียวกัน";
                        tran.Rollback();
                        return result;
                    }

                    if (!string.Equals(placeStatus, "RESERVED", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(placeStatus, "PLACED", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Message = $"Carton นี้มีสถานะ '{placeStatus}' ไม่สามารถย้ายผ่านระบบนี้ได้ (ต้องเป็น RESERVED หรือ PLACED เท่านั้น)";
                        tran.Rollback();
                        return result;
                    }

                    // ถ้าเดิมเป็น PLACED (วางจริงแล้วที่ตำแหน่งเก่า) ในหลักการควร Reset
                    // กลับเป็น RESERVED เพราะสถานะ "วางแล้ว" ผูกกับตำแหน่งเดิม ไม่ใช่
                    // ตำแหน่งใหม่ - แต่ตอนนี้ปิดไว้ก่อนผ่าน RESET_PLACE_STATUS_ON_MOVE
                    // (ดูคำอธิบายเต็มที่ประกาศ Flag ด้านบนคลาส)
                    bool wasPlaced = string.Equals(placeStatus, "PLACED", StringComparison.OrdinalIgnoreCase);
                    bool shouldResetPlaceStatus = wasPlaced && RESET_PLACE_STATUS_ON_MOVE;

                    result.FromPalletNo = sourcePalletNo;

                    // 2) เตรียม Pallet ปลายทาง - แยกกรณี "สร้างใหม่" กับ "ของเดิม"
                    PalletInfo sourcePallet;
                    PalletInfo destPallet;

                    if (isNewPallet)
                    {
                        // Lock ต้นทางอย่างเดียวก่อน (ปลายทางยังไม่มีตัวตน ไม่ต้อง Lock)
                        sourcePallet = PalletProvisioning.LockPallet(conn, tran, sourcePalletNo);

                        // สร้าง Pallet ใหม่ + จอง Area ให้อัตโนมัติ (Logic เดียวกับตอน Optimize)
                        // MaxCapacity คำนวณจาก Customer ของ Carton ที่กำลังย้าย (Local
                        // Variable เท่านั้น ไม่มี State ค้าง)
                        int maxCapacity = PalletProvisioning.GetMaxCapacity(cartonCustomer);
                        destPallet = PalletProvisioning.CreateNewPallet(conn, tran, cartonGroupKey, groupType, maxCapacity);

                        result.NewPalletNo = destPallet.PalletNo;
                        result.NewAreaNo = destPallet.AreaNo;
                        result.ToPalletNo = destPallet.PalletNo; // ใช้ PalletNo จริงแทน Sentinel ในผลลัพธ์
                    }
                    else
                    {
                        // Lock ทั้งสอง Pallet ตามลำดับ PalletNo น้อย -> มาก เสมอ กัน Deadlock
                        string firstLock = string.CompareOrdinal(sourcePalletNo, destinationPalletNo) < 0
                            ? sourcePalletNo : destinationPalletNo;
                        string secondLock = string.CompareOrdinal(sourcePalletNo, destinationPalletNo) < 0
                            ? destinationPalletNo : sourcePalletNo;

                        var firstPallet = PalletProvisioning.LockPallet(conn, tran, firstLock);
                        var secondPallet = PalletProvisioning.LockPallet(conn, tran, secondLock);

                        sourcePallet = firstLock == sourcePalletNo ? firstPallet : secondPallet;
                        destPallet = firstLock == destinationPalletNo ? firstPallet : secondPallet;

                        if (destPallet == null)
                        {
                            result.Message = $"ไม่พบ Pallet ปลายทาง '{destinationPalletNo}'";
                            tran.Rollback();
                            return result;
                        }

                        // 3) Validate เงื่อนไขซ้ำฝั่ง Backend (ห้ามเชื่อ Client เฉยๆ)
                        if (!string.Equals(destPallet.GroupKey, cartonGroupKey, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Message = "Pallet ปลายทางไม่ตรงกับ Warehouse/FinalDest ของ Carton นี้";
                            tran.Rollback();
                            return result;
                        }

                        if (destPallet.CartonCount >= destPallet.MaxCapacity)
                        {
                            result.Message = "Pallet ปลายทางเต็มแล้ว กรุณาเลือกใบอื่น";
                            tran.Rollback();
                            return result;
                        }
                    }

                    // 4) ย้าย: Update PalletNo + SeqInPallet ใหม่ของ Carton
                    //    (ถ้าเดิม PLACED จะ Reset PlaceStatus กลับเป็น RESERVED ให้ด้วย)
                    int newSeq = destPallet.CartonCount + 1;
                    UpdateCartonPallet(conn, tran, barcodeCarton, destPallet.PalletNo, newSeq, shouldResetPlaceStatus);

                    // 5) Renumber SeqInPallet ของ Pallet ต้นทางให้ต่อเนื่อง (1..N)
                    RenumberSourcePallet(conn, tran, sourcePalletNo);

                    // 6) Update Pallet ต้นทาง - ถ้าว่างสนิทหลังย้าย (Carton ใบสุดท้าย
                    //    ถูกย้ายออก) ให้ Close + คืน Warehouse Area ทันที เพื่อให้
                    //    Pallet ใบนี้ถูก Recycle ไปใช้กับ DocNo/GroupKey ไหนก็ได้ใน
                    //    รอบ Optimize ถัดไป ไม่ต้องรอ GroupKey เดิมมาเติมอีกต่อไป
                    int sourceRemainingCount = sourcePallet.CartonCount - 1;
                    if (sourceRemainingCount <= 0)
                    {
                        PalletProvisioning.ClosePalletAndReleaseArea(conn, tran, sourcePalletNo, sourcePallet.CurrentAreaNo);
                        result.SourcePalletClosed = true;
                    }
                    else
                    {
                        PalletProvisioning.UpdatePalletCount(conn, tran, sourcePalletNo, sourceRemainingCount, sourcePallet.MaxCapacity);
                    }

                    // Update Pallet ปลายทาง
                    PalletProvisioning.UpdatePalletCount(conn, tran, destPallet.PalletNo, newSeq, destPallet.MaxCapacity);

                    // 7) บันทึก Log การย้าย
                    InsertMoveLog(conn, tran, barcodeCarton, sourcePalletNo, destPallet.PalletNo, moveBy);

                    tran.Commit();

                    result.Success = true;
                    result.Message = $"ย้าย Carton '{barcodeCarton}' จาก {sourcePalletNo} ไป {destPallet.PalletNo} สำเร็จ";
                }
                else
                {
                    result.Message = "ไม่สามารถเชื่อมต่อฐานข้อมูล YPT_Innovation ได้";
                }
            }
            catch (Exception ex)
            {
                tran?.Rollback();
                result.Success = false;
                result.Message = "Move Carton Failed : " + ex.Message;
                App.DialogService.ShowMessageAsync("Failed", result.Message);
                //MessageBox.Show(result.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return result;
        }

        // =====================================================================
        // Helper: หา Pallet ปัจจุบันของ Carton + Lock แถวไว้ (UPDLOCK) พร้อม GroupType
        // =====================================================================
        private string GetCurrentPalletNoForUpdate(
            SqlConnection conn, SqlTransaction tran, string barcodeCarton,
            out string placeStatus, out string groupKey, out string groupType, out string customer)
        {
            placeStatus = null;
            groupKey = null;
            groupType = null;
            customer = null;
            string palletNo = null;

            string sql = @"
                SELECT pd.PalletNo, pd.PlaceStatus, pd.Customer, p.GroupKey, p.GroupType
                FROM dbo.FG_PalletDetail pd WITH (UPDLOCK, ROWLOCK)
                JOIN dbo.FG_Pallet p ON pd.PalletNo = p.PalletNo
                WHERE pd.BarcodeCarton = @BarcodeCarton";

            SqlCommand cmd = new SqlCommand(sql, conn, tran);
            cmd.Parameters.AddWithValue("@BarcodeCarton", barcodeCarton);

            SqlDataReader reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                palletNo = reader["PalletNo"] as string;
                placeStatus = reader["PlaceStatus"] as string;
                customer = reader["Customer"] as string;
                groupKey = reader["GroupKey"] as string;
                groupType = reader["GroupType"] as string;
            }
            reader.Close();

            return palletNo;
        }

        // =====================================================================
        // Helper: ย้าย Carton ไปยัง Pallet ปลายทาง
        // =====================================================================
        // =====================================================================
        // Helper: ย้าย Carton ไปยัง Pallet ปลายทาง - ถ้า resetPlaceStatus = true
        // (แปลว่าเดิม PLACED) จะ Reset PlaceStatus กลับเป็น RESERVED ด้วย เพราะ
        // สถานะ "วางแล้ว" ผูกกับตำแหน่งเดิม ต้องรอ Confirm ใหม่ที่ตำแหน่งใหม่
        // =====================================================================
        private void UpdateCartonPallet(
            SqlConnection conn, SqlTransaction tran, string barcodeCarton, string newPalletNo, int newSeq, bool resetPlaceStatus)
        {
            string sql = resetPlaceStatus
                ? @"
                UPDATE dbo.FG_PalletDetail
                SET PalletNo = @NewPalletNo,
                    SeqInPallet = @NewSeq,
                    PlaceStatus = 'RESERVED'
                WHERE BarcodeCarton = @BarcodeCarton"
                : @"
                UPDATE dbo.FG_PalletDetail
                SET PalletNo = @NewPalletNo,
                    SeqInPallet = @NewSeq
                WHERE BarcodeCarton = @BarcodeCarton";

            SqlCommand cmd = new SqlCommand(sql, conn, tran);
            cmd.Parameters.AddWithValue("@NewPalletNo", newPalletNo);
            cmd.Parameters.AddWithValue("@NewSeq", newSeq);
            cmd.Parameters.AddWithValue("@BarcodeCarton", barcodeCarton);
            cmd.ExecuteNonQuery();
        }

        // =====================================================================
        // Helper: Renumber SeqInPallet ของ Pallet ต้นทางให้ต่อเนื่อง 1..N
        // (นับเฉพาะ Carton ที่ยัง Active คือ ReleasedDate IS NULL AND CancelledDate IS NULL)
        // =====================================================================
        private void RenumberSourcePallet(SqlConnection conn, SqlTransaction tran, string sourcePalletNo)
        {
            string sql = @"
                ;WITH Ordered AS (
                    SELECT BarcodeCarton,
                           ROW_NUMBER() OVER (ORDER BY SeqInPallet ASC) AS NewSeq
                    FROM dbo.FG_PalletDetail
                    WHERE PalletNo = @SourcePalletNo
                      AND ReleasedDate IS NULL
                      AND CancelledDate IS NULL
                )
                UPDATE pd
                SET pd.SeqInPallet = o.NewSeq
                FROM dbo.FG_PalletDetail pd
                JOIN Ordered o ON pd.BarcodeCarton = o.BarcodeCarton";

            SqlCommand cmd = new SqlCommand(sql, conn, tran);
            cmd.Parameters.AddWithValue("@SourcePalletNo", sourcePalletNo);
            cmd.ExecuteNonQuery();
        }

        // =====================================================================
        // Helper: บันทึก Log การย้าย
        // =====================================================================
        private void InsertMoveLog(
            SqlConnection conn, SqlTransaction tran, string barcodeCarton,
            string fromPalletNo, string toPalletNo, string moveBy)
        {
            string sql = @"
                INSERT INTO dbo.FG_PalletMoveLog (BarcodeCarton, FromPalletNo, ToPalletNo, MoveDate, MoveBy)
                VALUES (@BarcodeCarton, @FromPalletNo, @ToPalletNo, GETDATE(), @MoveBy)";

            SqlCommand cmd = new SqlCommand(sql, conn, tran);
            cmd.Parameters.AddWithValue("@BarcodeCarton", barcodeCarton);
            cmd.Parameters.AddWithValue("@FromPalletNo", fromPalletNo);
            cmd.Parameters.AddWithValue("@ToPalletNo", toPalletNo);
            cmd.Parameters.AddWithValue("@MoveBy", (object)moveBy ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }











    /// <summary>
    /// Logic ร่วมสำหรับ "สร้าง Pallet ใหม่ + จอง Warehouse Area" ใช้ร่วมกันทั้ง
    /// PalletOptimizerService (ตอน Optimize เจอ DocNo ใหญ่เกิน MaxCapacity หรือ Best-fit
    /// หา Pallet เดิมไม่เจอ) และ CartonMoveService (ตอน User เลือก "สร้าง Pallet ใหม่"
    /// จากหน้า Move Carton) - แยกออกมาเป็น Static Class เพื่อไม่ให้ Logic เพี้ยนกัน
    /// ระหว่างสองที่ที่เรียกใช้
    ///
    /// ทุก Method รับ Connection/Transaction ที่เปิดอยู่แล้วมาใช้ต่อ (ไม่เปิด/ปิด
    /// Connection เอง) เพื่อให้ผู้เรียกควบคุม Transaction ได้เอง
    /// </summary>
    internal static class PalletProvisioning
    {
        public const int MAX_PALLET_NUMBER = 999;

        // =====================================================================
        // คำนวณ MaxCapacity ตาม Customer - Pure Function ไม่มี State ใดๆ
        // เรียกได้จากกี่ Thread/Call พร้อมกันก็ปลอดภัย (ต่างจาก Static Field เดิม
        // ที่เคยมีปัญหา Default 0 และ Race Condition)
        //
        // Field Customer จริงในฐานข้อมูลเก็บเป็น "OD" (ไม่ใช่ "ODLO" เต็มๆ)
        // =====================================================================
        public static int GetMaxCapacity(string customer)
        {
            return string.Equals(customer?.Trim(), "OD", StringComparison.OrdinalIgnoreCase)
                ? 30
                : 35;
        }

        // =====================================================================
        // สร้าง Pallet ใหม่ - ลอง Recycle Pallet ที่ CLOSED ก่อนเสมอ (ไม่สนใจ
        // GroupKey/GroupType เดิมของ Pallet นั้น เพราะ DocNo ใหม่ต้องวางได้เสมอ)
        // ถ้าไม่มี Pallet ให้ Recycle ค่อย Generate PalletNo ใหม่ตามปกติ
        // พร้อมจอง Warehouse Area ที่ว่างใบแรกให้ทันที (1 Pallet : 1 Area)
        // =====================================================================
        public static PalletInfo CreateNewPallet(
            SqlConnection conn, SqlTransaction tran, string groupKey, string groupType, int maxCapacity)
        {
            // 1) ลอง Recycle Pallet ที่ CLOSED อยู่ก่อนเสมอ
            var recycled = TryRecycleClosedPallet(conn, tran, groupKey, groupType, maxCapacity);
            if (recycled != null)
                return recycled;

            // 2) ไม่มี Pallet ให้ Recycle -> สร้างใหม่ด้วย PalletNo ถัดไป
            string newPalletNo = GenerateNextPalletNo(conn, tran);

            // จอง Area ก่อน Insert Pallet เพื่อเก็บ AreaNo ลงไปในแถวเดียวกันได้เลย
            // (groupKey ส่งไปแค่เพื่อเก็บ Trace/Log เท่านั้น ไม่ใช้ในการค้นหา/กรอง Area)
            string areaNo = ReserveWarehouseArea(conn, tran, newPalletNo);

            string insertSql = @"
                INSERT INTO dbo.FG_Pallet (PalletNo, GroupKey, GroupType, CartonCount, MaxCapacity, PalletStatus, AreaNo, CreateDate)
                VALUES (@PalletNo, @GroupKey, @GroupType, 0, @MaxCapacity, 'OPEN', @AreaNo, GETDATE())";

            SqlCommand cmd = new SqlCommand(insertSql, conn, tran);
            cmd.Parameters.AddWithValue("@PalletNo", newPalletNo);
            cmd.Parameters.AddWithValue("@GroupKey", groupKey);
            cmd.Parameters.AddWithValue("@GroupType", groupType);
            cmd.Parameters.AddWithValue("@MaxCapacity", maxCapacity);
            cmd.Parameters.AddWithValue("@AreaNo", (object)areaNo ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            return new PalletInfo
            {
                PalletNo = newPalletNo,
                GroupKey = groupKey,
                CartonCount = 0,
                MaxCapacity = maxCapacity,
                AreaNo = areaNo // ใช้เป็น Flag บอกว่า Pallet นี้เพิ่งถูกสร้าง/Recycle (ผู้เรียกเช็คได้)
            };
        }

        // =====================================================================
        // ลองหา Pallet ที่ PalletStatus = 'CLOSED' มา Recycle ใช้ใหม่ทั้งใบ
        // (ไม่สนใจ GroupKey/GroupType เดิม เพราะ DocNo ใหม่ต้องใช้ได้เสมอ)
        // ถ้าไม่มี Pallet ที่ CLOSED เลย -> คืน null ให้ผู้เรียกไป Generate ใหม่แทน
        //
        // สำคัญ: ต้องจอง Warehouse Area "ใหม่" เสมอ ห้ามใช้ AreaNo เดิมที่ค้างอยู่
        // ในแถว Pallet ต่อ เพราะตอน Close Pallet เราคืน Area นั้นเป็น Ready ไปแล้ว
        // อาจมี Pallet ใบอื่นมาจอง Area เดิมนี้ไปใช้ต่อแล้วก็ได้
        // =====================================================================
        private static PalletInfo TryRecycleClosedPallet(
            SqlConnection conn, SqlTransaction tran, string groupKey, string groupType, int maxCapacity)
        {
            string selectSql = @"
                SELECT TOP 1 PalletNo
                FROM dbo.FG_Pallet WITH (UPDLOCK, ROWLOCK)
                WHERE PalletStatus = 'CLOSED'
                ORDER BY PalletNo ASC";

            string palletNo = null;

            SqlCommand cmd = new SqlCommand(selectSql, conn, tran);
            var scalarResult = cmd.ExecuteScalar();
            if (scalarResult != null)
                palletNo = scalarResult.ToString();

            if (palletNo == null)
                return null; // ไม่มี Pallet ให้ Recycle

            // จอง Area ใหม่ให้ Pallet ใบนี้ (Area เดิมอาจถูกคนอื่นเอาไปใช้แล้ว)
            string areaNo = ReserveWarehouseArea(conn, tran, palletNo);

            string updateSql = @"
                UPDATE dbo.FG_Pallet
                SET GroupKey = @GroupKey,
                    GroupType = @GroupType,
                    CartonCount = 0,
                    MaxCapacity = @MaxCapacity,
                    PalletStatus = 'OPEN',
                    AreaNo = @AreaNo,
                    UpdateDate = GETDATE()
                WHERE PalletNo = @PalletNo";

            SqlCommand cmd2 = new SqlCommand(updateSql, conn, tran);
            cmd2.Parameters.AddWithValue("@GroupKey", groupKey);
            cmd2.Parameters.AddWithValue("@GroupType", groupType);
            cmd2.Parameters.AddWithValue("@MaxCapacity", maxCapacity);
            cmd2.Parameters.AddWithValue("@AreaNo", (object)areaNo ?? DBNull.Value);
            cmd2.Parameters.AddWithValue("@PalletNo", palletNo);
            cmd2.ExecuteNonQuery();

            return new PalletInfo
            {
                PalletNo = palletNo,
                GroupKey = groupKey,
                CartonCount = 0,
                MaxCapacity = maxCapacity,
                AreaNo = areaNo // ใช้เป็น Flag บอกว่า Pallet นี้เพิ่งถูก Recycle (เหมือนสร้างใหม่)
            };
        }

        // =====================================================================
        // จอง Warehouse Area ที่ว่าง (Status = 'Ready') ใบแรกตาม AreaNo ASC
        // ให้กับ Pallet ที่เพิ่งสร้างใหม่
        // =====================================================================
        public static string ReserveWarehouseArea(
            SqlConnection conn, SqlTransaction tran, string palletNo)
        {
            // Lock แถวที่จะจองไว้ก่อน กัน Process อื่นแย่ง Area เดียวกันพร้อมกัน
            string selectSql = @"
                SELECT TOP 1 AreaNo
                FROM dbo.FG_Warehouse_Area WITH (UPDLOCK, ROWLOCK)
                WHERE Status = 'Ready'
                ORDER BY AreaName ASC";

            string areaNo = null;

            SqlCommand cmd = new SqlCommand(selectSql, conn, tran);
            var scalarResult = cmd.ExecuteScalar();
            if (scalarResult != null)
                areaNo = scalarResult.ToString();

            if (areaNo == null)
            {
                // ต่างจาก Pallet Number ตรงนี้ "ไม่การันตีว่าพอเสมอ" ต้องแจ้ง Error ให้ชัดเจน
                throw new InvalidOperationException(
                    "ไม่มี Warehouse Area ว่าง (Status='Ready') เหลือในระบบ " +
                    "กรุณาตรวจสอบพื้นที่คงเหลือใน FG_Warehouse_Area");
            }

            string updateSql = @"
                UPDATE dbo.FG_Warehouse_Area
                SET Status = 'Reserved',
                    InputDate = GETDATE(),
                    PalletNo = @PalletNo
                WHERE AreaNo = @AreaNo";

            SqlCommand cmd2 = new SqlCommand(updateSql, conn, tran);
            cmd2.Parameters.AddWithValue("@PalletNo", palletNo);
            cmd2.Parameters.AddWithValue("@AreaNo", areaNo);
            cmd2.ExecuteNonQuery();

            return areaNo;
        }

        // =====================================================================
        // Generate หมายเลข Pallet ถัดไป (PL001 - PL999)
        // =====================================================================
        public static string GenerateNextPalletNo(SqlConnection conn, SqlTransaction tran)
        {
            // Lock ทั้ง Table ระหว่างอ่าน Max เพื่อกันเลขซ้ำเวลามีหลาย Process แย่งสร้าง Pallet ใหม่พร้อมกัน
            string sql = @"
                SELECT ISNULL(MAX(CAST(SUBSTRING(PalletNo, 3, 3) AS INT)), 0)
                FROM dbo.FG_Pallet WITH (TABLOCKX, HOLDLOCK)";

            SqlCommand cmd = new SqlCommand(sql, conn, tran);
            int maxNo = (int)cmd.ExecuteScalar();

            int nextNo = maxNo + 1;

            if (nextNo > MAX_PALLET_NUMBER)
            {
                // จุดนี้ควรแทบไม่มีทางถูกเรียกถึงในทางปฏิบัติแล้ว เพราะ CreateNewPallet()
                // จะพยายาม Recycle Pallet ที่ CLOSED ก่อนเสมอ (ดู TryRecycleClosedPallet)
                // ถ้ามาถึงจุดนี้จริง แปลว่า Pallet ทั้ง 999 ใบ OPEN/FULL พร้อมกันหมด
                // ไม่มีใบไหน CLOSED เลย - ต้องตรวจสอบว่า CartonReleaseService ทำงาน
                // ถูกต้องหรือไม่ หรือมี Stock ค้างในระบบเยอะผิดปกติจริงๆ
                throw new InvalidOperationException(
                    "Pallet Number เกิน PL999 และไม่มี Pallet CLOSED ให้ Recycle เลย " +
                    "กรุณาตรวจสอบสถานะ Pallet ทั้งหมดในระบบ");
            }

            return "PL" + nextNo.ToString("D3");
        }

        // =====================================================================
        // Update CartonCount/PalletStatus - ใช้ร่วมกันทั้ง Optimize/Move/Release
        // =====================================================================
        public static void UpdatePalletCount(
            SqlConnection conn, SqlTransaction tran, string palletNo, int newCount, int maxCapacity)
        {
            string sql = @"
                UPDATE dbo.FG_Pallet
                SET CartonCount = @Count,
                    PalletStatus = CASE WHEN @Count >= @MaxCapacity THEN 'FULL' ELSE 'OPEN' END,
                    UpdateDate = GETDATE()
                WHERE PalletNo = @PalletNo";

            SqlCommand cmd = new SqlCommand(sql, conn, tran);
            cmd.Parameters.AddWithValue("@Count", newCount);
            cmd.Parameters.AddWithValue("@MaxCapacity", maxCapacity);
            cmd.Parameters.AddWithValue("@PalletNo", palletNo);
            cmd.ExecuteNonQuery();
        }

        // =====================================================================
        // Lock 1 Pallet แถวเดียว พร้อมดึงข้อมูลปัจจุบันมาด้วย (รวม AreaNo)
        // =====================================================================
        public static PalletInfo LockPallet(SqlConnection conn, SqlTransaction tran, string palletNo)
        {
            string sql = @"
                SELECT PalletNo, GroupKey, CartonCount, MaxCapacity, AreaNo
                FROM dbo.FG_Pallet WITH (UPDLOCK, ROWLOCK)
                WHERE PalletNo = @PalletNo";

            SqlCommand cmd = new SqlCommand(sql, conn, tran);
            cmd.Parameters.AddWithValue("@PalletNo", palletNo);

            SqlDataReader reader = cmd.ExecuteReader();
            PalletInfo info = null;
            if (reader.Read())
            {
                info = new PalletInfo
                {
                    PalletNo = reader["PalletNo"] as string,
                    GroupKey = reader["GroupKey"] as string,
                    CartonCount = Convert.ToInt32(reader["CartonCount"]),
                    MaxCapacity = Convert.ToInt32(reader["MaxCapacity"]),
                    CurrentAreaNo = reader["AreaNo"] as string
                    // หมายเหตุ: ตั้งใจไม่ Set Field "AreaNo" ตรงนี้ (ปล่อยเป็น null)
                    // เพราะใช้ AreaNo != null เป็น Flag บอกว่า "Pallet เพิ่งถูกสร้างใหม่"
                    // ใน CreateNewPallet() - ถ้าต้องการ Area จริงของ Pallet ที่มีอยู่แล้ว
                    // ให้ใช้ CurrentAreaNo แทน
                };
            }
            reader.Close();

            return info;
        }

        // =====================================================================
        // Pallet ว่างสนิท (CartonCount = 0) -> Close Pallet + คืน Warehouse Area
        // กลับเป็น Ready ทันที เพื่อให้ TryRecycleClosedPallet เอาไปใช้กับ
        // GroupKey/DocNo ไหนก็ได้ในรอบ Optimize/MoveCarton ถัดไป
        //
        // ใช้ร่วมกันทั้ง CartonReleaseService (ของออกจากโกดังจริงเพราะมี Invoice)
        // และ CartonMoveService (ย้าย Carton ใบสุดท้ายออกจน Pallet ว่าง) - ทั้งสอง
        // กรณีถือว่า Pallet "ว่างพร้อมใช้งานใหม่" เหมือนกัน จึงใช้ Logic เดียวกัน
        // =====================================================================
        public static void ClosePalletAndReleaseArea(
            SqlConnection conn, SqlTransaction tran, string palletNo, string areaNo)
        {
            string closePalletSql = @"
                UPDATE dbo.FG_Pallet
                SET CartonCount = 0,
                    PalletStatus = 'CLOSED',
                    UpdateDate = GETDATE()
                WHERE PalletNo = @PalletNo";

            SqlCommand cmd = new SqlCommand(closePalletSql, conn, tran);
            cmd.Parameters.AddWithValue("@PalletNo", palletNo);
            cmd.ExecuteNonQuery();

            if (!string.IsNullOrWhiteSpace(areaNo))
            {
                string releaseAreaSql = @"
                    UPDATE dbo.FG_Warehouse_Area
                    SET Status = 'Ready',
                        PalletNo = NULL,
                        InputDate = NULL
                    WHERE AreaNo = @AreaNo";

                SqlCommand cmd2 = new SqlCommand(releaseAreaSql, conn, tran);
                cmd2.Parameters.AddWithValue("@AreaNo", areaNo);
                cmd2.ExecuteNonQuery();
            }
        }
    }






    /// <summary>
    /// Service คืนพื้นที่ Pallet/Warehouse Area เมื่อ Carton ถูกตัด Stock (มี InvoiceNo)
    /// จริงและออกจากโกดังไปแล้ว
    ///
    /// Entry Point หลักคือ SyncAndReleaseInvoicedCartons() - เรียกจากปุ่มบนหน้าจอ
    /// Warehouse Area (Lock สิทธิ์ผู้ใช้ที่กดได้ที่ฝั่ง UI) แบ่งการทำงานเป็น 2 Step:
    ///   1) SyncInvoiceFromERP() - ดึง InvoiceNo จากต้นทางจริง
    ///      (Netgarment.dbo.Scan_Order_InoutStatus) มาใส่ FG_PalletDetail.InvoiceNo
    ///      เฉพาะ Carton ที่ยังไม่เคย Sync
    ///   2) GetPendingReleaseBarcodes() + ReleaseCartonsFromPallet()
    ///      Release "ทุก Carton ที่มี InvoiceNo แต่ ReleasedDate ยังเป็น NULL"
    ///      ไม่ใช่เฉพาะที่เพิ่ง Sync รอบนี้
    ///
    /// *** จุดที่แก้จากเวอร์ชันเดิม (สาเหตุ InvoiceNo มีค่าแต่ ReleasedDate เป็น NULL) ***
    ///   เดิม Step 2 รับเฉพาะ Barcode ที่เพิ่ง Sync ในรอบนั้น ถ้า Release ล้มเหลว
    ///   (Timeout / Deadlock / Rollback / ไม่พบ Pallet) InvoiceNo ถูก Commit ไปแล้ว
    ///   แต่ Sync รอบถัดไปกรอง InvoiceNo IS NULL จึงไม่เก็บ Carton เหล่านั้นอีกเลย
    ///   -> ค้างถาวร  เวอร์ชันนี้ใช้ "สถานะในตาราง" เป็นตัวตัดสิน (InvoiceNo NOT NULL
    ///   AND ReleasedDate IS NULL) จึงซ่อมตัวเองได้ทุกครั้งที่กดปุ่ม
    ///
    /// การปรับปรุงอื่น:
    ///   - แบ่ง Release เป็นก้อนละไม่เกิน MaxCartonsPerTransaction (ไม่ผ่าครึ่ง Pallet)
    ///     ก้อนใดพัง Rollback เฉพาะก้อนนั้น ก้อนอื่นยังสำเร็จ
    ///   - ReadCommitted + UPDLOCK แทน Serializable และ Lock Pallet ก่อน Carton
    ///     (ลำดับเดียวกับ CartonMoveService กัน Deadlock)
    ///   - Query แบบ Set-based ต่อ Pallet แทนยิงทีละ Carton
    ///   - CommandTimeout 180 วินาที, Retry อัตโนมัติเมื่อเป็น Deadlock (1205)
    ///   - sp_getapplock กันกดซ้อนกันข้ามเครื่อง
    ///   - เขียน Log ลงไฟล์ และสรุปผลที่ LastRunSummary
    ///   - ไม่พบ Pallet -> ยัง Set ReleasedDate ให้ (ของออกไปแล้วจริง) แต่ Log Warning
    ///
    /// ไม่สนใจ PlaceStatus (RESERVED/PLACED) เลย เพราะบางงานอาจเป็นงานด่วนที่ไม่ผ่าน
    /// Warehouse ก็ได้ - แค่มี InvoiceNo ถือว่าออกจากระบบแล้ว
    ///
    /// เงื่อนไขคืนพื้นที่: ถ้า Pallet ว่างสนิทหลัง Release จะ Close Pallet
    /// และคืน Warehouse Area กลับเป็น Ready ทันที
    /// </summary>
    public class CartonReleaseService
    {
        private const int CommandTimeoutSeconds = 180;
        private const int MaxCartonsPerTransaction = 300;
        private const int MaxInClauseSize = 500;
        private const int MaxDeadlockRetries = 3;
        private const string AppLockResource = "AutoScan_Weight.CartonRelease";

        // เงื่อนไข "มี Invoice จริง" ใช้ร่วมกันทุกที่ให้ตรงกัน
        private const string HasInvoiceFilter = "InvoiceNo IS NOT NULL AND LTRIM(RTRIM(InvoiceNo)) <> ''";

        private static readonly object _logLock = new object();

        private sealed class PalletReleaseGroup
        {
            public string PalletNo;
            public List<string> Barcodes;
        }

        private sealed class CartonRow
        {
            public string PalletNo;
            public string InvoiceNo;
            public DateTime? ReleasedDate;
            public DateTime? CancelledDate;
        }

        /// <summary>สรุปผลการทำงานรอบล่าสุด (ให้ UI เอาไปแสดงหลังกดปุ่ม)</summary>
        public string LastRunSummary { get; private set; } = string.Empty;

        // =====================================================================
        // ENTRY POINT (Async): เรียกจากปุ่มบนหน้าจอโดยไม่บล็อก UI Thread
        // =====================================================================
        public Task<List<ReleaseCartonResult>> SyncAndReleaseInvoicedCartonsAsync()
        {
            return Task.Run(() => SyncAndReleaseInvoicedCartons());
        }

        // =====================================================================
        // ENTRY POINT: Sync InvoiceNo จาก ERP แล้ว Release ทุก Carton ที่ค้าง
        // =====================================================================
        public List<ReleaseCartonResult> SyncAndReleaseInvoicedCartons()
        {
            var results = new List<ReleaseCartonResult>();
            LastRunSummary = string.Empty;

            // Connection นี้ใช้ถือ Application Lock อย่างเดียวตลอดการทำงาน
            ConnectSQL guard = new ConnectSQL();
            bool locked = false;

            try
            {
                if (!guard.YPTInnovation())
                {
                    LastRunSummary = "ไม่สามารถเชื่อมต่อฐานข้อมูล YPT_Innovation ได้";
                    Log("ERROR", LastRunSummary);
                    App.DialogService.ShowMessageAsync("Failed", LastRunSummary);
                    return results;
                }

                locked = TryAcquireAppLock(guard.conn);
                if (!locked)
                {
                    LastRunSummary = "มีผู้ใช้อื่นกำลัง Sync/Release อยู่ กรุณารอสักครู่แล้วลองใหม่";
                    Log("WARN", LastRunSummary);
                    App.DialogService.ShowMessageAsync("Info", LastRunSummary);
                    return results;
                }

                // STEP 1: Sync InvoiceNo (ถ้าล้มเหลวยังไปต่อ Step 2 เพื่อเก็บตกของเก่าที่ค้าง)
                int synced = 0;
                string syncError = null;
                try
                {
                    synced = SyncInvoiceFromERP();
                }
                catch (Exception ex)
                {
                    syncError = ex.Message;
                    Log("ERROR", "Sync Invoice Failed : " + ex);
                }

                // STEP 2: Release ทุกใบที่มี Invoice แต่ยังไม่ Release (รวมของค้างจากรอบก่อนๆ)
                List<string> pending = GetPendingReleaseBarcodes();
                if (pending.Count > 0)
                    results = ReleaseCartonsFromPallet(pending);

                LastRunSummary = BuildSummary(synced, pending.Count, results, syncError);
                Log("INFO", LastRunSummary);

                var failures = results.Where(r => !r.Success).ToList();
                if (syncError != null || failures.Count > 0)
                {
                    var sb = new StringBuilder(LastRunSummary);
                    if (syncError != null)
                        sb.AppendLine().Append("Sync Error : ").Append(syncError);
                    foreach (var f in failures.Take(5))
                        sb.AppendLine().Append("- ").Append(f.BarcodeCarton).Append(" : ").Append(f.Message);
                    if (failures.Count > 5)
                        sb.AppendLine().Append("... และอีก ").Append(failures.Count - 5).Append(" รายการ (ดูใน Log)");

                    App.DialogService.ShowMessageAsync("Warning", sb.ToString());
                }
            }
            catch (Exception ex)
            {
                LastRunSummary = "Sync/Release Failed : " + ex.Message;
                Log("ERROR", ex.ToString());
                App.DialogService.ShowMessageAsync("Failed", LastRunSummary);
            }
            finally
            {
                if (locked)
                    ReleaseAppLock(guard.conn);
                guard.Close_Sql();
            }

            return results;
        }

        // =====================================================================
        // STEP 1: ดึง InvoiceNo จากต้นทางจริง (Netgarment.Scan_Order_InoutStatus)
        // มาใส่ FG_PalletDetail.InvoiceNo เฉพาะ Carton ที่ยังไม่เคย Sync
        // คืนค่าจำนวนแถวที่ Sync ใหม่ในรอบนี้ (Throw ถ้าผิดพลาด)
        // =====================================================================
        private int SyncInvoiceFromERP()
        {
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (!connectSQL.YPTInnovation())
                    throw new InvalidOperationException("ไม่สามารถเชื่อมต่อฐานข้อมูล YPT_Innovation ได้");

                string sql = @"
                    UPDATE a
                    SET a.InvoiceNo = b.InvoiceNo
                    FROM dbo.FG_PalletDetail a
                    INNER JOIN Netgarment.dbo.Scan_Order_InoutStatus b
                        ON a.BarcodeCarton = b.BarcodeCarton
                    WHERE b.InvoiceNo IS NOT NULL
                      AND LTRIM(RTRIM(b.InvoiceNo)) <> ''
                      AND a.InvoiceNo IS NULL
                      AND a.CancelledDate IS NULL";   // Carton ที่ถูกยกเลิก/ลบออกแล้ว ไม่รับ Invoice

                using (SqlCommand cmd = NewCommand(sql, connectSQL.conn, null))
                {
                    return cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                connectSQL.Close_Sql();
            }
        }

        // =====================================================================
        // หา Carton ทั้งหมดที่ "มี InvoiceNo แต่ยังไม่ Release" (ตัวเก็บตกของค้าง)
        // =====================================================================
        private List<string> GetPendingReleaseBarcodes()
        {
            var list = new List<string>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (!connectSQL.YPTInnovation())
                    throw new InvalidOperationException("ไม่สามารถเชื่อมต่อฐานข้อมูล YPT_Innovation ได้");

                string sql = $@"
                    SELECT BarcodeCarton
                    FROM dbo.FG_PalletDetail
                    WHERE {HasInvoiceFilter}
                      AND ReleasedDate IS NULL
                      AND CancelledDate IS NULL
                      AND BarcodeCarton IS NOT NULL";

                using (SqlCommand cmd = NewCommand(sql, connectSQL.conn, null))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(reader.GetString(0));
                }
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return list;
        }

        // =====================================================================
        // STEP 2: Release Carton หลายใบ (Public - Caller อื่นส่ง Barcode มาเองได้)
        //   1) หา PalletNo ของแต่ละ Carton
        //   2) Group ตาม Pallet (เรียง PalletNo) แล้วแบ่งเป็นก้อน ไม่ผ่าครึ่ง Pallet
        //   3) แต่ละก้อน = 1 Transaction ถ้าพัง Rollback เฉพาะก้อนนั้น (Retry เมื่อ Deadlock)
        // =====================================================================
        public List<ReleaseCartonResult> ReleaseCartonsFromPallet(List<string> barcodeCartons)
        {
            var results = new List<ReleaseCartonResult>();

            if (barcodeCartons == null || barcodeCartons.Count == 0)
                return results;

            var sortedBarcodes = barcodeCartons
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(b => b, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sortedBarcodes.Count == 0)
                return results;

            // 1) Barcode -> PalletNo
            Dictionary<string, string> palletOf;
            try
            {
                palletOf = ResolvePallets(sortedBarcodes);
            }
            catch (Exception ex)
            {
                Log("ERROR", "Resolve Pallet Failed : " + ex);
                foreach (var barcode in sortedBarcodes)
                    results.Add(Fail(barcode, null, "Release Carton Failed : " + ex.Message));
                return results;
            }

            foreach (var barcode in sortedBarcodes.Where(b => !palletOf.ContainsKey(b)))
                results.Add(Fail(barcode, null, "ไม่พบ Carton นี้ในระบบ"));

            // 2) Group ตาม Pallet แล้วแบ่งก้อน
            var groups = palletOf
                .GroupBy(kv => kv.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new PalletReleaseGroup
                {
                    PalletNo = g.Key,
                    Barcodes = g.Select(x => x.Key)
                                .OrderBy(b => b, StringComparer.OrdinalIgnoreCase)
                                .ToList()
                })
                .ToList();

            var batches = new List<List<PalletReleaseGroup>>();
            var current = new List<PalletReleaseGroup>();
            int currentCount = 0;
            foreach (var g in groups)
            {
                if (current.Count > 0 && currentCount + g.Barcodes.Count > MaxCartonsPerTransaction)
                {
                    batches.Add(current);
                    current = new List<PalletReleaseGroup>();
                    currentCount = 0;
                }
                current.Add(g);
                currentCount += g.Barcodes.Count;
            }
            if (current.Count > 0)
                batches.Add(current);

            // 3) Release ทีละก้อน
            foreach (var batch in batches)
                results.AddRange(ReleaseBatchWithRetry(batch));

            return results;
        }

        // =====================================================================
        // Release 1 ก้อน พร้อม Retry เมื่อเป็น Deadlock (1205)
        // =====================================================================
        private List<ReleaseCartonResult> ReleaseBatchWithRetry(List<PalletReleaseGroup> batch)
        {
            for (int attempt = 1; attempt <= MaxDeadlockRetries; attempt++)
            {
                try
                {
                    return ReleaseBatch(batch);
                }
                catch (SqlException ex) when (ex.Number == 1205 && attempt < MaxDeadlockRetries)
                {
                    Log("WARN", $"Deadlock ครั้งที่ {attempt} จะลองใหม่ (Pallet {batch[0].PalletNo}...)");
                    System.Threading.Thread.Sleep(500 * attempt);
                }
                catch (Exception ex)
                {
                    Log("ERROR", $"Release Batch Failed (Pallet {batch[0].PalletNo}...{batch[batch.Count - 1].PalletNo}) : {ex}");
                    return FailBatch(batch, "Release Carton Failed : " + ex.Message);
                }
            }

            return FailBatch(batch, "Release Carton Failed : เกินจำนวนครั้งที่ลองใหม่");
        }

        // =====================================================================
        // Release 1 ก้อน = 1 Transaction (Pallet ถูก Lock ก่อน Carton เสมอ)
        // คืน Result เมื่อ Commit สำเร็จเท่านั้น ถ้าผิดพลาด Rollback แล้ว Throw
        // =====================================================================
        private List<ReleaseCartonResult> ReleaseBatch(List<PalletReleaseGroup> batch)
        {
            var results = new List<ReleaseCartonResult>();
            ConnectSQL connectSQL = new ConnectSQL();
            SqlTransaction tran = null;

            try
            {
                if (!connectSQL.YPTInnovation())
                    throw new InvalidOperationException("ไม่สามารถเชื่อมต่อฐานข้อมูล YPT_Innovation ได้");

                SqlConnection conn = connectSQL.conn;
                tran = conn.BeginTransaction(IsolationLevel.ReadCommitted);

                // batch เรียงตาม PalletNo อยู่แล้ว -> ลำดับการ Lock คงที่ กัน Deadlock
                foreach (var group in batch)
                {
                    // 1) Lock Pallet ก่อน (ลำดับเดียวกับ CartonMoveService)
                    var pallet = string.IsNullOrWhiteSpace(group.PalletNo)
                        ? null
                        : PalletProvisioning.LockPallet(conn, tran, group.PalletNo);

                    // 2) Lock + อ่านสถานะ Carton ทั้งหมดของ Pallet นี้ในคำสั่งเดียว
                    var rows = LockCartonsForRelease(conn, tran, group.Barcodes);

                    var toRelease = new List<string>();
                    foreach (var barcode in group.Barcodes)
                    {
                        if (!rows.TryGetValue(barcode, out CartonRow row))
                        {
                            results.Add(Fail(barcode, group.PalletNo, "ไม่พบ Carton นี้ในระบบ"));
                            continue;
                        }

                        if (row.ReleasedDate != null)
                        {
                            // Idempotent: เคย Release ไปแล้ว ถือว่าสำเร็จ ไม่ต้องทำซ้ำ
                            results.Add(new ReleaseCartonResult
                            {
                                BarcodeCarton = barcode,
                                Success = true,
                                AlreadyReleased = true,
                                Message = "Carton นี้ถูก Release ไปแล้วก่อนหน้า (ข้าม)",
                                PalletNo = group.PalletNo
                            });
                            continue;
                        }

                        if (row.CancelledDate != null)
                        {
                            // ถูกยกเลิก/ลบออกจาก Pallet ไปแล้ว (CartonCount ลดไปแล้ว) ห้าม Release ซ้ำ
                            results.Add(Fail(barcode, group.PalletNo, "Carton นี้ถูกยกเลิก/ลบออกจาก Pallet ไปแล้ว ไม่ Release ซ้ำ"));
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(row.InvoiceNo))
                        {
                            results.Add(Fail(barcode, group.PalletNo, "ยังไม่มี InvoiceNo ยังไม่ถือว่าออกจากระบบ"));
                            continue;
                        }

                        toRelease.Add(barcode);
                    }

                    if (toRelease.Count == 0)
                        continue;

                    // 3) Set ReleasedDate ทั้งกลุ่มแบบ Set-based
                    SetReleasedDate(conn, tran, toRelease);

                    if (pallet == null)
                    {
                        // ข้อมูลไม่สอดคล้อง (Carton ไม่มี Pallet หรือ Pallet ถูกลบ) - ของออกไปแล้วจริง
                        // จึง Release ให้ไม่ให้ค้าง แต่ไม่มี Pallet/Area ให้คืน และ Log ไว้ตรวจสอบ
                        Log("WARN", $"ไม่พบ Pallet '{group.PalletNo}' ของ {toRelease.Count} Carton - Set ReleasedDate แล้วแต่ข้ามการคืนพื้นที่");
                        foreach (var barcode in toRelease)
                        {
                            results.Add(new ReleaseCartonResult
                            {
                                BarcodeCarton = barcode,
                                Success = true,
                                AlreadyReleased = false,
                                Message = $"Release สำเร็จ (ไม่พบ Pallet '{group.PalletNo}' จึงข้ามการคืนพื้นที่)",
                                PalletNo = group.PalletNo
                            });
                        }
                        continue;
                    }

                    var groupResults = new List<ReleaseCartonResult>();
                    foreach (var barcode in toRelease)
                    {
                        groupResults.Add(new ReleaseCartonResult
                        {
                            BarcodeCarton = barcode,
                            Success = true,
                            AlreadyReleased = false,
                            Message = "Release สำเร็จ",
                            PalletNo = group.PalletNo
                        });
                    }

                    // 4) Renumber Carton ที่เหลือ (ยัง Active) ในพัลเลทนั้นให้ต่อเนื่อง
                    RenumberActivePallet(conn, tran, group.PalletNo);

                    // 5) คำนวณ CartonCount ใหม่ + ตัดสินใจว่าต้อง Close Pallet ไหม
                    int newCount = pallet.CartonCount - toRelease.Count;
                    if (newCount < 0) newCount = 0; // กันข้อมูลเพี้ยน

                    if (newCount == 0)
                    {
                        PalletProvisioning.ClosePalletAndReleaseArea(conn, tran, group.PalletNo, pallet.CurrentAreaNo);
                        foreach (var r in groupResults)
                            r.PalletClosed = true;
                    }
                    else
                    {
                        PalletProvisioning.UpdatePalletCount(conn, tran, group.PalletNo, newCount, pallet.MaxCapacity);
                    }

                    results.AddRange(groupResults);
                }

                tran.Commit();
                return results;
            }
            catch
            {
                try { tran?.Rollback(); } catch { /* Connection อาจหลุดไปแล้ว */ }
                throw;
            }
            finally
            {
                connectSQL.Close_Sql();
            }
        }

        // =====================================================================
        // Helper: Barcode -> PalletNo (อ่านอย่างเดียว ไม่ Lock)
        // =====================================================================
        private Dictionary<string, string> ResolvePallets(List<string> barcodes)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (!connectSQL.YPTInnovation())
                    throw new InvalidOperationException("ไม่สามารถเชื่อมต่อฐานข้อมูล YPT_Innovation ได้");

                foreach (var part in Partition(barcodes, MaxInClauseSize))
                {
                    using (SqlCommand cmd = NewCommand(null, connectSQL.conn, null))
                    {
                        string inClause = AddInParameters(cmd, part);
                        cmd.CommandText = "SELECT BarcodeCarton, PalletNo FROM dbo.FG_PalletDetail WHERE BarcodeCarton IN " + inClause;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string barcode = reader["BarcodeCarton"] as string;
                                if (barcode == null) continue;
                                map[barcode] = (reader["PalletNo"] as string) ?? string.Empty;
                            }
                        }
                    }
                }
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return map;
        }

        // =====================================================================
        // Helper: Lock แถว FG_PalletDetail ของ Carton หลายใบ + ดึง InvoiceNo/ReleasedDate
        // =====================================================================
        private Dictionary<string, CartonRow> LockCartonsForRelease(
            SqlConnection conn, SqlTransaction tran, List<string> barcodes)
        {
            var map = new Dictionary<string, CartonRow>(StringComparer.OrdinalIgnoreCase);

            foreach (var part in Partition(barcodes, MaxInClauseSize))
            {
                using (SqlCommand cmd = NewCommand(null, conn, tran))
                {
                    string inClause = AddInParameters(cmd, part);
                    cmd.CommandText = @"
                        SELECT BarcodeCarton, PalletNo, InvoiceNo, ReleasedDate, CancelledDate
                        FROM dbo.FG_PalletDetail WITH (UPDLOCK, ROWLOCK)
                        WHERE BarcodeCarton IN " + inClause;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string barcode = reader["BarcodeCarton"] as string;
                            if (barcode == null) continue;

                            map[barcode] = new CartonRow
                            {
                                PalletNo = reader["PalletNo"] as string,
                                InvoiceNo = reader["InvoiceNo"] as string,
                                ReleasedDate = reader["ReleasedDate"] == DBNull.Value
                                    ? (DateTime?)null
                                    : Convert.ToDateTime(reader["ReleasedDate"]),
                                CancelledDate = reader["CancelledDate"] == DBNull.Value
                                    ? (DateTime?)null
                                    : Convert.ToDateTime(reader["CancelledDate"])
                            };
                        }
                    }
                }
            }

            return map;
        }

        // =====================================================================
        // Helper: Set ReleasedDate ให้ Carton หลายใบ (Idempotency Flag)
        // เช็ค Invoice + ReleasedDate IS NULL ซ้ำอีกชั้น กันกรณีข้อมูลเปลี่ยนระหว่างทาง
        // =====================================================================
        private void SetReleasedDate(SqlConnection conn, SqlTransaction tran, List<string> barcodes)
        {
            foreach (var part in Partition(barcodes, MaxInClauseSize))
            {
                using (SqlCommand cmd = NewCommand(null, conn, tran))
                {
                    string inClause = AddInParameters(cmd, part);
                    cmd.CommandText = $@"
                        UPDATE dbo.FG_PalletDetail
                        SET ReleasedDate = GETDATE()
                        WHERE ReleasedDate IS NULL
                          AND CancelledDate IS NULL
                          AND {HasInvoiceFilter}
                          AND BarcodeCarton IN " + inClause;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // =====================================================================
        // Helper: Renumber SeqInPallet ของ Carton ที่ยัง Active (ReleasedDate IS NULL)
        // ในพัลเลทนั้นให้ต่อเนื่อง 1..N
        // =====================================================================
        private void RenumberActivePallet(SqlConnection conn, SqlTransaction tran, string palletNo)
        {
            string sql = @"
                ;WITH Active AS (
                    SELECT BarcodeCarton,
                           ROW_NUMBER() OVER (ORDER BY SeqInPallet ASC) AS NewSeq
                    FROM dbo.FG_PalletDetail
                    WHERE PalletNo = @PalletNo
                      AND ReleasedDate IS NULL
                      AND CancelledDate IS NULL
                )
                UPDATE pd
                SET pd.SeqInPallet = a.NewSeq
                FROM dbo.FG_PalletDetail pd
                JOIN Active a ON pd.BarcodeCarton = a.BarcodeCarton";

            using (SqlCommand cmd = NewCommand(sql, conn, tran))
            {
                cmd.Parameters.AddWithValue("@PalletNo", palletNo);
                cmd.ExecuteNonQuery();
            }
        }

        // =====================================================================
        // Application Lock (กันกดปุ่มซ้อนกันข้ามเครื่อง/ข้าม Session)
        // =====================================================================
        private static bool TryAcquireAppLock(SqlConnection conn)
        {
            using (SqlCommand cmd = NewCommand(
                "EXEC @ret = sp_getapplock @Resource = @res, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0",
                conn, null))
            {
                var ret = cmd.Parameters.Add("@ret", SqlDbType.Int);
                ret.Direction = ParameterDirection.Output;
                cmd.Parameters.AddWithValue("@res", AppLockResource);
                cmd.ExecuteNonQuery();
                return ret.Value != DBNull.Value && Convert.ToInt32(ret.Value) >= 0;
            }
        }

        private static void ReleaseAppLock(SqlConnection conn)
        {
            try
            {
                using (SqlCommand cmd = NewCommand(
                    "EXEC sp_releaseapplock @Resource = @res, @LockOwner = 'Session'", conn, null))
                {
                    cmd.Parameters.AddWithValue("@res", AppLockResource);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Log("WARN", "Release App Lock Failed : " + ex.Message);
            }
        }

        // =====================================================================
        // Small helpers
        // =====================================================================
        private static SqlCommand NewCommand(string sql, SqlConnection conn, SqlTransaction tran)
        {
            var cmd = new SqlCommand(sql, conn, tran);
            cmd.CommandTimeout = CommandTimeoutSeconds;
            return cmd;
        }

        private static string AddInParameters(SqlCommand cmd, List<string> values)
        {
            var names = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                names[i] = "@b" + i;
                cmd.Parameters.AddWithValue(names[i], values[i]);
            }
            return "(" + string.Join(",", names) + ")";
        }

        private static IEnumerable<List<T>> Partition<T>(IList<T> source, int size)
        {
            for (int i = 0; i < source.Count; i += size)
            {
                var part = new List<T>(Math.Min(size, source.Count - i));
                for (int j = i; j < Math.Min(i + size, source.Count); j++)
                    part.Add(source[j]);
                yield return part;
            }
        }

        private static ReleaseCartonResult Fail(string barcode, string palletNo, string message)
        {
            return new ReleaseCartonResult
            {
                BarcodeCarton = barcode,
                Success = false,
                Message = message,
                PalletNo = palletNo
            };
        }

        private static List<ReleaseCartonResult> FailBatch(List<PalletReleaseGroup> batch, string message)
        {
            var list = new List<ReleaseCartonResult>();
            foreach (var g in batch)
                foreach (var barcode in g.Barcodes)
                    list.Add(Fail(barcode, g.PalletNo, message));
            return list;
        }

        private static string BuildSummary(int synced, int pendingCount, List<ReleaseCartonResult> results, string syncError)
        {
            int ok = results.Count(r => r.Success && !r.AlreadyReleased);
            int already = results.Count(r => r.Success && r.AlreadyReleased);
            int failed = results.Count(r => !r.Success);
            int closedPallets = results.Where(r => r.PalletClosed).Select(r => r.PalletNo).Distinct().Count();

            return $"Sync InvoiceNo ใหม่ {synced} Carton"
                 + (syncError != null ? " (Sync ผิดพลาด)" : "")
                 + $" | รอ Release {pendingCount} Carton -> สำเร็จ {ok}, เคย Release แล้ว {already}, ล้มเหลว {failed}"
                 + $" | Pallet ที่ปิดและคืน Area {closedPallets}";
        }

        private static void Log(string level, string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
            System.Diagnostics.Debug.WriteLine("[CartonRelease] " + line);

            try
            {
                lock (_logLock)
                {
                    string dir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AutoScan_Weight_WinUI", "Logs");
                    System.IO.Directory.CreateDirectory(dir);
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(dir, $"CartonRelease_{DateTime.Now:yyyyMMdd}.log"),
                        line + Environment.NewLine);
                }
            }
            catch
            {
                // Log ห้ามทำให้งานหลักล้ม
            }
        }

        // หมายเหตุ: ClosePalletAndReleaseArea เป็น Shared Method ที่
        // PalletProvisioning.ClosePalletAndReleaseArea (ใช้ร่วมกับ CartonMoveService)
    }


    /// <summary>
    /// ตัวอย่างการเรียก CartonReleaseService สำหรับ Program ที่ Update PlaceStatus
    ///
    /// Flow แนะนำ:
    ///   1) เมื่อ Program ตรวจพบว่า InvoiceNo ถูก ERP เขียนเข้ามาแล้ว (Trigger จาก
    ///      Event ของโปรแกรมเอง เช่น Scan ของขึ้นรถเสร็จ / Polling เช็คระยะ)
    ///   2) ดึงรายการ BarcodeCarton ทั้งหมดที่อยู่ใน Invoice นั้น (GetBarcodesByInvoiceNo)
    ///   3) เรียก ReleaseCartonsFromPallet() ครั้งเดียวทั้ง Batch
    ///   4) วนดูผลลัพธ์ทีละ Carton เพื่อ Log/แจ้งเตือนกรณีมีปัญหา
    /// </summary>
    public class ReleaseCartonExample
    {
        /// <summary>
        /// เรียกใช้งานหลัก - ส่ง InvoiceNo เข้ามา ระบบจะหา Carton ทั้งหมดที่เกี่ยวข้องเอง
        /// แล้ว Release ให้ครบทุกใบใน Transaction เดียว
        /// </summary>
        public void ClearCartonByInvoice(string invoiceNo)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo))
                return;

            // 1) หา Barcode ทั้งหมดที่อยู่ใน Invoice นี้ และยังไม่เคย Release
            List<string> barcodes = GetBarcodesByInvoiceNo(invoiceNo);

            if (barcodes.Count == 0)
            {
                // ไม่มี Carton ที่รอ Release สำหรับ Invoice นี้ (อาจ Release ไปหมดแล้ว
                // หรือยังไม่เคยผ่าน PalletOptimize เลยก็ได้ - ไม่ถือว่าผิดปกติ)
                return;
            }

            // 2) เรียก Service เพื่อ Release ทั้ง Batch
            var releaseService = new CartonReleaseService();
            List<ReleaseCartonResult> results = releaseService.ReleaseCartonsFromPallet(barcodes);

            // 3) วนดูผลลัพธ์ทีละ Carton - Log เก็บไว้ตรวจสอบย้อนหลัง หรือแจ้งเตือนถ้าพบปัญหา
            foreach (var r in results)
            {
                if (r.Success)
                {
                    if (r.AlreadyReleased)
                    {
                        // เคย Release ไปแล้ว (เช่นเรียกซ้ำ) - ไม่ต้องทำอะไรเพิ่ม
                        // Log.Info($"{r.BarcodeCarton} ถูก Release ไปแล้วก่อนหน้า");
                    }
                    else if (r.PalletClosed)
                    {
                        // Release สำเร็จ และทำให้ Pallet ว่างสนิท -> Area ถูกคืนเป็น Ready แล้ว
                        // Log.Info($"{r.BarcodeCarton} Release สำเร็จ, Pallet {r.PalletNo} ปิดและคืน Area แล้ว");
                    }
                    else
                    {
                        // Release สำเร็จปกติ
                        // Log.Info($"{r.BarcodeCarton} Release สำเร็จ จาก Pallet {r.PalletNo}");
                    }
                }
                else
                {
                    // ล้มเหลว - ควร Log ไว้ให้ทีมตรวจสอบ (เช่น Carton ไม่มีในระบบ Pallet เลย)
                    // Log.Warning($"{r.BarcodeCarton} Release ไม่สำเร็จ: {r.Message}");
                }
            }
        }

        /// <summary>
        /// หา BarcodeCarton ทั้งหมดที่อยู่ภายใต้ InvoiceNo ที่ระบุ และยังไม่เคย Release
        /// (ReleasedDate IS NULL) - ใช้เป็น Input ให้ ReleaseCartonsFromPallet
        /// </summary>
        public List<string> GetBarcodesByInvoiceNo(string invoiceNo)
        {
            var result = new List<string>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    string sql = @"
                        SELECT BarcodeCarton
                        FROM dbo.FG_PalletDetail
                        WHERE InvoiceNo = @InvoiceNo
                          AND ReleasedDate IS NULL
                          AND CancelledDate IS NULL";

                    SqlCommand cmd = new SqlCommand(sql, connectSQL.conn);
                    cmd.Parameters.AddWithValue("@InvoiceNo", invoiceNo);

                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        result.Add(reader["BarcodeCarton"] as string);
                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                App.DialogService.ShowMessageAsync("Failed", "Query Failed : " + ex.Message);
                //MessageBox.Show("Query Failed : " + ex.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return result;
        }
    }

    /// <summary>
    /// "เบิกงานชั่วคราว" หลายใบพร้อมกัน - เฉพาะ Carton ที่ PLACED (อยู่ในโกดังจริง)
    /// เปลี่ยน PlaceStatus -> RESERVED เท่านั้น ไม่แตะ Pallet/Area/CartonCount/SeqInPallet
    /// (Barcode เดิม ของจะกลับมาที่ Pallet เดิม; ตอนกลับเข้าให้ระบบเดิม Set PLACED เอง)
    ///
    /// ใบที่ไม่ผ่านเงื่อนไข (ไม่พบ / Release แล้ว / Cancel แล้ว / ไม่ใช่ PLACED) ข้ามพร้อมเหตุผล
    /// ใบที่ผ่านทำใน Transaction เดียว เก็บ Log ที่ FG_PalletWithdrawLog
    /// </summary>
    public class CartonWithdrawService
    {
        private const int MaxInClauseSize = 500;

        public List<WithdrawCartonResult> WithdrawCartons(List<string> barcodeCartons, string withdrawBy = null)
        {
            var results = new List<WithdrawCartonResult>();

            if (barcodeCartons == null)
                return results;

            var barcodes = barcodeCartons
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Select(b => b.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(b => b, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (barcodes.Count == 0)
                return results;

            ConnectSQL connectSQL = new ConnectSQL();
            SqlTransaction tran = null;

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    SqlConnection conn = connectSQL.conn;
                    tran = conn.BeginTransaction(IsolationLevel.Serializable);

                    // 1) Lock + อ่านสถานะ
                    var rows = new Dictionary<string, (string PalletNo, string PlaceStatus, bool Released, bool Cancelled)>(
                        StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < barcodes.Count; i += MaxInClauseSize)
                    {
                        var part = barcodes.Skip(i).Take(MaxInClauseSize).ToList();
                        SqlCommand cmd = new SqlCommand(null, conn, tran);

                        var names = new string[part.Count];
                        for (int k = 0; k < part.Count; k++)
                        {
                            names[k] = "@b" + k;
                            cmd.Parameters.AddWithValue(names[k], part[k]);
                        }

                        cmd.CommandText = @"
                            SELECT BarcodeCarton, PalletNo, PlaceStatus, ReleasedDate, CancelledDate
                            FROM dbo.FG_PalletDetail WITH (UPDLOCK, ROWLOCK)
                            WHERE BarcodeCarton IN (" + string.Join(",", names) + ")";

                        SqlDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string bc = reader["BarcodeCarton"] as string;
                            if (bc == null) continue;
                            rows[bc] = (
                                reader["PalletNo"] as string,
                                reader["PlaceStatus"] as string,
                                reader["ReleasedDate"] != DBNull.Value,
                                reader["CancelledDate"] != DBNull.Value);
                        }
                        reader.Close();
                    }

                    // 2) Validate + เบิกทีละใบ
                    foreach (var bc in barcodes)
                    {
                        if (!rows.TryGetValue(bc, out var row))
                        {
                            results.Add(Fail(bc, null, $"ไม่พบ Carton '{bc}' ในระบบ"));
                            continue;
                        }
                        if (row.Released)
                        {
                            results.Add(Fail(bc, row.PalletNo, "Carton นี้ถูก Release (ส่งออก) ไปแล้ว ไม่สามารถเบิกได้"));
                            continue;
                        }
                        if (row.Cancelled)
                        {
                            results.Add(Fail(bc, row.PalletNo, "Carton นี้ถูกลบ/ยกเลิกไปแล้ว ไม่สามารถเบิกได้"));
                            continue;
                        }
                        if (!string.Equals(row.PlaceStatus, "PLACED", StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(Fail(bc, row.PalletNo,
                                $"Carton นี้มีสถานะ '{row.PlaceStatus}' ไม่ใช่ PLACED ไม่สามารถเบิกได้ (ต้องวางจริงในโกดังแล้วเท่านั้น)"));
                            continue;
                        }

                        SqlCommand updateCmd = new SqlCommand(@"
                            UPDATE dbo.FG_PalletDetail
                            SET PlaceStatus = 'RESERVED'
                            WHERE BarcodeCarton = @BarcodeCarton", conn, tran);
                        updateCmd.Parameters.AddWithValue("@BarcodeCarton", bc);
                        updateCmd.ExecuteNonQuery();

                        SqlCommand logCmd = new SqlCommand(@"
                            INSERT INTO dbo.FG_PalletWithdrawLog (BarcodeCarton, PalletNo, WithdrawDate, WithdrawBy)
                            VALUES (@BarcodeCarton, @PalletNo, GETDATE(), @WithdrawBy)", conn, tran);
                        logCmd.Parameters.AddWithValue("@BarcodeCarton", bc);
                        logCmd.Parameters.AddWithValue("@PalletNo", row.PalletNo ?? string.Empty);
                        logCmd.Parameters.AddWithValue("@WithdrawBy", (object)withdrawBy ?? DBNull.Value);
                        logCmd.ExecuteNonQuery();

                        results.Add(new WithdrawCartonResult
                        {
                            BarcodeCarton = bc,
                            Success = true,
                            Message = $"เบิก Carton '{bc}' จาก Pallet {row.PalletNo} สำเร็จ",
                            PalletNo = row.PalletNo
                        });
                    }

                    tran.Commit();
                }
                else
                {
                    App.DialogService.ShowMessageAsync("Failed", "ไม่สามารถเชื่อมต่อฐานข้อมูล YPT_Innovation ได้");
                }
            }
            catch (Exception ex)
            {
                try { tran?.Rollback(); } catch { }
                App.DialogService.ShowMessageAsync("Failed", "Withdraw Cartons Failed : " + ex.Message);
                results = barcodes.Select(b => Fail(b, null, "Withdraw Cartons Failed : " + ex.Message)).ToList();
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return results;
        }

        /// <summary>Wrapper เดิม - เบิกใบเดียว</summary>
        public WithdrawCartonResult WithdrawCarton(string barcodeCarton, string withdrawBy = null)
        {
            if (string.IsNullOrWhiteSpace(barcodeCarton))
                return Fail(barcodeCarton, null, "กรุณาระบุ Barcode Carton");

            return WithdrawCartons(new List<string> { barcodeCarton }, withdrawBy).FirstOrDefault()
                   ?? Fail(barcodeCarton, null, "ไม่มีผลลัพธ์");
        }

        private static WithdrawCartonResult Fail(string barcode, string palletNo, string message)
        {
            return new WithdrawCartonResult
            {
                BarcodeCarton = barcode,
                Success = false,
                Message = message,
                PalletNo = palletNo
            };
        }
    }

    /// <summary>
    /// ลบ Carton ออกจาก Pallet (Soft Delete ผ่าน CancelledDate) หลายใบพร้อมกัน
    /// ใช้กับ:
    ///   - ดึงของออกจาก Master (Pull from Master)
    ///   - Short ship -> ทำ DocNo ใหม่ (เลือกกล่องที่จะย้ายออก)
    ///   - Revise DocNo (CartonCancelService.CancelDocNo เรียก Method นี้ต่อ)
    ///
    /// กติกา:
    ///   - รับ Carton ที่ RESERVED หรือ PLACED ที่ยังไม่ Release และยังไม่ Cancel
    ///   - ใบที่ไม่ผ่านเงื่อนไข ข้ามพร้อมเหตุผลรายใบ ไม่ทำให้ใบอื่นล้ม
    ///   - ใบที่ผ่าน ทำใน Transaction เดียว (ล้มแล้ว Rollback ทั้งหมด)
    ///   - Lock Pallet ตามลำดับ PalletNo น้อย -> มาก กัน Deadlock
    ///   - ลด CartonCount, Renumber SeqInPallet, FULL -> OPEN (ผ่าน UpdatePalletCount),
    ///     ถ้า Pallet ว่างสนิท -> Close + คืน Area เป็น Ready
    ///   - PLACED จะมี RequiresPhysicalPickup = true (แค่ Flag ไม่มีขั้นตอนยืนยันใน Service)
    /// </summary>
    public class CartonRemoveService
    {
        private const int MaxInClauseSize = 500;

        // =====================================================================
        // รายการ Carton ที่ยัง Active ใน Warehouse (ให้ UI เลือกหลายใบ) - กรอง DocNo ได้
        // =====================================================================
        public List<ActiveCartonItem> GetActiveCartons(string docNo = null)
        {
            var result = new List<ActiveCartonItem>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    string sql = @"
                        SELECT BarcodeCarton, DocNo, PalletNo, PlaceStatus
                        FROM dbo.FG_PalletDetail
                        WHERE ReleasedDate IS NULL
                          AND CancelledDate IS NULL";

                    if (!string.IsNullOrWhiteSpace(docNo))
                        sql += " AND DocNo = @DocNo";

                    sql += " ORDER BY PalletNo ASC, SeqInPallet ASC";

                    SqlCommand cmd = new SqlCommand(sql, connectSQL.conn);
                    if (!string.IsNullOrWhiteSpace(docNo))
                        cmd.Parameters.AddWithValue("@DocNo", docNo);

                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        result.Add(new ActiveCartonItem
                        {
                            BarcodeCarton = reader["BarcodeCarton"] as string,
                            DocNo = reader["DocNo"] as string,
                            PalletNo = reader["PalletNo"] as string,
                            PlaceStatus = reader["PlaceStatus"] as string
                        });
                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                App.DialogService.ShowMessageAsync("Failed", "Query Failed : " + ex.Message);
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return result;
        }

        // =====================================================================
        // ปุ่มยืนยันลบ Carton (หลายใบ)
        // =====================================================================
        public List<CancelCartonResult> RemoveCartons(List<string> barcodeCartons)
        {
            var results = new List<CancelCartonResult>();

            if (barcodeCartons == null)
                return results;

            var barcodes = barcodeCartons
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Select(b => b.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(b => b, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (barcodes.Count == 0)
                return results;

            ConnectSQL connectSQL = new ConnectSQL();
            SqlTransaction tran = null;

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    SqlConnection conn = connectSQL.conn;
                    tran = conn.BeginTransaction(IsolationLevel.Serializable);

                    // 1) Lock + อ่านสถานะ Carton ทั้งหมด (เรียง Barcode กัน Deadlock)
                    var rows = new Dictionary<string, (string PalletNo, string PlaceStatus, bool Released, bool Cancelled)>(
                        StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < barcodes.Count; i += MaxInClauseSize)
                    {
                        var part = barcodes.Skip(i).Take(MaxInClauseSize).ToList();
                        SqlCommand cmd = new SqlCommand(null, conn, tran);

                        var names = new string[part.Count];
                        for (int k = 0; k < part.Count; k++)
                        {
                            names[k] = "@b" + k;
                            cmd.Parameters.AddWithValue(names[k], part[k]);
                        }

                        cmd.CommandText = @"
                            SELECT BarcodeCarton, PalletNo, PlaceStatus, ReleasedDate, CancelledDate
                            FROM dbo.FG_PalletDetail WITH (UPDLOCK, ROWLOCK)
                            WHERE BarcodeCarton IN (" + string.Join(",", names) + ")";

                        SqlDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string bc = reader["BarcodeCarton"] as string;
                            if (bc == null) continue;
                            rows[bc] = (
                                reader["PalletNo"] as string,
                                reader["PlaceStatus"] as string,
                                reader["ReleasedDate"] != DBNull.Value,
                                reader["CancelledDate"] != DBNull.Value);
                        }
                        reader.Close();
                    }

                    // 2) แยกใบที่ลบได้ / ลบไม่ได้
                    var toRemove = new List<(string Barcode, string PalletNo, bool WasPlaced)>();

                    foreach (var bc in barcodes)
                    {
                        if (!rows.TryGetValue(bc, out var row))
                        {
                            results.Add(Fail(bc, null, "ไม่พบ Carton นี้ในระบบ"));
                            continue;
                        }
                        if (row.Released)
                        {
                            results.Add(Fail(bc, row.PalletNo, "Carton นี้ถูก Release (ส่งออก) ไปแล้ว ไม่สามารถลบได้"));
                            continue;
                        }
                        if (row.Cancelled)
                        {
                            results.Add(Fail(bc, row.PalletNo, "Carton นี้ถูกลบ/ยกเลิกไปแล้ว"));
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(row.PalletNo))
                        {
                            results.Add(Fail(bc, null, "Carton นี้ไม่มี Pallet สังกัด"));
                            continue;
                        }

                        bool wasPlaced = string.Equals(row.PlaceStatus, "PLACED", StringComparison.OrdinalIgnoreCase);
                        toRemove.Add((bc, row.PalletNo, wasPlaced));
                    }

                    // 3) Group ตาม Pallet เรียงน้อย -> มาก
                    var palletGroups = toRemove
                        .GroupBy(x => x.PalletNo, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

                    foreach (var group in palletGroups)
                    {
                        string palletNo = group.Key;
                        var pallet = PalletProvisioning.LockPallet(conn, tran, palletNo);

                        if (pallet == null)
                        {
                            // ข้อมูลไม่สอดคล้อง - ไม่แตะ Carton กลุ่มนี้เลย
                            foreach (var item in group)
                                results.Add(Fail(item.Barcode, palletNo, $"ไม่พบ Pallet '{palletNo}' ที่ Carton นี้สังกัดอยู่"));
                            continue;
                        }

                        var groupResults = new List<CancelCartonResult>();
                        foreach (var item in group)
                        {
                            SetCancelledDate(conn, tran, item.Barcode);

                            groupResults.Add(new CancelCartonResult
                            {
                                BarcodeCarton = item.Barcode,
                                Success = true,
                                Message = item.WasPlaced
                                    ? "ลบสำเร็จ - ของยังวางอยู่บน Pallet จริง ต้องไปเก็บออก"
                                    : "ลบสำเร็จ",
                                PalletNo = palletNo,
                                RequiresPhysicalPickup = item.WasPlaced
                            });
                        }

                        RenumberActivePallet(conn, tran, palletNo);

                        int newCount = pallet.CartonCount - group.Count();
                        if (newCount < 0) newCount = 0; // กันข้อมูลเพี้ยน

                        if (newCount == 0)
                        {
                            PalletProvisioning.ClosePalletAndReleaseArea(conn, tran, palletNo, pallet.CurrentAreaNo);
                            foreach (var r in groupResults)
                                r.PalletClosed = true;
                        }
                        else
                        {
                            PalletProvisioning.UpdatePalletCount(conn, tran, palletNo, newCount, pallet.MaxCapacity);
                        }

                        results.AddRange(groupResults);
                    }

                    tran.Commit();
                }
                else
                {
                    App.DialogService.ShowMessageAsync("Failed", "ไม่สามารถเชื่อมต่อฐานข้อมูล YPT_Innovation ได้");
                }
            }
            catch (Exception ex)
            {
                try { tran?.Rollback(); } catch { }
                App.DialogService.ShowMessageAsync("Failed", "Remove Cartons Failed : " + ex.Message);

                // Rollback ทั้งหมด -> ทุกใบถือว่าไม่สำเร็จ
                results = barcodes.Select(b => Fail(b, null, "Remove Cartons Failed : " + ex.Message)).ToList();
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return results;
        }

        private static CancelCartonResult Fail(string barcode, string palletNo, string message)
        {
            return new CancelCartonResult
            {
                BarcodeCarton = barcode,
                Success = false,
                Message = message,
                PalletNo = palletNo
            };
        }

        private void SetCancelledDate(SqlConnection conn, SqlTransaction tran, string barcodeCarton)
        {
            string sql = @"
                UPDATE dbo.FG_PalletDetail
                SET CancelledDate = GETDATE()
                WHERE BarcodeCarton = @BarcodeCarton
                  AND CancelledDate IS NULL
                  AND ReleasedDate IS NULL";

            SqlCommand cmd = new SqlCommand(sql, conn, tran);
            cmd.Parameters.AddWithValue("@BarcodeCarton", barcodeCarton);
            cmd.ExecuteNonQuery();
        }

        private void RenumberActivePallet(SqlConnection conn, SqlTransaction tran, string palletNo)
        {
            string sql = @"
                ;WITH Active AS (
                    SELECT BarcodeCarton,
                           ROW_NUMBER() OVER (ORDER BY SeqInPallet ASC) AS NewSeq
                    FROM dbo.FG_PalletDetail
                    WHERE PalletNo = @PalletNo
                      AND ReleasedDate IS NULL
                      AND CancelledDate IS NULL
                )
                UPDATE pd
                SET pd.SeqInPallet = a.NewSeq
                FROM dbo.FG_PalletDetail pd
                JOIN Active a ON pd.BarcodeCarton = a.BarcodeCarton";

            SqlCommand cmd = new SqlCommand(sql, conn, tran);
            cmd.Parameters.AddWithValue("@PalletNo", palletNo);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Service สำหรับ "Revise DocNo" - เมื่อ ERP Revise DocNo เอกสารเดิมถูกยกเลิก
    /// ใช้ทันที ต้องลบ DocNo นั้นออกจาก Warehouse (BarcodeCarton ไม่มีทางซ้ำข้าม
    /// DocNo เพราะ ERP Generate ใหม่เสมอเมื่อทำ DocNo ใหม่)
    ///
    /// Soft Delete ผ่าน FG_PalletDetail.CancelledDate (เก็บ History ไว้ ไม่ Delete
    /// แถวทิ้งจริง)
    ///
    /// เงื่อนไข:
    ///   - Carton ที่ Release ไปแล้ว (ReleasedDate NOT NULL) ของออกจริงแล้ว
    ///     ยกเลิกไม่ได้ - ข้ามไปรายงานแยก ไม่กระทบ Carton อื่นใน DocNo เดียวกัน
    ///   - Carton ที่ PlaceStatus = PLACED ยกเลิกได้ปกติ แต่ Flag
    ///     RequiresPhysicalPickup = true เพื่อแจ้งหน้างานไปเก็บของออกจาก Pallet จริง
    ///   - Cascade เหมือน CartonReleaseService ทุกประการ (ลด CartonCount, Renumber,
    ///     Close Pallet + คืน Area ถ้าว่างสนิท) เพราะถือว่า "ของออกจากระบบ Pallet
    ///     แล้ว" เหมือนกัน (ต่างแค่เพราะถูกยกเลิก ไม่ใช่เพราะส่งออกจริง)
    /// </summary>
    public class CartonCancelService
    {
        // =====================================================================
        // ComboBox: DocNo ทั้งหมดที่ยังมี Carton ค้างอยู่ใน Warehouse และยัง Active
        // จริง (ยังไม่เคย Release หรือ Cancel มาก่อน)
        // =====================================================================
        public List<CancellableDocNoItem> GetCancellableDocNos()
        {
            var result = new List<CancellableDocNoItem>();
            ConnectSQL connectSQL = new ConnectSQL();

            try
            {
                if (connectSQL.YPTInnovation())
                {
                    string sql = @"
                        SELECT DocNo, COUNT(*) AS CartonCount
                        FROM dbo.FG_PalletDetail
                        WHERE ReleasedDate IS NULL
                          AND CancelledDate IS NULL
                        GROUP BY DocNo
                        ORDER BY DocNo ASC";

                    SqlCommand cmd = new SqlCommand(sql, connectSQL.conn);
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        result.Add(new CancellableDocNoItem
                        {
                            DocNo = reader["DocNo"] as string,
                            CartonCount = Convert.ToInt32(reader["CartonCount"])
                        });
                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                App.DialogService.ShowMessageAsync("Failed", "Query Failed : " + ex.Message);
            }
            finally
            {
                connectSQL.Close_Sql();
            }

            return result;
        }

        // =====================================================================
        // ปุ่มยืนยันยกเลิก DocNo - Cancel ทุก Carton ที่ยัง Active ของ DocNo นี้
        // ใช้ Logic เดียวกับ CartonRemoveService.RemoveCartons ทุกประการ
        // (Carton ที่ Release ไปแล้วจะถูกข้ามและรายงานแยก)
        // =====================================================================
        public List<CancelCartonResult> CancelDocNo(string docNo)
        {
            if (string.IsNullOrWhiteSpace(docNo))
                return new List<CancelCartonResult>();

            var remove = new CartonRemoveService();
            var barcodes = remove.GetActiveCartons(docNo).Select(c => c.BarcodeCarton).ToList();

            return remove.RemoveCartons(barcodes);
        }
    }
}
