using AutoScan_Weight_WinUI.Models;
using AutoScan_Weight_WinUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.AppBroadcasting;
using Microsoft.UI.Dispatching;




namespace AutoScan_Weight_WinUI.ViewModels
{
    public partial class SelectOrderViewModel : ObservableRecipient
    {

        private readonly QueryAll queryAll;
        private readonly PalletOptimizerService qureyPallet;
        private readonly UpdateInvoiceService upDateInvoice = new();
        private readonly DispatcherTimer _timer;
        private readonly ISerialPortService _serialService;
        //private readonly DispatcherQueue _dispatcherQueue;
        private DispatcherQueue? _dispatcherQueue;

        private List<DataCarton> cartons = new();
        private List<DataCarton> FinalDest_ = new();
        private List<DataCarton> Allcartons = new();
        private List<DataCarton> Detail = new();
        private List<DataCarton> Carton_Aleady = new();
        private List<DataCarton> Carton_Barcode = new();
        private List<DataCarton> Carton_Dairy = new();
        private List<DataCarton> cartons_Weight = new();
        private List<DataCarton> cartons_KeepingArea = new();
        private List<DataCarton> pallet_No = new();
        // Properties
        [ObservableProperty]
        private string statusWeight_Machine = string.Empty;
        [ObservableProperty]
        private string currentTime = DateTime.Now.ToString("HH:mm:ss");
        [ObservableProperty]
        private string currentDate = DateTime.Now.ToString("dd/MM/yyyy");

        //Add Combobox
        [ObservableProperty]
        private ObservableCollection<string> orderNo;
        [ObservableProperty]
        private string selectedOrderNo = string.Empty;
        [ObservableProperty]
        private ObservableCollection<string> packNo;
        [ObservableProperty]
        private string selectedPackNo = string.Empty;
        [ObservableProperty]
        private ObservableCollection<string> finalDest;
        [ObservableProperty]
        private string selectedFinalDest = string.Empty;
        [ObservableProperty]
        private ObservableCollection<string> carton;
        [ObservableProperty]
        private string selectedCarton = string.Empty;


        //DataGridView
        [ObservableProperty]
        private ObservableCollection<DataCarton> dataWeight = new();
        [ObservableProperty]
        private ObservableCollection<DataCarton> dataDetails = new();

        bool IsCheck_Combo = false;

        // OffAll and alredyCarton
        [ObservableProperty]
        private string textCartonOf = string.Empty;
        [ObservableProperty]
        private string textOfAll = string.Empty;
        [ObservableProperty]
        private string textAlready = string.Empty;
        [ObservableProperty]
        private string text_Dairy = string.Empty;

        // Text row 2
        [ObservableProperty]
        private string textCarton = string.Empty;
        //TextWeight
        [ObservableProperty]
        private string textWeight =string.Empty;
        // Text sum
        [ObservableProperty]
        private string textSumSize = string.Empty;
        [ObservableProperty]
        private string textSumQty = string.Empty;
        // Color
        [ObservableProperty]
        private SolidColorBrush colorRight = new SolidColorBrush(Colors.Red);
        [ObservableProperty]
        private SolidColorBrush colorCarton = new SolidColorBrush(Colors.Red);
        [ObservableProperty]
        private SolidColorBrush colorWeight = new SolidColorBrush(Colors.Red);
        [ObservableProperty]
        private SolidColorBrush colorCartonOf = new SolidColorBrush(Colors.Red);
        [ObservableProperty]
        private SolidColorBrush colorOfAll = new SolidColorBrush(Colors.Red);
        [ObservableProperty]
        private SolidColorBrush colorAlready = new SolidColorBrush(Colors.Red);
        [ObservableProperty]
        private SolidColorBrush keepingArea_colorBorder = new SolidColorBrush(Colors.Transparent);

        //ComBoBoxArea
        [ObservableProperty]
        private string textArea = string.Empty;
        [ObservableProperty]
        private string textKeepArea = string.Empty;
        [ObservableProperty]
        private ObservableCollection<String> areaList = new()
        { "รอเก็บ", "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9", "A10", "A11", "A12",
          "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8", "B9", "B10", "B11", "B12",
          "C1", "C2", "C3", "C4", "C5", "C6", "C7", "C8", "C9", "C10", "C11", "C12",
          "D1", "D2", "D3", "D4", "D5", "D6", "D7","D8","D9","D10","D11","D12",
          "E1","E2","E3","E4","E5","E6","E7","E8","E9",
          "F1","F2","F3","F4","F5","F6",
          "G1","G2","G3","G4","G5","G6",
          "โกดังจักร"

        };

        public SelectOrderViewModel(QueryAll queryAll, ISerialPortService serialService, PalletOptimizerService palletOptimizerService)
        {
            this.queryAll = queryAll;
            _serialService = serialService;
            qureyPallet = palletOptimizerService;

            _serialService.DataReceived += SerialService_DataReceived;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            FromLoad();
        }

        public void Activate()
        {
            _serialService.DataReceived += SerialService_DataReceived;
        }

