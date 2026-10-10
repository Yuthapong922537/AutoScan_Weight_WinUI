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
using AutoScan_Weight_WinUI.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AutoScan_Weight_WinUI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    public sealed partial class SelectOrderPage : Page
    {
        private bool _isLoad;
        public SelectOrderViewModel ViewModel { get; }
        public SelectOrderPage()
        {
            InitializeComponent();
            ViewModel = App.GetService<SelectOrderViewModel>();
            ViewModel.SetDispatcherQueue(DispatcherQueue);

            DataContext = ViewModel;
        }
    

    private async void Area_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (_isLoad)
                return;
            if (e.AddedItems.Count == 0)
                return;

            if (sender is ComboBox comboBox)
            {
                var SelectedArea = comboBox.SelectedItem as string;

                if (!string.IsNullOrEmpty(SelectedArea))
                {
                    await ViewModel.UpdateKeepingArea(SelectedArea);
                }

            }

        }

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
