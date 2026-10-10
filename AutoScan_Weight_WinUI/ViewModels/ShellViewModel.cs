using AutoScan_Weight_WinUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.ViewModels
{
    public partial class ShellViewModel : ObservableRecipient
    {
        [ObservableProperty]
        private bool isAdmin;

        public UserSessionService Session { get; }



        public ShellViewModel(UserSessionService session)
        {
            Session = session;

        }

        public void CheckAdmin()
        {
            if (Session.CurrentUser == null)
            {
                IsAdmin = false;
                return;
            }

            IsAdmin = Session.CurrentUser.UserGroup == "Innovation" || Session.CurrentUser.UserGroup == "Finishgoods";


            var areaViewModel = App.GetService<AreaViewModel>();

            areaViewModel?.SetUserPermission(Session.CurrentUser);

        }
    }
}
