using AutoScan_Weight_WinUI.Models;
using AutoScan_Weight_WinUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static AutoScan_Weight_WinUI.Services.PalletOptimizerService;

namespace AutoScan_Weight_WinUI.ViewModels
{
    public partial class ScanBarcodeViewModel : ObservableRecipient
    {
        private readonly QueryAll queryAll;
        private readonly PalletOptimizerService qureyPallet;
        private readonly UpdateInvoiceService upDateInvoice = new();
        private readonly DispatcherTimer _timer;
        private readonly ISerialPortService _serialService;
        //private readonly DispatcherQueue _dispatcherQueue;
        private DispatcherQueue? _dispatcherQueue;


        private List<DataCarton> cartons = new();
        private List<DataCarton> cartons_Barcode = new();
        private List<DataCarton> cartons_Detail = new();
        private List<DataCarton> cartons_KeepingArea = new();
        private List<DataCarton> cartons_Weight = new();
        private List<DataCarton> cartons_Dairy = new();
       
        private List<DataCarton> pallet_No = new();
        


        [ObservableProperty]
        private string barcodeCarton;

        [ObservableProperty]
        private ObservableCollection<DataCarton> dataWeight = new ();
        [ObservableProperty]
        private ObservableCollection<DataCarton> dataDetails = new ();

        [ObservableProperty]
        private string textCarton  = string.Empty;
        [ObservableProperty]
        private string textWeight = string.Empty;
        [ObservableProperty]
        private string textArea = string.Empty;

        [ObservableProperty]
           private string textKeepArea = string.Empty;
        [ObservableProperty]
        private string textCartonOf = string.Empty;
        [ObservableProperty]
        private string textOfAll = string.Empty;                
        [ObservableProperty]
        private string textAlready = string.Empty;

        [ObservableProperty]
        private bool isConnected;
        [ObservableProperty]
        private string receiveText = "";

        [ObservableProperty]
        private string statusWeight_Machine = string.Empty;

        

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


        [ObservableProperty]
        private string currentTime = DateTime.Now.ToString("HH:mm:ss");
        [ObservableProperty]
        private string currentDate = DateTime.Now.ToString("dd/MM/yyyy");

        [ObservableProperty]
        private string textSumSize = string.Empty;
        [ObservableProperty]
        private string textSumQty = string.Empty;
        [ObservableProperty]
        private string text_Dairy = string.Empty;

        public string SumSize { get; set; }
        public string SumQty { get; set; }


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



        public ScanBarcodeViewModel(QueryAll queryAll, ISerialPortService serialService , PalletOptimizerService palletOptimizerService)
        {
            this.queryAll = queryAll;
            this._serialService = serialService;
            this.qureyPallet = palletOptimizerService;


            //_serialService.DataReceived += SerialService_DataReceived;

            //_serialService.DataReceived += OndataReceived;
            _serialService.Connect("COM3",9600);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            //AddKeepingArea();
            FormLoad();
        }
        public void Activate()
        {
            _serialService.DataReceived += SerialService_DataReceived;
        }

        public void Deactivate()
        {
            _serialService.DataReceived -= SerialService_DataReceived;
        }