        public void Deactivate()
        {
            _serialService.DataReceived -= SerialService_DataReceived;
        }

        public void FromLoad()
        {
            Refresh();

           // bool result = _serialService.Connect("COM3", 9600);

            Allcartons = queryAll.GetOrderNo();
            OrderNo = new ObservableCollection<string>(Allcartons.Select(x => x.OrderNo).Where(s => s != null).Distinct().ToList());
            if (_serialService.IsConnected)
            {
                StatusWeight_Machine = "✅ Connected to serial port";
            }
            else
            {
                StatusWeight_Machine = "❌ Error connecting to serial port";
            }
        }

        partial void OnSelectedOrderNoChanged(string value)
        {
            Refresh();

            IsCheck_Combo = true;

            SelectedPackNo = null;
            SelectedFinalDest = null;
            SelectedCarton = null;

            KeepingArea_colorBorder = new SolidColorBrush(Colors.Transparent);
            //PackNo.Clear();

            PackNo = new ObservableCollection<string>(Allcartons
                .Where(x => x.OrderNo == value)
                .Select(x => x.PackNo)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList());

            DataWeight = null;
            DataDetails = null;

            IsCheck_Combo = false;
        }

        partial void OnSelectedPackNoChanged(string value)
        {

            if (IsCheck_Combo || string.IsNullOrEmpty(value))
                return;

            IsCheck_Combo = true;

            SelectedFinalDest = null;
            SelectedCarton = null;

            DataWeight = null;
            DataDetails = null;

            KeepingArea_colorBorder = new SolidColorBrush(Colors.Transparent);

            //FinalDest.Clear();

            FinalDest = new ObservableCollection<string>(Allcartons
                .Where (x => x.OrderNo == SelectedOrderNo && x.PackNo == value)
                .Select(x => x.FinalDest)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct().
                ToList());

            IsCheck_Combo = false;
        }

        partial void OnSelectedFinalDestChanged(string value)
        {

            if (IsCheck_Combo || string.IsNullOrEmpty(value))
                return;

            IsCheck_Combo = true;

            SelectedCarton = null;
            DataDetails = null;

            KeepingArea_colorBorder = new SolidColorBrush(Colors.Transparent);
            //Carton.Clear();

            Carton = new ObservableCollection<string>(Allcartons
                .Where(x => x.OrderNo == SelectedOrderNo && x.PackNo == SelectedPackNo && x.FinalDest == value)
                .Select (x => x.Carton)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct().
                ToList());

            FinalDest_ = queryAll.GetFinal_Select(SelectedOrderNo, SelectedPackNo,value);
            
            DataWeight = new ObservableCollection<DataCarton>(FinalDest_);
            
            IsCheck_Combo = false;

        }

        partial void OnSelectedCartonChanged(string value)
        { 
        if(IsCheck_Combo || string.IsNullOrEmpty(value))
                return;

            IsCheck_Combo= true;

            cartons = queryAll.GetCarton_Select(SelectedOrderNo, SelectedPackNo, SelectedFinalDest, value);

            if (!string.IsNullOrEmpty(cartons[0].InvoiceNo))
            {
                App.DialogService.ShowMessageAsync("❌ Fail the Pallet", "งานกล่องนี้มี Invoice เเล้ว");

                SelectedOrderNo = string.Empty;
                SelectedPackNo = string.Empty;
                SelectedFinalDest = string.Empty;
                SelectedCarton = string.Empty;
                DataWeight = null;
                return;
            }

            DataWeight = new ObservableCollection<DataCarton>(cartons);

            // TextWeight
            if (cartons[0].Weight.ToString() == "0")
            {
                TextWeight = cartons[0].Weight.ToString();
                ColorWeight = new SolidColorBrush(Colors.Red);


            }
            else
            {
                TextWeight = cartons[0].Weight.ToString();
                ColorWeight = new SolidColorBrush(Colors.LightGreen);

            }

            // TextArea
            //if (cartons[0].KeepingArea == "รอเก็บ")
            //{
            //    KeepingArea_colorBorder = new SolidColorBrush(Colors.IndianRed);
            //}
            //else
            //{
            //    KeepingArea_colorBorder = new SolidColorBrush(Colors.DarkGreen);
            //}



            //Update Invoice
            upDateInvoice.UpdateInvoice();

            // Optimazer Pallet
            var result = qureyPallet.OptimizePallets(SelectedPackNo);

            Detail = queryAll.GetDataDetail_Select(SelectedOrderNo,SelectedPackNo,SelectedFinalDest,SelectedCarton);
            DataDetails = new ObservableCollection<DataCarton>(Detail);
    
            // Textsum
            TextSumSize = Detail.Count.ToString();

            TextSumQty = new ObservableCollection<string>(Detail.Select(x => x.Qty)).Sum(x => Convert.ToInt32(x)).ToString();

            // TextAlready
            Carton_Aleady = queryAll.GetAlrady_Select(SelectedOrderNo, SelectedPackNo, selectedFinalDest);
            TextAlready = Carton_Aleady.Count.ToString();
            // TextBarcode
            Carton_Barcode = queryAll.GetBarcode_Select(SelectedOrderNo, SelectedPackNo, SelectedFinalDest,SelectedCarton);
            TextCarton = Carton_Barcode[0].BarcodeCarton.ToString();
            TextCartonOf = Carton_Barcode[0].Carton.Split("-")[1].Substring(1, 3);
            TextOfAll = Carton_Barcode[0].Qty.ToString();
            // color
            ColorRight = new SolidColorBrush(Colors.LightSeaGreen);
            ColorCarton = new SolidColorBrush(Colors.LightGreen);
            ColorCartonOf = new SolidColorBrush(Colors.ForestGreen);
            ColorOfAll = new SolidColorBrush(Colors.DarkGreen);
            ColorAlready = new SolidColorBrush(Colors.GreenYellow);

            Carton_Dairy = queryAll.GetDairy();
            Text_Dairy = Carton_Dairy.Count.ToString();

            pallet_No = queryAll.getPalletNo(TextCarton);

            if (pallet_No.Count == 0)
            {
                App.DialogService.ShowMessageAsync("❌ Fail the Pallet", "ไม่สามารถจองพาเรทได้");
                return;
            }
            TextArea = pallet_No[0].PalletNo.ToString();
            TextKeepArea = pallet_No[0].KeepingArea.ToString();

            IsCheck_Combo = false;

        }

