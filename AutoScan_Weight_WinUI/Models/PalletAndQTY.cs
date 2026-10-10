using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Models
{
    public partial class PalletAndQTY : ObservableObject
    {

        [ObservableProperty]
        private string areaNo = "";
        [ObservableProperty]
        private string areaName = "";
        [ObservableProperty]
        private string palletID = "";
        [ObservableProperty]
        private string palletNo = "";
        [ObservableProperty]
        private string docNo = "";
        [ObservableProperty]
        private string styleID = "";
        [ObservableProperty]
        private string orderNo = "";
        [ObservableProperty]
        private string style = "";
        [ObservableProperty]
        private string custPORef = "";
        [ObservableProperty]
        private string lotRef = "";
        [ObservableProperty]
        private string finalDest = "";
        [ObservableProperty]
        private string warehouse = "";
        [ObservableProperty]
        private string barcodeCarton = "";
        [ObservableProperty]
        private string scancarton = "";
        [ObservableProperty]
        private string invoiceNo = "";
        [ObservableProperty]
        private string customer = "";
        [ObservableProperty]
        private string seqlnPallet = "";
        [ObservableProperty]
        private string placeStatus = "";
        [ObservableProperty]
        private DateTime createDate = DateTime.Now;
        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private string groupType = string.Empty;
        [ObservableProperty]
        private string groupKey = string.Empty;

        [ObservableProperty]
        private string qTY = string.Empty;
    }
}
