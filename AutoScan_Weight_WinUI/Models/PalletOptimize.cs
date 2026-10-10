using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Models;

public class PalletOptimize
{
}

/// <summary>
/// Mapping 1:1 กับ Column ของ Query ต้นทาง (cte_Master) - 1 แถว = 1 Order-line
/// ภายในกล่อง (1 BarcodeCarton อาจมีได้หลายแถว/หลาย Order-line)
/// </summary>
public class CartonInput
{
    public string DocNo { get; set; }
    public string StyleID { get; set; }
    public string OrderNo { get; set; }
    public string Style { get; set; }
    public string CustPORef { get; set; }
    public string LotRef { get; set; }
    public string FinalDest { get; set; }
    public string Warehouse { get; set; }
    public string BarcodeCarton { get; set; }
    public string ScanCarton { get; set; }
    public string InvoiceNo { get; set; }
    public string Customer { get; set; }
}

/// <summary>
/// รายการ Order หนึ่งรายการที่อยู่ภายในกล่อง (Order-line) - StyleID/Style/
/// CustPORef/LotRef/OrderNo/FinalDest เป็นข้อมูลระดับนี้ เพราะ 1 กล่องมีของ
/// จากมากกว่า 1 Order ได้จริง
/// </summary>
public class OrderLine
{
    public string OrderNo { get; set; }
    public string FinalDest { get; set; }
    public string StyleID { get; set; }
    public string Style { get; set; }
    public string CustPORef { get; set; }
    public string LotRef { get; set; }
}

/// <summary>
/// ตัวแทน "กล่องจริง 1 ใบ" หลัง Group จาก CartonInput ตาม BarcodeCarton แล้ว -
/// เป็นหน่วยที่ใช้จัดลง Pallet จริง (1 กล่อง = 1 ตำแหน่งใน Pallet เสมอ ไม่ว่า
/// ข้างในจะมีกี่ Order-line ก็ตาม) DocNo/Warehouse/BarcodeCarton เป็นค่าระดับ
/// กล่อง คงที่เสมอ ส่วน OrderLines คือรายการ Order ที่อยู่ข้างในกล่องนั้น
/// </summary>
public class CartonGroup
{
    public string DocNo { get; set; }
    public string BarcodeCarton { get; set; }
    public string ScanCarton { get; set; }
    public string Warehouse { get; set; }
    public string Customer { get; set; }
    public string InvoiceNo { get; set; }

    /// <summary>
    /// GroupKey ที่คำนวณแล้วผ่าน Validation ว่าค่าเดียวจริงทั้งกล่อง
    /// (Warehouse ถ้ามี ไม่งั้น fallback เป็น FinalDest)
    /// </summary>
    public string GroupKey { get; set; }
    public string GroupType { get; set; }

    public System.Collections.Generic.List<OrderLine> OrderLines { get; set; }
        = new System.Collections.Generic.List<OrderLine>();
}

/// <summary>
/// กล่องที่ถูกกันออกจากการ Optimize อัตโนมัติ เพราะข้อมูลขัดกับกฎธุรกิจ
/// (เช่น มี Warehouse มากกว่า 1 ค่าในกล่องเดียวกัน หรือไม่มี Warehouse แล้ว
/// FinalDest ไม่เท่ากันในกล่องเดียวกัน) ต้องให้หน้างานมาตรวจสอบเอง
/// </summary>
public class ConflictingCarton
{
    public string BarcodeCarton { get; set; }
    public string Reason { get; set; }
}

/// <summary>
/// ข้อมูลสรุปของ Pallet ที่ใช้ระหว่างการคำนวณ (in-memory state)
/// </summary>
internal class PalletInfo
{
    public string PalletNo { get; set; }
    public int CartonCount { get; set; }
    public int MaxCapacity { get; set; }

    /// <summary>
    /// จะมีค่าเฉพาะตอนที่ Pallet นี้ "เพิ่งถูกสร้างใหม่" ในรอบทำงานนี้เท่านั้น
    /// (ใช้เป็น Flag บอกว่าต้องรายงาน Area Reservation หรือไม่ -
    /// ถ้าเป็น Pallet เก่าที่ถูกดึงมาใช้ต่อผ่าน LockPallet ค่านี้จะเป็น null เสมอ)
    /// </summary>
    public string AreaNo { get; set; }

    /// <summary>
    /// AreaNo จริงปัจจุบันของ Pallet นี้ (Set ทุกครั้งที่ดึงผ่าน LockPallet)
    /// ใช้ตอนต้องรู้ว่า Pallet ที่มีอยู่แล้วอยู่ Area ไหน เช่น ตอน Close Pallet
    /// แล้วต้องคืน Area กลับเป็น Ready (ดู CartonReleaseService)
    /// </summary>
    public string CurrentAreaNo { get; set; }

