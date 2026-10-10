using AutoScan_Weight_WinUI.Models;
using AutoScan_Weight_WinUI.Repositories;
using AutoScan_Weight_WinUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using ClosedXML.Excel;
using static AutoScan_Weight_WinUI.Services.PalletOptimizerService;
//using static AutoScan_Weight_WinUI.Services.PalletOptimizerService;

namespace AutoScan_Weight_WinUI.ViewModels
{
    public partial class AreaViewModel : ObservableObject
    {
        private readonly QueryPallet queryPallet = new();
        private List<PalletDetailModel> Pallet = new();
        private readonly AreaService _service = new();
        private readonly DispatcherTimer _refreshTimer = new();
        public ObservableCollection<AreaModel> Areas { get; } = new();

        public ObservableCollection<PalletDetailModel> _Pallet { get; } = new();
        public ObservableCollection<PalletDetailModel> PalletArear { get; } = new();
        private List<PalletDetailModel> PalletAll = new();

        private readonly AreaRepository _repository = new();

        //DataGridView
        [ObservableProperty]
        private ObservableCollection<PalletDetailModel> dataPallet = new();

        [ObservableProperty]
        private ObservableCollection<string> carton = new();
        [ObservableProperty]
        private string selectedCarton = string.Empty;
        [ObservableProperty]
        private ObservableCollection<string> pallets = new();
        [ObservableProperty]
        private string selectedPallet = string.Empty;
        [ObservableProperty]
        private string textChangePallet = string.Empty;

        [ObservableProperty]
        private AreaModel? selectedArea;

        // ComBoBox
        [ObservableProperty]
        private ObservableCollection<string> searchText;
        [ObservableProperty]
        private string selectedSearchText = string.Empty;

        public ObservableCollection<string> DocNoCombo { get; } = new();
        [ObservableProperty]
        private string selectedDocNo = string.Empty;

        public ObservableCollection<string> FinalCombo { get; } = new();
        [ObservableProperty]
        private string selectedFinal = string.Empty;

        public ObservableCollection<string> CartonCombo { get; } = new();
        [ObservableProperty]
        private string selectedCartons = string.Empty;
        [ObservableProperty]
        private ObservableCollection<string> packingListCombo;
        [ObservableProperty]
        private string selectedPackingList = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> groupTypeCombo = new();
        [ObservableProperty]
        private string selectedGroupType = string.Empty;

        bool IsCheck_Combo = false;
        bool IsCheck_Combo_area = false;
        private bool _isRefreshing;
        [ObservableProperty]
        private bool isDragging;
        [ObservableProperty]
        private bool isSearchBusy;

        [ObservableProperty]
        private string customer = string.Empty;

        [ObservableProperty]
        private string already = string.Empty;
        [ObservableProperty]
        private string remaining = string.Empty;
        [ObservableProperty]
        private string cartonCount = string.Empty;
        [ObservableProperty]
        private string allQty = string.Empty;

        string Area_name = string.Empty;

        [ObservableProperty]
        private bool isAdmin;

        private bool _isInitialized;

        public AreaViewModel()
        {
            //queryPallet = _querypallet;


            //LoadArea();

            _refreshTimer = new DispatcherTimer();

            _refreshTimer.Interval = TimeSpan.FromSeconds(5);

            _refreshTimer.Tick += RefreshTimer_Tick;

            _refreshTimer.Start();

            
            // results.Count == 0 ก็ปกติ แปลว่ารอบนี้ไม่มี Invoice ใหม่เข้ามา
        }



        public void SetUserPermission(User currentUser)
        {
            IsAdmin = currentUser.UserGroup == "Innovation" || currentUser.UserGroup == "Finishgoods";

            LoadArea();
        }



        private void LoadArea()
        {

            Areas.Clear();

            var list = _repository.GetAreas();

            foreach (var item in list)
            {

                if (item.AreaName == "A09" || item.AreaName == "A10" || item.AreaName == "A11" || item.AreaName == "A12" || item.AreaName == "E01" || item.AreaName == "E02" || item.AreaName == "E03" || item.AreaName == "E04")
                    continue;

                // กำหนดสิทธิ์ก่อนเพิ่ม Area
                item.CanDrag = IsAdmin;

                Areas.Add(item);
            }

            Debug.WriteLine(
        $"Initialize: IsAdmin={IsAdmin}, Areas.Count={Areas.Count}");

            // Load Order In Combobox search
            Pallet = queryPallet.GetOrderNOAll();
            SearchText = new ObservableCollection<string>(Pallet.Select(x => x.OrderNo).Where(s => s != null).Distinct().ToList());

            // Load PackingList In Combobox serach
            PackingListCombo = new ObservableCollection<string>(Pallet.Select(x => x.DocNo).Where(s => s != null).Distinct().ToList());

        }




