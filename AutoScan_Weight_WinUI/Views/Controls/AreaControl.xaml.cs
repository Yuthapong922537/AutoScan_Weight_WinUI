
using AutoScan_Weight_WinUI.Helps;
using AutoScan_Weight_WinUI.Models;
using AutoScan_Weight_WinUI.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AutoScan_Weight_WinUI.Views.Controls
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AreaControl : UserControl
    {
        public event EventHandler<AreaModel>? DragStarted;

        public event EventHandler<AreaModel>? Dropped;

        public event EventHandler<AreaModel>? DragOverArea;

        public event EventHandler? DragEnded;

        private Brush? _currentBodyBrush;
        private bool _isFlashing;
        private bool _isBodyFlashing;

        public event EventHandler<AreaModel>? Clicked;

        private AreaModel? _boundArea;

      

        public AreaControl()
        {
            InitializeComponent();

          Loaded += AreaControl_Loaded;
          DataContextChanged += AreaControl_DataContextChanged;
        }

        private void RootBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (DataContext is AreaModel area)
            {
                area.IsHovered = true;
            }
        }

        private void RootBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (DataContext is AreaModel area)
            {
                area.IsHovered = false;
            }
        }

        private void UpdateAreaColor()
        {
            if (DataContext is not AreaModel area)
                return;

          

            switch (area.PalletStatus)
            {
                case "OPEN":
                    _currentBodyBrush = new SolidColorBrush(Colors.Yellow);
                    BodyBorder.Background = _currentBodyBrush;
                    break;

                case "FULL":
                    _currentBodyBrush = new SolidColorBrush(Colors.IndianRed);
                    BodyBorder.Background = _currentBodyBrush;
                    break;

                case "":

                    if (area.Status == "Reserved")
                    {
                        _currentBodyBrush = new SolidColorBrush(Colors.Orange);
                    }
                    else
                    {
                        _currentBodyBrush = new SolidColorBrush(Colors.Transparent);
                    }
                    BodyBorder.Background = _currentBodyBrush;
                    break;

                case "CLOSED":
                    _currentBodyBrush = new SolidColorBrush(Colors.LightGreen);
                    BodyBorder.Background = _currentBodyBrush;
                    break;
            }
         

        }

        private void AreaControl_Loaded(object sender, RoutedEventArgs e)
        {

            UpdateVisualState();
        }

        private void RootBorder_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            if (DataContext is not AreaModel area)
                return;

            if (area == null || !area.CanDrag)
            {
                args.Cancel = true;
                return;
            }

            args.Data.SetText(area.AreaName);

            var vm = App.GetService<AreaViewModel>();

            vm.IsDragging = true;

            DragStarted?.Invoke(this, area);

        }

        private void RootBorder_DragOver(object sender, DragEventArgs e)
        {
           

            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

            if (DataContext is AreaModel area)
            {
                if (area == null || !area.CanDrag)
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                    return;
                }

                DragOverArea?.Invoke(this, area);
            }
        }

        private async void RootBorder_Drop(object sender, DragEventArgs e)
        {
            //var sourceAreaCode = await e.DataView.GetTextAsync();

            //if (DataContext is not AreaModel targetArea)
            //    return;
            var vm = App.GetService<AreaViewModel>();

            if (DataContext is AreaModel area)
            {
                if (area == null || !area.CanDrag)
                    return;

                Dropped?.Invoke(this, area);
            }

            vm.EndDrag();

            //System.Diagnostics.Debug.WriteLine($"ลากจาก {sourceAreaCode} ไป {targetArea.AreaName}");
        }

        private void RootBorder_DragLeave(object sender, DragEventArgs e)
        {
            //var vm = App.GetService<AreaViewModel>();
            //vm.IsDragging = false;
        }

        public void SetHighlight(bool highlight)
        {
            if (DataContext is AreaModel area)
            {
                area.IsSelected = highlight;
            }
        }

        public void SetDropTarget(bool enable)
        {
            if (DataContext is AreaModel area)
            {
                area.IsDropTarget = enable;
            }

        }
        public void RefreshState()
        {
            UpdateVisualState();
        }

        public async Task FlashAsync()
        {
            if (_isFlashing)
                return;

            _isFlashing = true;

            try
            {
                RootBorder.BorderBrush =
                    new SolidColorBrush(Colors.Gold);

                RootBorder.BorderThickness =
                    new Thickness(4);

                await Task.Delay(250);
            }
            finally
            {
                _isFlashing = false;

                UpdateVisualState();
            }
        }

        public async Task FlashBodyAsync()
        {
            if (_isBodyFlashing)
                return;

            _isBodyFlashing = true;

            try
            {
                BodyBorder.Background =
                    new SolidColorBrush(Colors.Gold);

                await Task.Delay(300);
            }
            finally
            {
                _isBodyFlashing = false;

                UpdateVisualState();
            }
        }

        private void RootBorder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (DataContext is AreaModel area)
            {
                Clicked?.Invoke(this, area);
            }
        }

        public void Setselected(bool selected)
        {
            if (DataContext is AreaModel area)
            {
                area.IsSelected = selected;
            }

        }

        public void UpdateSelected()
        {
            UpdateVisualState();
        }

        private void AreaControl_DataContextChanged(FrameworkElement sender ,DataContextChangedEventArgs args)
        {
            //if (args.NewValue is AreaModel area)
            //{
            //    area.PropertyChanged += Area_PropertyChanged;

            //    UpdateSelected();
            //    UpdateAreaColor();
            //}

            if (_boundArea != null)
            {
                _boundArea.PropertyChanged -= Area_PropertyChanged;
            }

            _boundArea = DataContext as AreaModel;

                if (_boundArea != null)
             {
                 _boundArea.PropertyChanged += Area_PropertyChanged;
                UpdateVisualState();
            }
        }

        private void Area_PropertyChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
        {
            //if (e.PropertyName == nameof(AreaModel.IsSelected))
            //{
            //    DispatcherQueue.TryEnqueue(UpdateSelected);
            //}

            //if (e.PropertyName == nameof(AreaModel.PalletStatus))
            //{
            //    DispatcherQueue.TryEnqueue(UpdateAreaColor);
            //}
            switch (e.PropertyName)
            {
                case nameof(AreaModel.IsSelected):
                case nameof(AreaModel.IsDropTarget):
                case nameof(AreaModel.IsHovered):
                case nameof(AreaModel.PalletStatus):

                    UpdateVisualState();
                    break;
            }
        }

        private void UpdateVisualState() 
        {
            if (DataContext is not AreaModel area)
                return;

            // Flash Border กำลังทำงาน
            if (_isFlashing)
                return;

            // =========================
            // Border State
            // =========================

            if (area.IsSelected)
            {
                // Search area

                RootBorder.BorderBrush = new SolidColorBrush(Colors.Yellow);

                RootBorder.BorderThickness = new Thickness(4);

            }

            else if (area.IsDropTarget)
            {
                // กำลังลากมาวางที่ Area นี้
                RootBorder.BorderBrush = (Brush)Application.Current.Resources["AreaDropBrush"];

                RootBorder.BorderThickness = new Thickness(3);

            }

            else if (area.IsHovered)
            {
                // Mouse อยู่บน Area
                RootBorder.BorderBrush = new SolidColorBrush(Colors.DeepSkyBlue);

                RootBorder.BorderThickness = new Thickness(2);

            }
            else
            {
                // Normal
                RootBorder.BorderBrush = (Brush)Application.Current.Resources["AreaBorderBrush"];

                RootBorder.BorderThickness = new Thickness(1);
            
            }

            // =========================
            // Body State
            // =========================

            if (!_isBodyFlashing)
            {
                UpdateAreaColor();
            }

        }

        protected override void OnDisconnectVisualChildren()
        {
            if (_boundArea != null)
            {
                _boundArea.PropertyChanged -= Area_PropertyChanged;
                _boundArea = null;
            }

            base.OnDisconnectVisualChildren();
        }


    }
}
