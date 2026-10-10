using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Models
{
    public partial class AlreadyOf : ObservableObject
    {

        [ObservableProperty]
        private string pack_of;
        [ObservableProperty]
        private string of_all;
        [ObservableProperty]
        private string carton_of;


    }
}
