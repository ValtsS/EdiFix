using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdiFix
{
    public static class MaidenheadLocatorUtils
    {
        public static double DistanceBetweenLocators(string loc1, string loc2)
        {
            var (lat1, lon1) = LocatorToLatLon(loc1);
            var (lat2, lon2) = LocatorToLatLon(loc2);
            return Haversine(lat1, lon1, lat2, lon2);
        }

        // Converts 4 or 6-character Maidenhead locator to center lat/lon
        public static (double lat, double lon) LocatorToLatLon(string locator)
        {
            locator = locator.ToUpperInvariant();

            if (locator.Length != 4 && locator.Length != 6)
                throw new ArgumentException("Locator must be 4 or 6 characters");

            int lonField = locator[0] - 'A';
            int latField = locator[1] - 'A';
            int lonSquare = locator[2] - '0';
            int latSquare = locator[3] - '0';

            double lon = lonField * 20.0 - 180.0 + lonSquare * 2.0 + 1.0;
            double lat = latField * 10.0 - 90.0 + latSquare * 1.0 + 0.5;

            if (locator.Length == 6)
            {
                int lonSubsquare = locator[4] - 'A';
                int latSubsquare = locator[5] - 'A';

                lon += lonSubsquare * (2.0 / 24.0) + (2.0 / 24.0) / 2.0;
                lat += latSubsquare * (1.0 / 24.0) + (1.0 / 24.0) / 2.0;
            }

            return (lat, lon);
        }

        // Haversine formula to compute distance in km
        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0; // Earth radius in km
            double dLat = DegreesToRadians(lat2 - lat1);
            double dLon = DegreesToRadians(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double DegreesToRadians(double deg) => deg * Math.PI / 180.0;

        public static bool IsValidMaidenhead6(string locator)
        {
            if (string.IsNullOrWhiteSpace(locator) || locator.Length != 6)
                return false;

            locator = locator.ToUpperInvariant();

            return
                locator[0] >= 'A' && locator[0] <= 'R' &&
                locator[1] >= 'A' && locator[1] <= 'R' &&
                locator[2] >= '0' && locator[2] <= '9' &&
                locator[3] >= '0' && locator[3] <= '9' &&
                locator[4] >= 'A' && locator[4] <= 'X' &&
                locator[5] >= 'A' && locator[5] <= 'X';
        }
    }


}
