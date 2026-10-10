using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Helps
{
    public class AreaToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return new SolidColorBrush(Microsoft.UI.Colors.Transparent);


            string Area = System.Convert.ToString(value);


            if (Area == "รอเก็บ")
            {
                return new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            else
            {
                return new SolidColorBrush(Microsoft.UI.Colors.Green);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    
    }
}

