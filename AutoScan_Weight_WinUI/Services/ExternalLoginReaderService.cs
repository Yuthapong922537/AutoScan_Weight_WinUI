using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoScan_Weight_WinUI.Services
{
    public class ExternalLoginReaderService
    {
        private readonly string _path = @"C:\YMT\yptlogin.txt";

        public string? GetCurrentUser()
        {
            if (!File.Exists(_path))
            {
                return null;
            }
            var content = File.ReadAllText(_path).Trim();

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }
            // กรณี USER=Yuthapong
            if (content.Contains("="))
            {
                var parts = content.Split('=');
                return parts.Length > 1 ? parts[1].Trim() : null;
            }

            // กรณีมีแค่ชื่อ
            return content;
        }
    }
}
