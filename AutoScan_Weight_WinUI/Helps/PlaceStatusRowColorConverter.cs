using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Helps
{
    public class PlaceStatusRowColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        { 
            string status = value?.ToString();

            if (status == "PLACED")
            { 
                return new SolidColorBrush(ColorHelper.FromArgb(255, 198, 239, 206));
            }
            // RESERVED
            return new SolidColorBrush(ColorHelper.FromArgb(255, 255, 252, 244));

        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        { 
            throw new NotImplementedException();
        }
    }
}
