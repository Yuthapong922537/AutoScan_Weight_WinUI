using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Helps
{
    public interface IDialogSevice
    {
        Task ShowMessageAsync(string title, string message);

        Task<bool> ShowConfirmAsync(string message);
    }
}