    /// <summary>
    /// ใช้ตอน Lock Pallet สำหรับ Move Carton เพื่อเทียบ GroupKey กับ Carton ที่จะย้าย
    /// (ไม่ได้ใช้ตอน Optimize เพราะ GroupKey รู้อยู่แล้วจากภายนอก)
    /// </summary>
    public string GroupKey { get; set; }

    public int RemainingSpace => MaxCapacity - CartonCount;
}

/// <summary>
/// ผลลัพธ์สรุปหลังจาก Optimize เสร็จ ใช้ Return กลับให้ผู้เรียกดูผลได้
/// </summary>
public class OptimizeResult
{
    public string DocNo { get; set; }
    public int TotalCartonProcessed { get; set; }
    public int TotalCartonSkipped { get; set; } // ที่ถูก Optimize ไปแล้วก่อนหน้า (idempotent)

    /// <summary>
    /// จำนวนกล่อง (BarcodeCarton) ที่มี Order-line มากกว่า 1 รายการข้างใน
    /// (เช่น 1 กล่องมี 2 OrderNo ปลายทางเดียวกัน) - ข้อมูลเชิงสถิติเฉยๆ
    /// ไม่กระทบการ Optimize เพราะทุก Order-line ถูกเก็บครบใน
    /// FG_PalletDetail_Order อยู่แล้ว
    /// </summary>
    public int MultiOrderCartonCount { get; set; }

    /// <summary>
    /// กล่องที่ถูกกันออกจากการ Optimize อัตโนมัติเพราะข้อมูลขัดกับกฎธุรกิจ
    /// (ดู ConflictingCarton) - ต้องให้หน้างานตรวจสอบเอง ไม่ถูก Insert เข้าระบบ
    /// </summary>
    public System.Collections.Generic.List<ConflictingCarton> ConflictingCartons { get; set; }
        = new System.Collections.Generic.List<ConflictingCarton>();

    public System.Collections.Generic.List<PalletAssignment> Assignments { get; set; }
        = new System.Collections.Generic.List<PalletAssignment>();

    /// <summary>
    /// รายการ Pallet ใหม่ที่ถูกสร้างและจอง Warehouse Area ในรอบ Optimize นี้
    /// (1 รายการ ต่อ 1 Pallet ใหม่ที่เกิดขึ้น)
    /// </summary>
    public System.Collections.Generic.List<AreaReservation> AreaReservations { get; set; }
        = new System.Collections.Generic.List<AreaReservation>();
}

public class AreaReservation
{
    public string PalletNo { get; set; }
    public string AreaNo { get; set; }
    public string WarehouseName { get; set; } // ค่าที่ใช้จอง (Warehouse หรือ FinalDest แล้วแต่กรณี)
}

// =========================================================================
// MOVE CARTON - Models
// =========================================================================

/// <summary>
/// รายการสำหรับ ComboBox 1 - เลือก Carton ที่ต้องการย้าย
/// (กรองเฉพาะ Carton ที่ PlaceStatus = RESERVED เท่านั้น)
/// </summary>
public class MovableCartonItem
{
    public string BarcodeCarton { get; set; }
    public string DocNo { get; set; }
    public string PalletNo { get; set; }
}

/// <summary>
/// ข้อมูลตำแหน่งปัจจุบันของ Carton ที่เลือก - แสดงใน Detail Box
/// FinalDest ไม่ใช่ Field เดี่ยวอีกต่อไป เพราะ 1 กล่องมีได้หลาย Order-line/
/// FinalDest ข้างใน - ดูที่ OrderLines แทน
/// </summary>
public class CartonLocationInfo
{
    public string BarcodeCarton { get; set; }
    public string DocNo { get; set; }
    public string Warehouse { get; set; }
    public string Customer { get; set; }
    public string PlaceStatus { get; set; }

    public string PalletNo { get; set; }
    public string GroupKey { get; set; }
    public int CartonCount { get; set; }
    public int MaxCapacity { get; set; }

    public string AreaNo { get; set; }
    public string AreaName { get; set; }
    public string WarehouseAreaName { get; set; } // FG_Warehouse_Area.WarehouseName (ชื่อภายในโรงงาน)

    /// <summary>
    /// รายการ Order ทั้งหมดที่อยู่ในกล่องนี้ (จาก FG_PalletDetail_Order)
    /// </summary>
    public System.Collections.Generic.List<OrderLine> OrderLines { get; set; }
        = new System.Collections.Generic.List<OrderLine>();
}

/// <summary>
/// รายการสำหรับ ComboBox 2 - เลือก Pallet ปลายทาง
/// (กรองเฉพาะ Pallet ที่ GroupKey ตรงกับ Carton และยังไม่เต็ม
/// + เพิ่มตัวเลือก "สร้าง Pallet ใหม่" ต่อท้ายเสมอถ้ายังมี Warehouse Area ว่าง)
/// </summary>
public class DestinationPalletOption
{
    /// <summary>
    /// Sentinel Value ใช้แทน "สร้าง Pallet ใหม่" ใน PalletNo - ตรวจสอบด้วย
    /// IsNewPalletOption แทนการเทียบ String ตรงๆ ในโค้ดฝั่งเรียกใช้
    /// </summary>
    public const string NEW_PALLET_SENTINEL = "<NEW>";

