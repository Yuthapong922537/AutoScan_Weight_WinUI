using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Models
{
    public partial class AreaModel : ObservableObject
    {
        // =========================
        // Database Data
        // =========================

        [ObservableProperty]
        private string areaNo = "";

        [ObservableProperty]
        private string areaName = "";

        [ObservableProperty]
        private string customer = "";


        [ObservableProperty]
        private string warehouseName = "";

        [ObservableProperty]
        private string status = "";

        [ObservableProperty]
        private string palletsNo = "";

        [ObservableProperty]
        private string groupKey = "";

        [ObservableProperty]
        private string groupType = "";

        [ObservableProperty]
        private string cartonCount = "";

        [ObservableProperty]
        private string maxCapital = "";

        [ObservableProperty]
        private string palletStatus = "";

        [ObservableProperty]
        private bool isEmpty;

        [ObservableProperty]
        private bool isVisible = true;

        [ObservableProperty]
        private Visibility visibility = Visibility.Visible;

        // =========================
        // UI State
        // =========================

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool isDropTarget;

        [ObservableProperty]
        private bool isHovered;


        // =========================
        // UI Position
        // =========================

        [ObservableProperty]
        private double left;

        [ObservableProperty]
        private double top;


        //[ObservableProperty]
        //private AreaStatus status = AreaStatus.EMPTY;

        [ObservableProperty]
        private bool canDrag;


    }
}
