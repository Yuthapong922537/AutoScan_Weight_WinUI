using AutoScan_Weight_WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ApplicationSettings;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AutoScan_Weight_WinUI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ShellPage : Page
    {
        public ShellViewModel ViewModel
        {
            get;
        }
        public static Frame AppFrame { get; private set; }
        public ShellPage()
        {
            InitializeComponent();

            AppFrame = ContentFrame;

            ViewModel = App.GetService<ShellViewModel>();
            this.DataContext = ViewModel;

        
                NavView.IsPaneOpen = false;

           
                NavView.SelectedItem = NavView.MenuItems
           .OfType<NavigationViewItem>()
           .FirstOrDefault(x => x.Tag?.ToString() == "ScanBarcode");

                ContentFrame.Navigate(typeof(ScanBarcodePage));
 

        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                switch (item.Tag)
                {
                    case "ScanBarcode":
                        ContentFrame.Navigate(typeof(ScanBarcodePage));
                        break;
                    case "SelectOrders":
                        ContentFrame.Navigate(typeof(SelectOrderPage));
                        break;
                    case "Delete":
                       ContentFrame.Navigate(typeof(DeletePage));
                       break;
                    case "Area":
                        ContentFrame.Navigate(typeof(AreaPage));
                        break;
                    //case "history":
                    //    ContentFrame.Navigate(typeof(HistoryPage));
                    //    break;
                    //default:
                        //break;
                }
            }
        }
    }
}