        public bool SwapArea(AreaModel source, AreaModel target)
        {
            //return _service.SwapArea(source, target);
            bool success = _service.SwapArea(source, target);



            RefreshArea();

            return success;
        }
        public void SelectedAreaChanged(string PalletNo)
        {
            Pallet = queryPallet.GetPalletDetails(SelectedArea.PalletsNo);

            if (Pallet == null || Pallet.Count == 0)
            {
                DataPallet.Clear();

                Already = "0";
                Remaining = "0";
                AllQty = "0";

                return;
            }
            DataPallet = new ObservableCollection<PalletDetailModel>(Pallet);

            CartonCount = Pallet.Count.ToString();
            Customer = Pallet[0].Customer.ToString();

            int count = Pallet.Where(x => x.PlaceStatus == "PLACED")
                .Select(x => x.BarcodeCarton)
                .Distinct()
                .Count();

            int countRemaining = Pallet.Where(x => x.PlaceStatus != "PLACED")
                .Select(x => x.BarcodeCarton)
                .Distinct()
                .Count();

            long All_qty = Pallet.Sum(x =>long.TryParse(x.Qty, out long qty) ? qty : 0);
            AllQty = All_qty.ToString();

            Already = count.ToString();
            Remaining = countRemaining.ToString();

            Debug.WriteLine($"Selected Area: {SelectedArea.PalletsNo}");

            // Load BarcodeCarton In Combobox change
            PalletOptimizerService.CartonMoveService cartonMoveService = new PalletOptimizerService.CartonMoveService();
            var movableCartons = cartonMoveService.GetMovableCartons();

            //string result = string.Join(", ", movableCartons.Select(x => x.BarcodeCarton));
            Carton = new ObservableCollection<string>(movableCartons
                .Where(x => x.PalletNo == PalletNo)
                .Select(x => x.BarcodeCarton)
                .ToArray().ToList());

            SelectedCarton = string.Empty;
            SelectedPallet = string.Empty;

        }

        partial void OnSelectedCartonChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            IsCheck_Combo = true;

            SelectedPallet = string.Empty;

            try
            {
                PalletOptimizerService.CartonMoveService cartonMoveService = new PalletOptimizerService.CartonMoveService();

                var cartonLocation = cartonMoveService.GetCartonLocation(value);

                string str_result = "AreaName : " + cartonLocation.AreaName + "  ,  Pallet No : " + cartonLocation.PalletNo + "  ,  GroupKey : " + cartonLocation.GroupKey.ToString() + " , Customer : " + cartonLocation.Customer;

                TextChangePallet = str_result;

                var result = cartonMoveService.GetAvailableDestinationPallets(cartonLocation.GroupKey, cartonLocation.PalletNo, cartonLocation.Customer);

                Pallets = new ObservableCollection<string>(result.Select(x => x.PalletNo).ToArray().ToList());

            }
            finally
            {
                IsCheck_Combo = false;
            }
        }

        [RelayCommand]
        public async Task Change()
        {
            if (SelectedCarton == string.Empty || SelectedPallet == string.Empty)
            {
                App.DialogService.ShowMessageAsync("❌ Fail ", "กรุณาเลือกข้อมูลให้ครบ");
                return;
            }

            PalletOptimizerService.CartonMoveService cartonMoveService = new PalletOptimizerService.CartonMoveService();

            var result = cartonMoveService.MoveCarton(SelectedCarton, SelectedPallet);

            SelectedCarton = string.Empty;
            SelectedPallet = string.Empty;

            App.DialogService.ShowMessageAsync("✅ Complete ", "ย้ายกล่องสำเร็จ " + result.NewPalletNo);

            IsCheck_Combo = false;
        }

        private async void RefreshTimer_Tick(object? sender, object e)
        {
            if (_isRefreshing || IsDragging || IsSearchBusy)
                return;

            _isRefreshing = true;

            try
            {
                RefreshArea();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh Error : {ex.Message}");
            }
            finally
            {
                _isRefreshing = false;
            }

        }

