using AutoScan_Weight_WinUI.Helps;
using AutoScan_Weight_WinUI.Models;
using AutoScan_Weight_WinUI.Services;
using AutoScan_Weight_WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AutoScan_Weight_WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        /// 
        public static IDialogSevice DialogService { get; private set; }
        public static IServiceProvider Services { get; private set; }
        public App()
        {
            InitializeComponent();
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Windown
            services.AddSingleton<MainWindow>();

            // ViewModels
            services.AddSingleton<ScanBarcodeViewModel>();
            services.AddSingleton<SelectOrderViewModel>();
            services.AddSingleton<DeleteViewModel>();

            // ใช้ AreaViewModel Instance เดียวกันทั้งแอป
            services.AddSingleton<AreaViewModel>();

            services.AddSingleton<ShellViewModel>();

            // Services
            services.AddSingleton<QueryAll>();
            services.AddSingleton<ConnectSQL>();
            services.AddSingleton<ISerialPortService, SerialPortService>();
            services.AddSingleton<PalletOptimizerService>();
            services.AddSingleton<UpdateInvoiceService>();

            // User Services
            services.AddSingleton<ExternalLoginReaderService>();
            services.AddSingleton<UserService>();
            services.AddSingleton<UserSessionService>();

            //Dialog Service

            services.AddSingleton<IDialogSevice>(sp =>
            {
                var window = sp.GetRequiredService<MainWindow>();
                return new MessageService(window);
            });

            return services.BuildServiceProvider();
        }
        public static T GetService<T>() where T : class
        {
            return Services.GetService<T>();
        }
        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = Services.GetRequiredService<MainWindow>();
            

            DialogService = new MessageService(_window);

            var reader = Services.GetRequiredService<ExternalLoginReaderService>();
            var userService = Services.GetRequiredService<UserService>();
            var session = Services.GetRequiredService<UserSessionService>();

            var username = reader.GetCurrentUser();

            if (!string.IsNullOrEmpty(username))
            {
                var user = userService.GetUser(username);

                if (user != null)
                {
                    session.SetUser(user);

                    //Check Admin Status
                    var ShellVM = Services.GetRequiredService<ShellViewModel>();
                    ShellVM.CheckAdmin();
                }
            }

            _window.Activate();

        }
    }
}