    public string PalletNo { get; set; }
    public int CartonCount { get; set; }
    public int MaxCapacity { get; set; }
    public int RemainingSpace { get; set; }
    public string AreaNo { get; set; }
    public string AreaName { get; set; }

    /// <summary>
    /// true ถ้าตัวเลือกนี้คือ "สร้าง Pallet ใหม่" (ไม่ใช่ Pallet ที่มีอยู่แล้วจริง)
    /// Area จะถูกจองอัตโนมัติตอนกด Save ไม่ให้ User เลือกเอง
    /// </summary>
    public bool IsNewPalletOption { get; set; }
}

/// <summary>
/// ผลลัพธ์การกด Save ย้าย Carton
/// </summary>
public class MoveCartonResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string BarcodeCarton { get; set; }
    public string FromPalletNo { get; set; }
    public string ToPalletNo { get; set; }

    /// <summary>
    /// true ถ้าการย้ายครั้งนี้ทำให้ Pallet ต้นทางว่างสนิท และถูก Close + คืน
    /// Warehouse Area กลับเป็น Ready ไปด้วย (พร้อมให้ Optimize รอบถัดไป Recycle ใช้)
    /// </summary>
    public bool SourcePalletClosed { get; set; }

    /// <summary>
    /// มีค่าเฉพาะกรณีเลือก "สร้าง Pallet ใหม่" แล้วระบบสร้างสำเร็จ
    /// </summary>
    public string NewPalletNo { get; set; }
    public string NewAreaNo { get; set; }
}

// =========================================================================
// CARTON RELEASE (เมื่อ ERP ตัด Stock / มี InvoiceNo แล้ว) - Models
// =========================================================================

public class ReleaseCartonResult
{
    public string BarcodeCarton { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }

    /// <summary>
    /// true ถ้า Carton นี้เคยถูก Release ไปแล้วก่อนหน้า (Idempotent - ไม่ทำซ้ำ)
    /// </summary>
    public bool AlreadyReleased { get; set; }

    /// <summary>
    /// true ถ้าการ Release ครั้งนี้ทำให้ Pallet ที่ Carton สังกัดอยู่ว่างสนิท
    /// และถูก Close + คืน Warehouse Area กลับเป็น Ready ไปด้วย
    /// </summary>
    public bool PalletClosed { get; set; }
    public string PalletNo { get; set; }
}

// =========================================================================
// CONFIRM PLACEMENT (โปรแกรม Track หน้างาน สแกนยืนยันว่าวางของจริงแล้ว) - Models
// =========================================================================

public class ConfirmPlacementResult
{
    public string BarcodeCarton { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }

    /// <summary>
    /// PalletNo จริงที่ Carton นี้สังกัดอยู่ในระบบ (ใช้เทียบกับที่สแกนมา)
    /// </summary>
    public string PalletNo { get; set; }

    /// <summary>
    /// true ถ้า Carton นี้เคยถูก Confirm ไปแล้วก่อนหน้า (Idempotent - ไม่ทำซ้ำ)
    /// </summary>
    public bool AlreadyPlaced { get; set; }
}

public class PalletAssignment
{
    public string PalletNo { get; set; }
    public string BarcodeCarton { get; set; }
    public string GroupKey { get; set; }
    public int SeqInPallet { get; set; }
}


// =========================================================================
// WITHDRAW CARTON (เบิกงานชั่วคราว) - Models
// =========================================================================

public class WithdrawCartonResult
{
    public string BarcodeCarton { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
    public string PalletNo { get; set; }
}

// =========================================================================
// CANCEL DOCNO (Revise DocNo) - Models
// =========================================================================

/// <summary>
///รายการสำหรับ ComboBox - Doc ที่ยังยกเลิกได้ (ยังมี Carton อยู่ใน Warehouse ที่ยัง Active จริง)
/// </summary>
public class CancellableDocNoItem
{
    public string DocNo { get; set; }
    public int CartonCount { get; set; }
}

public class CancelCartonResult
{
    public string BarcodeCarton { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
    public string PalletNo { get; set; }

    /// <summary>
    /// true ถ้า Carton นี้มี PlacedStatus = PLACED ตอนที่ถฤูกยกเลิก - ของยังอยู่
    /// ของยังอยู่จริงบน Pallet ในโกดัง ต้องแจ้งหน้างานให้ไปเก็บออกด้วย
    /// </summary>
    public bool RequiresPhysicalPickup { get; set; }

    /// <summary>
    /// true ถ้าการยกเลิกครั้งนี้ทำให้ Pallet ที่ Carton สังกัดอยู่ว่างสนิท
    /// และถูก Close + คืน Warehouse Area กลับเป็น Ready ไปด้วย
    /// </summary>
    public bool PalletClosed { get; set; }
}