        public void RefreshArea()
        {
            var dbAreas = _service.GetAreas();

            foreach (var dbArea in dbAreas)
            {
                var uiArea = Areas.FirstOrDefault(x => x.AreaNo == dbArea.AreaNo);

                if (uiArea == null)
                    continue;

                uiArea.AreaNo = dbArea.AreaNo;
                uiArea.AreaName = dbArea.AreaName;
                uiArea.PalletsNo = dbArea.PalletsNo;
                uiArea.Customer = dbArea.Customer;
                uiArea.Status = dbArea.Status;
                uiArea.GroupKey = dbArea.GroupKey;
                uiArea.GroupType = dbArea.GroupType;
                uiArea.CartonCount = dbArea.CartonCount;
                uiArea.MaxCapital = dbArea.MaxCapital;
                uiArea.PalletStatus = dbArea.PalletStatus;
            }


        }
        partial void OnSelectedSearchTextChanged(string value)
        {
            IsCheck_Combo_area = true;
            IsSearchBusy = true;

            try
            {
                SelectedDocNo = string.Empty;
                SelectedFinal = string.Empty;
                SelectedCartons = string.Empty;

                foreach (var area in Areas)
                {
                    area.IsSelected = false;
                }

                var dbAreas = _service.GetAreaByOrder(value);

                foreach (var areaNo in dbAreas)
                {
                    var area = Areas.FirstOrDefault(x => x.AreaNo == areaNo.AreaNo);

                    if (area != null)
                        area.IsSelected = true;
                }

                var docNos = Pallet
                    .Where(x => x.OrderNo == value)
                    .Select(x => x.DocNo)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct()
                    .ToList();

                DocNoCombo.Clear();

                foreach (var docno in docNos)
                {
                    DocNoCombo.Add(docno);
                }
            }
            finally
            {
                IsCheck_Combo_area = false;
                IsSearchBusy = false;
            }


        }

        partial void OnSelectedDocNoChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
            { return; }

            if (IsCheck_Combo_area)
                return;

            IsCheck_Combo_area = true;
            IsSearchBusy = true;

            try
            {

                SelectedFinal = string.Empty;
                SelectedCartons = string.Empty;

                foreach (var area in Areas)
                {
                    area.IsSelected = false;
                }

                var dbAreas = _service.GetDocByOrder(SelectedSearchText, value);

                foreach (var docNo in dbAreas)
                {
                    var doc = Areas.FirstOrDefault(x => x.AreaNo == docNo.AreaNo);

                    if (doc != null)
                        doc.IsSelected = true;
                }

                FinalCombo.Clear();

                var finals = dbAreas
                    .Where(x => x.OrderNo == SelectedSearchText && x.DocNo == value)
                    .Select(x => x.FinalDest)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct()
                    .ToList();

                foreach (var fianl in finals)
                {
                    FinalCombo.Add(fianl);
                }

                //FinalCombo = new ObservableCollection<string>(dbAreas
                //    .Where(x => x.OrderNo == SelectedSearchText && x.DocNo == value)
                //    .Select(x => x.FinalDest)
                //    .Where(x => !string.IsNullOrEmpty(x))
                //    .Distinct()
                //    .ToList());
            }
            finally
            {
                IsCheck_Combo_area = false;
                IsSearchBusy = false;
            }



