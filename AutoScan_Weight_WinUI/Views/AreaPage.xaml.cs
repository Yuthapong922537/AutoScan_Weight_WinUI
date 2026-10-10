using AutoScan_Weight_WinUI.Models;
using AutoScan_Weight_WinUI.ViewModels;
using AutoScan_Weight_WinUI.Views.Controls;
using Microsoft.UI;
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
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AutoScan_Weight_WinUI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AreaPage : Page
    {
        public AreaViewModel ViewModel { get; }
        public AreaModel? DragSourceArea { get; set;  }

        private AreaModel? _dragSource;

        private AreaControl? _currentDropTarget;

        private readonly Dictionary<AreaModel, AreaControl> _controls = new();

        private AreaControl? _selectedControl;
        private AreaModel? _selectedArea;
        public AreaPage()
        {
            InitializeComponent();

            ViewModel = App.GetService<AreaViewModel>();

            DataContext = ViewModel;
    
            CreateAreas();
        }
        private void CreateAreas()
        {
            foreach (var area in ViewModel.Areas)
            {
                AreaControl control = new AreaControl();

                control.DataContext = area;

                control.DragStarted += AreaControl_DragStarted;

                control.Dropped += AreaControl_Dropped;
                _controls.Add(area, control);

                control.DragOverArea += AreaControl_DragOverArea;

                control.Clicked += AreaControl_Clicked;

                Canvas.SetLeft(control, area.Left);

                Canvas.SetTop(control, area.Top);

                AreaCanvas.Children.Add(control);
            }

        }

        private void AreaControl_DragStarted(object sender, AreaModel area)
        {
            _dragSource = area;

            if (_controls.TryGetValue(area, out AreaControl? control))
            {
                control.SetHighlight(true);
            }
        }

        private async void AreaControl_Dropped(object? sender, AreaModel target)
        {
            
            if(_dragSource == null )
                return;

            if (_dragSource == target)
                return;

            bool success = ViewModel.SwapArea(_dragSource, target);

            if (!success)
            {
                App.DialogService.ShowMessageAsync("❌ Fail ", "Try again");
            }


            if (_controls.TryGetValue(_dragSource, out var sourceControl))
            {
                await sourceControl.FlashAsync();
                await Task.WhenAll(sourceControl.FlashBodyAsync());
            }

            if (_controls.TryGetValue(target, out var targetControl))
            {
                await targetControl.FlashAsync();
                await Task.WhenAll(targetControl.FlashBodyAsync());
            }

            if (_controls.TryGetValue(_dragSource , out var control))
            {
                control.SetHighlight(false);
                control.RefreshState();
            }

            if (_currentDropTarget != null)
            {
                _currentDropTarget.SetDropTarget(false);
                _currentDropTarget.RefreshState();
                _currentDropTarget = null;
            }

            _dragSource = null;

        }

        public void AreaControl_DragOverArea(object? sender, AreaModel target)
        {
            //if (_dragSource == null || _dragSource == target)
            //    return;

            //if (_currentDropTarget != null)
            //{
            //    _currentDropTarget.SetDropTarget(false);
            //}

            //if(_controls.TryGetValue(target, out var control))
            //{
            //    control.SetDropTarget(true);
            //    _currentDropTarget = control;
            //}
            ViewModel.SetDropTarget(target);
        }

        private void AreaControl_Clicked(object sender, AreaModel area )
        {
            if (_selectedControl != null)
            {
                _selectedControl.SetHighlight(false);
            }

            if (_controls.TryGetValue(area, out var control))
            {
                control.SetHighlight(true);
                _selectedControl = control;
                ViewModel.SelectedArea = area;

                ViewModel.SelectedAreaChanged(area.PalletsNo);
            }
        }

        private void RootBorder_DragOver(object sender,DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            
        }

        private void RootBorder_Drop(object sender, DragEventArgs e)
        {
            var vm = ViewModel;

            vm.CancelDrag();
        }

        private void DataInformation_LoadingRow(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridRowEventArgs e)
        { 
            if(e.Row.DataContext is not PalletDetailModel model)
                return;

            if (model.PlaceStatus == "PLACED")
            {
                e.Row.Background = new SolidColorBrush(
                    ColorHelper.FromArgb(
                        255,
                        198,
                        239,
                        206));
            }
            else
            {
                e.Row.Background = new SolidColorBrush(
                    ColorHelper.FromArgb(
                        255,
                        255,
                        252,
                        244));
            }


        }

        private void RectangleToggle_Toggled(object sender,RoutedEventArgs e)
        {
            MyRectangle.Visibility = RectangleToggle.IsOn
                ? Visibility.Collapsed
                : Visibility.Visible;
        }


    }
}
