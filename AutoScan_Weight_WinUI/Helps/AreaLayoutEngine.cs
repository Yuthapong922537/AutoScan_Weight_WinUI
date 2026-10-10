using AutoScan_Weight_WinUI.Models;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Helps
{
    public static class AreaLayoutEngine
    {
        private const double CellSize = 70;



        private const int Adjust_zoneA_Left = 0;
        private const int Adjust_zoneA_Top = 30;

        private const int Adjust_zoneB_Left = 0;
        private const int Adjust_zoneB_Top = 60;

        private const int Adjust_zoneC_Left = 0;
        private const int Adjust_zoneC_Top = 90;

        private const int Adjust_zoneD_Left = 0;
        private const int Adjust_zoneD_Top = 120;

        private const int Adjust_zoneE_Left = 0;
        private const int Adjust_zoneE_Top = 150;

        private const int Adjust_zoneF_Left = 0;
        private const int Adjust_zoneF_Top = 60;

        private const int Adjust_zoneG_Left = 0;
        private const int Adjust_zoneG_Top = 90;

        private const int Adjust_zoneH_Left = 0;
        private const int Adjust_zoneH_Top = 60;

        private const int Adjust_zoneI_Left = 0;
        private const int Adjust_zoneI_Top = 120;

        private static readonly Dictionary<char, (double Left, double Top)> ZoneOrigin = new()
        {


            {'A', (100 + Adjust_zoneA_Left, 20 + Adjust_zoneA_Top) },
            {'B', (100 + Adjust_zoneB_Left, 160 + Adjust_zoneB_Top) },
            {'C', (100 + Adjust_zoneC_Left, 300 + Adjust_zoneC_Top) },
            {'D', (100 + Adjust_zoneD_Left, 440 + Adjust_zoneD_Top) },
            {'E', (100 + Adjust_zoneE_Left, 580 + Adjust_zoneE_Top) },
            {'G', (550 + Adjust_zoneF_Left, 160 + Adjust_zoneF_Top) },
            {'F', (550 + Adjust_zoneG_Left, 300 + Adjust_zoneG_Top) },
            {'H', (850 + Adjust_zoneH_Left, 160 + Adjust_zoneH_Top) },
            {'I', (850 + Adjust_zoneI_Left, 440 + Adjust_zoneI_Top) }
        };

        public static void SetPosition(AreaModel area)
        {
            if (string.IsNullOrWhiteSpace(area.AreaName))
                return;

            char zone = char.ToUpper(area.AreaName[0]);

            int index = int.Parse(area.AreaName.Substring(1));

            if (!ZoneOrigin.TryGetValue(zone, out var origin))
            {
                return;
            }

            // -------------------------
            // Zone A-D (12 ช่อง)
            // -------------------------
            if (zone is 'A' or 'B' or 'C' or 'D' )
            {
                if (index <= 6)
                {
                    area.Left = origin.Left + (6 - index) * CellSize;
                    area.Top = origin.Top + CellSize; 
                }
                else
                {
                    area.Left = origin.Left + (index - 7) * CellSize;
                    area.Top = origin.Top;
                }
            }
            // -------------------------
            // Zone E (9 ช่อง)
            // -------------------------
            else if (zone is 'E')
                if (index <= 5)
                {
                    area.Left = origin.Left + (5 - index) * CellSize;
                    area.Top = origin.Top + CellSize;
                }
                else
                {
                    area.Left = origin.Left + (index - 6) * CellSize;
                    area.Top = origin.Top;
                }
            // -------------------------
            // Zone F-G (6 ช่อง)
            // -------------------------
            else if (zone is 'F' or 'G')
            {
                if (index <= 3)
                {
                    // F3 F2 F1
                    area.Left = origin.Left + (3 - index) * CellSize;
                    area.Top = origin.Top + CellSize;
                }
                else
                {
                    // F4 F5 F6
                    area.Left = origin.Left + (index - 4) * CellSize;
                    area.Top = origin.Top ;
                }
            }
            // -------------------------
            // Zone H-I (6 ช่อง)
            // -------------------------
            else if (zone is 'H' or 'I')
            {
                int columns = 5; // จำนวนคอลัมน์ในแต่ละแถว

                int row = (index - 1) / columns; // แถวที่อยู่ (0-based)

                int col = (index - 1) % columns; // คอลัมน์ที่อยู่ (0-based)

                col = columns - 1 - col;

                area.Left = origin.Left + col * CellSize;
                area.Top = origin.Top + row * CellSize;

            }
        }
    }

   
}
