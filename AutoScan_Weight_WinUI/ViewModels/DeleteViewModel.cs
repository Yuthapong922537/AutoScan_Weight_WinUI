using AutoScan_Weight_WinUI.Models;
using AutoScan_Weight_WinUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AutoScan_Weight_WinUI.Services.PalletOptimizerService;


namespace AutoScan_Weight_WinUI.ViewModels
{
    public partial class DeleteViewModel : ObservableRecipient
    {
        private readonly QueryAll queryAll;
        private readonly QueryPallet queryPallet = new();
        private readonly ISerialPortService _serialService;

        private List<CancellableDocNoItem> cartons_pack = new();

        private List<PalletDetailModel> cartons_order = new();

        private List<PalletDetailModel> PalletDetails = new();
        private List<DataCarton> data_cartons = new();
        private List<DataCarton> data_delete = new();

        [ObservableProperty]
        private ObservableCollection<string> packingList;

        [ObservableProperty]
        private string selectedPacking = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> order;
        [ObservableProperty]
        private string selectedOrder = string.Empty;
        [ObservableProperty]
        private ObservableCollection<string> pack;
        [ObservableProperty]
        private string selectedPack = string.Empty;
        [ObservableProperty]
        private ObservableCollection<string> finalDest;
        [ObservableProperty]
        private string selectedFinalDest = string.Empty;
        [ObservableProperty]
        private ObservableCollection<string> carton;
        [ObservableProperty]
        private string selectedCarton = string.Empty;

        [ObservableProperty]
        private string barcodeCarton = string.Empty;


        [ObservableProperty]
        private ObservableCollection<PalletDetailModel> dataWeight = new();

        bool IsCheck_Combo = false;

        string BarCodeCarton = string.Empty;


        public DeleteViewModel(QueryAll queryAll , ISerialPortService serialService)
        { 
            this.queryAll = queryAll;
            this._serialService = serialService;

            from_Load();
        
        }

        public void from_Load()
        { 
        
            //cartons_order = queryAll.GetOrderNo();
            //PackingList = new ObservableCollection<string>(cartons_order
            //    .Select(x => x.OrderNo)
            //    .Where(s => s != null)
            //    .Distinct().ToList());

            var cancelService = new CartonCancelService();
            cartons_pack = cancelService.GetCancellableDocNos();

            PackingList = new ObservableCollection<string>(cartons_pack.Select(c => c.DocNo).ToList());


            cartons_order = queryPallet.Orderwithdraw();

            Order = new ObservableCollection<string>(cartons_order
                .Select(x => x.OrderNo)
                .Where(s => s != null)
                .Distinct().ToList());

        }

        partial void OnSelectedPackingChanged(string value)
        {
            if(value == null)
                return;

           var ShowPacking = new QueryPallet();

            PalletDetails = ShowPacking.ShowPalletDetails(SelectedPacking);

            DataWeight = new ObservableCollection<PalletDetailModel>(PalletDetails);


        }

        partial void OnSelectedOrderChanged(string value)
        {
            if (value == null)
                return;

            Pack = new ObservableCollection<string>(cartons_order
                .Where(x => x.OrderNo  == SelectedOrder )
                .Select(x => x.DocNo)
                .Where(s => s != null)
                .Distinct().ToList());


        }

        partial void OnSelectedPackChanged(string value)
        {
            if (value == null)
                return;
            FinalDest = new ObservableCollection<string>(cartons_order
                .Where(x => x.OrderNo == SelectedOrder && x.DocNo == SelectedPack  )
                .Select(x => x.FinalDest)
                .Where(s => s != null)
                .Distinct().ToList());
        }

        partial void OnSelectedFinalDestChanged(string value)
        {
            if (value == null)
                return;

            Carton = new ObservableCollection<string>(cartons_order
                .Where(x => x.OrderNo == SelectedOrder && x.DocNo == SelectedPack && x.FinalDest == SelectedFinalDest)
                .Select(x => x.Scancarton)
                .Where(s => s != null)               
                .Distinct()
                .OrderBy(s => s)
                .ToList());
        }

        partial void OnSelectedCartonChanged(string value)
        {
            if (value == null)          
            return;

            var BarcodeCarton = new ObservableCollection<string>(cartons_order
               .Where(x => x.OrderNo == SelectedOrder && x.DocNo == SelectedPack && x.FinalDest == SelectedFinalDest && x.Scancarton == SelectedCarton)
               .Select(x => x.BarcodeCarton)
               .Where(s => s != null)
               .Distinct().ToList());

            BarCodeCarton = BarcodeCarton.FirstOrDefault() ?? string.Empty;

        }

        [RelayCommand]
        public async Task Delete()
        {
            if(string.IsNullOrEmpty(SelectedPacking))
            {
                App.DialogService.ShowMessageAsync("❌ Error", "กรุณาเลือก Packinlist ที่ต้องการลบ");
                return;
            }

            var cancelService = new CartonCancelService();

            var results = cancelService.CancelDocNo(SelectedPacking);

            App.DialogService.ShowMessageAsync("✅ Delete", "Delete Complete");

            var needPickup = results.Where(r => r.RequiresPhysicalPickup).ToList();
            if (needPickup.Count > 0)
            {
                // แจ้งเตือน: Carton เหล่านี้ยังวางอยู่บน Pallet จริง ต้องไปเก็บออก
            }

        }

        [RelayCommand]
        public async Task DeleteText()
        {
            if (string.IsNullOrEmpty(SelectedOrder) || string.IsNullOrEmpty(SelectedPack) || string.IsNullOrEmpty(SelectedFinalDest) || string.IsNullOrEmpty(SelectedCarton) || string.IsNullOrEmpty(BarCodeCarton))
            {
                App.DialogService.ShowMessageAsync("❌ Error", "กรุณาเลือกข้อมูลให้ครบ");
                return;
            }

            var withdrawService = new CartonWithdrawService();
            var result = withdrawService.WithdrawCarton(BarCodeCarton);

            App.DialogService.ShowMessageAsync("✅ Withdraw", "ตัด Carton เรียบร้อย");

        }
    }
}
