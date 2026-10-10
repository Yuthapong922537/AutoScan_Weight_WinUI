using AutoScan_Weight_WinUI.Models;
using AutoScan_Weight_WinUI.Services;
using AutoScan_Weight_WinUI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AutoScan_Weight_WinUI.Views
{
    

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ScanBarcodePage : Page
    {

        private bool _isLoad;
        public ScanBarcodeViewModel ViewModel { get; }
        public ScanBarcodePage()
        {
            InitializeComponent();
            ViewModel = App.GetService<ScanBarcodeViewModel>();
            ViewModel.SetDispatcherQueue(DispatcherQueue);

          
            DataContext = ViewModel;
        }

        private async void txt_BarcodeCarton_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                _isLoad = true;
                string barcode = BarcodeCartonTextBox.Text.Trim();
                await ViewModel.ScanBarcode(barcode);
                _isLoad = false;
            }
        }
        private async void Area_SelectionChanged(object sender, SelectionChangedEventArgs e) 
        {

            if (_isLoad)
                return;
            if(e.AddedItems.Count == 0)
                return;

            if (sender is ComboBox comboBox)
            { 
            var SelectedArea = comboBox.SelectedItem as string;

                string barcode = BarcodeCartonTextBox.Text.Trim();

                if (!string.IsNullOrEmpty(SelectedArea))
                {
                    await ViewModel.UpdateKeepingArea(barcode,SelectedArea);
                }

            }
        
        }

        //private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        //{
        //    string weightFromSerial = serialPort.ReadLine().Trim();

        //    DispatcherQueue.TryEnqueue(() =>
        //    {
        //        ViewModel.Weight = weightFromSerial;
        //    });
        //}
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ViewModel.Activate();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.Deactivate();

            base.OnNavigatedFrom(e);
        }

    }
}