            IsCheck_Combo_area = false;
        }

        partial void OnSelectedFinalChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
            { return; }

            if (IsCheck_Combo_area)
                return;

            IsCheck_Combo_area = true;
            IsSearchBusy = true;

            try
            {
                SelectedCartons = string.Empty;

                foreach (var area in Areas)
                {
                    area.IsSelected = false;
                }

                var dbAreas = _service.GetFinalByOrder(SelectedSearchText, SelectedDocNo, value);

                foreach (var docNo in dbAreas)
                {
                    var doc = Areas.FirstOrDefault(x => x.AreaNo == docNo.AreaNo);

                    if (doc != null)
                        doc.IsSelected = true;
                }

                CartonCombo.Clear();

                var cartons = dbAreas
                    .Where(x => x.OrderNo == SelectedSearchText && x.DocNo == SelectedDocNo && x.FinalDest == value)
                    .Select(x => x.Scancarton)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct()
                    .ToList();

                foreach (var carton in cartons)
                {
                    CartonCombo.Add(carton);
                }

                //CartonCombo = new ObservableCollection<string>(dbAreas
                //    .Where(x => x.OrderNo == SelectedSearchText && x.DocNo == SelectedDocNo && x.FinalDest == value)
                //    .Select(x => x.Scancarton)
                //    .Where(x => !string.IsNullOrEmpty(x))
                //    .Distinct()
                //    .ToList());
            }
            finally
            { IsCheck_Combo_area = false;
                IsSearchBusy = false;
            }
        }

        partial void OnSelectedCartonsChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
            { return; }

            if (IsCheck_Combo_area)
                return;

            IsCheck_Combo_area = true;
            isSearchBusy = true;

            try
            {
                foreach (var area in Areas)
                {
                    area.IsSelected = false;
                }

                var dbAreas = _service.GetCartonByOrder(SelectedSearchText, SelectedDocNo, SelectedFinal, value);

                foreach (var docNo in dbAreas)
                {
                    var doc = Areas.FirstOrDefault(x => x.AreaNo == docNo.AreaNo);

                    if (doc != null)
                        doc.IsSelected = true;
                }
            }
            finally
            {
                IsCheck_Combo_area = false;
                IsSearchBusy = false;
            }

        }

        [RelayCommand]
        private void ClearSearch()
        {
            //SearchText = "";
            SelectedSearchText = string.Empty;
            SelectedDocNo = string.Empty;
            SelectedFinal = string.Empty;
            SelectedCartons = string.Empty;
            SelectedPackingList = string.Empty;
            SelectedGroupType = string.Empty;

            foreach (var area in Areas)
            {
                area.IsSelected = false;
            }


        }

        public void SetDropTarget(AreaModel? target)
        {
            foreach (var area in Areas)
            {
                area.IsDropTarget = target != null && area.AreaNo == target.AreaNo;

            }

        }

        public void ClearDeopTarget()
        {
            foreach (var area in Areas)
            {
                area.IsDropTarget = false;
            }
        }

        public void EndDrag()
        {
            IsDragging = false;

            ClearDeopTarget();
        }

        public void CancelDrag()
        {
            IsDragging = false;

            ClearDeopTarget();
        }

        partial void OnSelectedPackingListChanged(string value)
        {
            IsCheck_Combo_area = true;
            IsSearchBusy = true;

            if (string.IsNullOrEmpty(value))
                return;

            try
            {

                foreach (var area in Areas)
                {
                    area.IsSelected = false;
                }

                var dbAreas = _service.GetAreaByPackinglist(value);

                foreach (var areaNo in dbAreas)
                {
                    var area = Areas.FirstOrDefault(x => x.AreaNo == areaNo.AreaNo);

                    if (area != null)
                        area.IsSelected = true;
                }



                var groupType = dbAreas
                    .Select(x => x.GroupType).Distinct().ToList();

                foreach (var carton in groupType)
                {
                    GroupTypeCombo.Add(carton);
                }

                SelectedGroupType = groupType[0].ToString();



            }
            finally
            {
                IsCheck_Combo_area = false;
                IsSearchBusy = false;
            }

        }

        partial void OnSelectedGroupTypeChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            foreach (var area in Areas)
            {
                area.IsSelected = false;
            }


        }

        [RelayCommand]

        private void Export()
        {
            // ดึงข้อมูลจาก Service
            //var Export_ = queryPallet.GetPalletDetails(SelectedArea.PalletsNo);

            var Export_ = queryPallet.GetPalletDetailsAndQTY(SelectedArea.PalletsNo);

            var dbAreas = _service.GetAreas();

            Area_name = dbAreas[0].AreaName.ToString();

            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add(SelectedArea.PalletsNo);

                    // =========================
                    // Header Information
                    // =========================

                    //Row 1 A1-C1
                    ws.Range("A1:C1").Merge();
                    ws.Cell(1, 1).Value = "Pallet No = " + SelectedArea.PalletsNo;

                    //Row 2 A2-C2
                    ws.Range("A2:C2").Merge();
                    ws.Cell(2, 1).Value = "Group Type = " + SelectedArea.GroupType;

                    //Row 3 A3-C3
                    ws.Range("A3:C3").Merge();
                    ws.Cell(3, 1).Value = "Group Key = " + SelectedArea.GroupKey;

                    //Row 4 A4-C4
                    ws.Range("A4:C4").Merge();
                    ws.Cell(4, 1).Value = "Max Capacity = " + SelectedArea.MaxCapital;

                    //Row 5 A5-C5
                    ws.Range("A5:C5").Merge();
                    ws.Cell(5, 1).Value = "Carton Count = " + SelectedArea.CartonCount;
                    //Row 1 F1-I1
                    ws.Range("F1:I1").Merge();
                    ws.Cell(1, 6).Value = "PrintDate = " + DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss");
                    // Row 1 D1-E1
                    ws.Range("D1:E1").Merge();
                    ws.Cell(1, 4).Value = "Area = " + SelectedArea.AreaName;
                    // Row 1 D2-E2
                    ws.Range("D2:E2").Merge();
                    ws.Cell(2, 4).Value = "Customer = " + Customer;
                    // Row 1 D3-E3
                    ws.Range("D3:E3").Merge();
                    ws.Cell(3, 4).Value = "Item Qty = " + AllQty;
                    //Header
                    ws.Cell(7, 1).Value = "No";
                    ws.Cell(7, 2).Value = "PO";
                    ws.Cell(7, 3).Value = "OrderNo";
                    ws.Cell(7, 4).Value = "PackNo";
                    ws.Cell(7, 5).Value = "Style";
                    ws.Cell(7, 6).Value = "Final";
                    ws.Cell(7, 7).Value = "Qty";
                    ws.Cell(7, 8).Value = "Carton";
                    ws.Cell(7, 9).Value = "BarcodeCarton";


                    // Style Table Header
                    var tableHeader = ws.Range("A7:I7");

                    tableHeader.Style.Font.Bold = true;
                    tableHeader.Style.Font.FontSize = 11;

                    tableHeader.Style.Alignment.Horizontal =
                        XLAlignmentHorizontalValues.Center;

                    tableHeader.Style.Alignment.Vertical =
                        XLAlignmentVerticalValues.Center;



                    // เส้นตาราง Header
                    tableHeader.Style.Border.TopBorder =
                        XLBorderStyleValues.Thin;

                    tableHeader.Style.Border.BottomBorder =
                        XLBorderStyleValues.Thin;

                    tableHeader.Style.Border.LeftBorder =
                        XLBorderStyleValues.Thin;

                    tableHeader.Style.Border.RightBorder =
                        XLBorderStyleValues.Thin;

                    // =========================
                    // Data
                    // =========================
                    int row = 8;

                    foreach (var item in Export_)
                    {
                        ws.Cell(row, 1).Value = row - 7;
                        ws.Cell(row, 2).Value = item.CustPORef;
                        ws.Cell(row, 3).Value = item.OrderNo;
                        ws.Cell(row, 4).Value = item.DocNo;
                        ws.Cell(row, 5).Value = item.Style;
                        ws.Cell(row, 6).Value = item.FinalDest;
                        ws.Cell(row, 7).Value = item.QTY;
                        ws.Cell(row, 8).Value = item.Scancarton;
                        ws.Cell(row, 9).Value = item.BarcodeCarton;


                        row++;

                    }

                    // =========================
                    // Style Data
                    // =========================

                    // row - 1 คือแถวสุดท้ายที่มีข้อมูล
                    if (row > 8)
                    {
                        var dataRange = ws.Range(8, 1, row - 1, 9);

                        // ขนาดตัวอักษร
                        dataRange.Style.Font.FontSize = 9;

                        // จัดข้อความ
                        dataRange.Style.Alignment.Vertical =
                            XLAlignmentVerticalValues.Center;

                        // เส้นตาราง
                        dataRange.Style.Border.TopBorder =
                            XLBorderStyleValues.Thin;

                        dataRange.Style.Border.BottomBorder =
                            XLBorderStyleValues.Thin;

                        dataRange.Style.Border.LeftBorder =
                            XLBorderStyleValues.Thin;

                        dataRange.Style.Border.RightBorder =
                            XLBorderStyleValues.Thin;
                    }


                    // =========================
                    // ปรับขนาด Column อัตโนมัติ
                    // =========================

                    ws.Columns().AdjustToContents();


                    // =========================
                    // ปรับความสูง Row
                    // =========================

                    //ws.Row(1).Height = 25;
                    //ws.Row(4).Height = 22;


                    // =========================
                    // Save File
                    // =========================

                    // Save ไฟล์ลง Desktop
                    string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Pallet " + SelectedArea.PalletsNo + ".xlsx");
                    workbook.SaveAs(filePath);

                    App.DialogService.ShowMessageAsync("✅ Complete ", "Export สำเร็จ ชื่อไฟล์ : " + SelectedArea.PalletsNo);

                }


            }
            catch (Exception ex)
            {

            }


        }

        [RelayCommand]

        private void Invoic()
        {
            var releaseService = new CartonReleaseService();

            var results = releaseService.SyncAndReleaseInvoicedCartons();

            if (results.Count > 0)
            {
                App.DialogService.ShowMessageAsync("✅ Complete ", "Update Invoiced Cartons Complete");
            }
            else
            {
                App.DialogService.ShowMessageAsync("❌ Fail ", "ไม่มี Invoice ให้ Update");

            }

        } 



        }
}