        void FormLoad()
        {
            Refresh();

            //bool result = _serialService.Connect("COM3",9600);

            

            if (_serialService.IsConnected == false)
            {
                //App.DialogService.ShowMessageAsync("❌ Error connecting to serial port", "Please Check Weight Machine");
                StatusWeight_Machine = "❌ Error connecting to serial port";

                return;
            }

            StatusWeight_Machine = "✅ Connected to serial port";
        }
        public async Task ScanBarcode(string barcode)
        {
            if(barcode == string.Empty)
            {
                BarcodeCarton = string.Empty;
                App.DialogService.ShowMessageAsync("❌ Fail the barcode", "Please enter a valid barcode.");
                return;
            }

            if(barcode.Count() != 11)
            {
                BarcodeCarton = string.Empty;
                App.DialogService.ShowMessageAsync("❌ Fail the barcode", "Barcode must be 11 characters long.");
                return;
            }
            // Check QA
            cartons_Barcode = queryAll.CheckRowInQA(barcode);
            if (cartons_Barcode == null || cartons_Barcode.Count == 0)
            {
                BarcodeCarton = string.Empty;
                App.DialogService.ShowMessageAsync("❌ Fail the barcode", "Barcode not found in QA.");
                return;
            }
            if (cartons_Barcode[0].BarcodeCarton == string.Empty)
            {
                BarcodeCarton = string.Empty;
                App.DialogService.ShowMessageAsync("❌ Fail the barcode", "Barcode not found in QA.");
                return;

            }
            
            cartons = queryAll.GetDataCarton(barcode);

            if (!string.IsNullOrEmpty(cartons[0].InvoiceNo))
            {
                App.DialogService.ShowMessageAsync("❌ Fail the Pallet", "งานกล่องนี้มี Invoice เเล้ว");


                return;
            }

            DataWeight = new ObservableCollection<DataCarton>(cartons);

            if(cartons.Count ==0)
            {
                App.DialogService.ShowMessageAsync("❌ Fail the barcode", "No data found for the scanned barcode.");
                return;
            }


            string OrderNo = cartons[0].OrderNo;
            string PackNo = cartons[0].PackNo;
            string FinalDest = cartons[0].FinalDest;

            //Update Invoice
            //upDateInvoice.UpdateInvoice();

            //var releaseService = new CartonReleaseService();
            //var results = releaseService.SyncAndReleaseInvoicedCartons();
            // results.Count == 0 ก็ปกติ แปลว่ารอบนี้ไม่มี Invoice ใหม่เข้ามา

            // Optmizer Pallet
            var result = qureyPallet.OptimizePallets(PackNo);

            pallet_No = queryAll.getPalletNo(barcode);

            if (pallet_No.Count == 0)
            {
                App.DialogService.ShowMessageAsync("❌ Fail the Pallet", "ไม่สามารจองพาเรทได้");
                return;
            }
            TextArea = pallet_No[0].PalletNo.ToString();

            TextKeepArea = pallet_No[0].KeepingArea.ToString();

            TextCartonOf = cartons[0].Carton.Split("-")[1].Substring(1,3);

            cartons_Detail = queryAll.GetDataDetail(barcode);
            DataDetails = new ObservableCollection<DataCarton>(cartons_Detail);

            TextSumSize = cartons_Detail.Count.ToString();

            TextSumQty = new ObservableCollection<string>(cartons_Detail.Select(x => x.Qty)).Sum(x => Convert.ToInt32(x)).ToString();
            
            //SumSize = cartons_Detail.Sum(c => c.Size).ToString();
            //SumQty = cartons_Detail.Sum(c => c.Qty).ToString();

            TextCarton = cartons_Barcode[0].BarcodeCarton;

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

            //if (cartons[0].KeepingArea == "รอเก็บ")
            //{
            //    KeepingArea_colorBorder = new SolidColorBrush(Colors.IndianRed);
            //}
            //else
            //{
            //    KeepingArea_colorBorder = new SolidColorBrush(Colors.DarkGreen);
            //}

            

            cartons = queryAll.GetPackOf(barcode);

            string packOf = string.Empty;
       

            packOf = string.Join(", ", cartons.Select(c => c.PackNo));
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                
            cartons = queryAll.GetrowOfAll(packOf);
            TextOfAll= new ObservableCollection<string>(cartons.Select(x => x.PackNo)).Count.ToString();

            cartons = queryAll.GetCartonInstore(OrderNo , PackNo , FinalDest);
            TextAlready = cartons.Count.ToString();

            cartons_Dairy = queryAll.GetDairy();
            Text_Dairy = cartons_Dairy.Count.ToString();

            ColorRight = new SolidColorBrush(Colors.LightSeaGreen);
            ColorCarton = new SolidColorBrush(Colors.LightGreen);
            ColorCartonOf = new SolidColorBrush(Colors.ForestGreen);
            ColorOfAll = new SolidColorBrush(Colors.DarkGreen);
            ColorAlready = new SolidColorBrush(Colors.GreenYellow);

            
        }

        public async Task UpdateKeepingArea(string barcode, string area)
        {
            if (BarcodeCarton == string.Empty)
                TextArea = string.Empty;
                return;

            cartons_Detail = queryAll.GetDataDetail(barcode);

            string OrderNo = cartons_Detail[0].OrderNo;
            string PackNo = cartons_Detail[0].PackNo;
            string FinalDest = cartons_Detail[0].FinalDest;
            string BarcodeCartons = cartons_Detail[0].Carton;

            //cartons_KeepingArea = queryAll.UpdateArea(OrderNo, PackNo, FinalDest, BarcodeCartons, area);    
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

        public async void SerialService_DataReceived(string data)
        {
            if (_dispatcherQueue == null)
            {
                //Debug.WriteLine("DispatcherQueue is null");
                return;
            }

            if (BarcodeCarton == string.Empty)
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

            if(DataWeight.Count == 0)
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

            // Update PLACED
            cartons_Weight = queryAll.UpdatePlaceStatus(BarcodeCarton);


        }
        

        public void SetDispatcherQueue(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue;
        }

        public void Refresh()
        {
            BarcodeCarton = string.Empty;
            TextCarton = string.Empty;
            TextWeight = string.Empty;
            TextArea = string.Empty;
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

        

    }
}
