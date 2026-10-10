using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Helps
{
    public  class WeightToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return new SolidColorBrush(Microsoft.UI.Colors.Transparent);


            double weight = System.Convert.ToDouble(value);


            if (weight == 0)
            {
                return new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            else if (weight >= 1) 
            {
                return new SolidColorBrush(Microsoft.UI.Colors.Green);
            }
            else
            {
                return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