        public async Task UpdateKeepingArea(string area)
        {
            if(SelectedOrderNo == string.Empty || SelectedPackNo == string.Empty || selectedFinalDest == string.Empty ||SelectedCarton == string.Empty)
                return;
            //cartons_KeepingArea = queryAll.UpdateArea(DataWeight.Last().OrderNo, DataWeight.Last().PackNo, DataWeight.Last().FinalDest, DataWeight.Last().Carton, area);    
        }

        private void Timer_Tick(object sender, object e)
        {
            UpdateDateTime();
        }

        private void UpdateDateTime()
        {
            CurrentDate = DateTime.Now.ToString("dd/MM/yyyy");
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        }

        public void SetDispatcherQueue(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue;
        }
        public void Refresh()
        {
            //BarcodeCarton = string.Empty;
            TextCarton = string.Empty;
            TextWeight = string.Empty;
            TextArea = string.Empty;
            TextSumSize = string.Empty;
            TextSumQty = string.Empty;
            TextCartonOf = string.Empty;
            TextOfAll = string.Empty;
            TextAlready = string.Empty;
            ColorRight = new SolidColorBrush(Colors.Red);
            ColorCarton = new SolidColorBrush(Colors.Red);
            ColorWeight = new SolidColorBrush(Colors.Red);
            ColorCartonOf = new SolidColorBrush(Colors.Red);
            ColorOfAll = new SolidColorBrush(Colors.Red);
            ColorAlready = new SolidColorBrush(Colors.Red);

        }

        public async void SerialService_DataReceived(string data)
        {
            if (_dispatcherQueue == null)
            {
                //Debug.WriteLine("DispatcherQueue is null");
                return;
            }

            if (SelectedCarton == string.Empty)
                return;

            _dispatcherQueue?.TryEnqueue(() =>
            {
                var clearWeight = data.Replace("\r", "").Replace("\n", "").Trim();
                string Sub_Weight = clearWeight.Substring(7, 5);
                double Value_Weight = Convert.ToDouble(Sub_Weight);
                TextWeight = Value_Weight.ToString();

                if (TextWeight.Trim() == "0")
                {
                    ColorWeight = new SolidColorBrush(Colors.Red);
                }
                else
                {
                    ColorWeight = new SolidColorBrush(Colors.LightGreen);
                }

            });

            if (DataWeight.Count == 0)
                return;

            _dispatcherQueue?.TryEnqueue(() =>
            {
                var clearWeight = data.Replace("\r", "").Replace("\n", "").Trim();
                string Sub_Weight = clearWeight.Substring(7, 5);
                double Value_Weight = Convert.ToDouble(Sub_Weight);
                var row = DataWeight.Last();
                row.Weight = Value_Weight.ToString();
            });

            cartons_Weight = queryAll.CheckWeight(DataWeight.Last().OrderNo, DataWeight.Last().PackNo, DataWeight.Last().FinalDest, DataWeight.Last().Carton);

            if (cartons_Weight.Count == 0)
            {
               cartons_Weight = queryAll.InsertWeight(DataWeight.Last().OrderNo, DataWeight.Last().PackNo, DataWeight.Last().FinalDest, DataWeight.Last().Carton, TextWeight, TextKeepArea);
            }
            else if (cartons_Weight.Count == 1)
            {
               cartons_Weight = queryAll.UpdateWeight(DataWeight.Last().OrderNo, DataWeight.Last().PackNo, DataWeight.Last().FinalDest, DataWeight.Last().Carton, TextWeight, TextKeepArea);
            }
            else
            {
                App.DialogService.ShowMessageAsync("❌ Fail the Data", "Not Update Weight.");
            }

            cartons_Weight = queryAll.UpdatePlaceStatus(TextCarton);
        }
    }
}
