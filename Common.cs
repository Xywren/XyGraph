using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;

namespace XyGraph
{
    internal class Common
    {
        // Return a hex colour string derived from the input (e.g. "#RRGGBB")
        public static string HashColour(string input)
        {
            if (input == null) input = string.Empty;

            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes;
            using (SHA256 sha = SHA256.Create())
            {
                hashBytes = sha.ComputeHash(inputBytes);
            }

            // Derive H, S, L from hash bytes
            int hue = ((hashBytes[0] << 8) | hashBytes[1]) % 360; // 0-359
            double saturation = 0.5 + (hashBytes[2] / 255.0) * 0.4; // 0.5 - 0.9
            double lightness = 0.4 + (hashBytes[3] / 255.0) * 0.3; // 0.4 - 0.7

            Color c = HslToRgb(hue, saturation, lightness);
            // Return as #RRGGBB
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        // Convert HSL (h in degrees 0-360, s,l in 0..1) to Color
        private static Color HslToRgb(double h, double s, double l)
        {
            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double hh = h / 60.0;
            double x = c * (1.0 - Math.Abs(hh % 2.0 - 1.0));
            double r1 = 0.0;
            double g1 = 0.0;
            double b1 = 0.0;

            if (0 <= hh && hh < 1)
            {
                r1 = c; g1 = x; b1 = 0;
            }
            else if (1 <= hh && hh < 2)
            {
                r1 = x; g1 = c; b1 = 0;
            }
            else if (2 <= hh && hh < 3)
            {
                r1 = 0; g1 = c; b1 = x;
            }
            else if (3 <= hh && hh < 4)
            {
                r1 = 0; g1 = x; b1 = c;
            }
            else if (4 <= hh && hh < 5)
            {
                r1 = x; g1 = 0; b1 = c;
            }
            else if (5 <= hh && hh < 6)
            {
                r1 = c; g1 = 0; b1 = x;
            }

            double m = l - c / 2.0;
            byte r = (byte)Math.Round((r1 + m) * 255.0);
            byte g = (byte)Math.Round((g1 + m) * 255.0);
            byte b = (byte)Math.Round((b1 + m) * 255.0);

            return Color.FromRgb(r, g, b);
        }
    }
}
