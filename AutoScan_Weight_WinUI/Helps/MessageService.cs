using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Helps
{
    internal class MessageService : IDialogSevice
    {
        private readonly Window _window;

        private readonly XamlRoot _xamlRoot;

        public MessageService(Window window)
        {
            _window = window;

        }
        public async Task ShowMessageAsync(string title, string message)
        {
            if (_window.Content is not FrameworkElement root)
                throw new NotImplementedException("Window content not ready.");

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = root.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private XamlRoot GetXamlRoot()
        {
            if (_window.Content is not FrameworkElement root)
                throw new InvalidOperationException("Window content not ready.");

            return root.XamlRoot;
        }

        public async Task<bool> ShowConfirmAsync(string message)
        {

            var dialog = new ContentDialog()
            {
                Title = "Confirm",
                Content = message,
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                XamlRoot = _xamlRoot



            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;



        }
    }
}
