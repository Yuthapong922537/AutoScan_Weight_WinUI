using CommunityToolkit.Mvvm.ComponentModel;
using AutoScan_Weight_WinUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Services
{
    public partial class UserSessionService : ObservableObject
    {
        [ObservableProperty]
        private User? currentUser;

        public bool IsLoggedIn => CurrentUser != null;

        public string? UserName => CurrentUser?.YPTName;

        partial void OnCurrentUserChanged(User? value)
        {
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(UserName));

        }

        public void SetUser(User user)
        {
            CurrentUser = user;
        }
    }
}
