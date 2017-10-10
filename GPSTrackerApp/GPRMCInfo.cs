using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GPSTrackerApp {
    class GPRMCInfo {
        public DateTime Date;
        public bool IsValid;
        public float Lat;
        public float Lng;
        public float Speed;
        public float TrackAngle;
        public float MagneticVariation;
        public string Checksum;
        
        /* Attempt to parse te NMEA message using regular expressions */
        public static GPRMCInfo Parse(string gprmc) {

            Regex regex = new Regex(@"\$GPRMC,(.*),(.*),(.*),(.*),(.*),(.*),(.*),(.*),(.*),(.*),(.*),(.*)");
            Match match = regex.Match(gprmc);
            if (match.Success) {
                if (match.Groups.Count < 11) {
                    return null;
                }
                var time = match.Groups[1].Value;
                var status = match.Groups[2].Value;
                var lat = match.Groups[3].Value;
                var latDirection = match.Groups[4].Value;
                var lng = match.Groups[5].Value;
                var lngDirection = match.Groups[6].Value;
                var speed = match.Groups[7].Value;
                var trackAngle = match.Groups[8].Value;
                var date = match.Groups[9].Value;
                var magneticVariation = match.Groups[10].Value;
                var checksum = match.Groups[11].Value;

                GPRMCInfo result = new GPRMCInfo();

                try {
                    result.Date = DateTime.ParseExact(date + time, "ddMMyyHHmmss.ff", CultureInfo.InvariantCulture);
                    result.Lat = ParseCoord(lat) * (latDirection == "N" ? 1 : -1);
                    result.Lng = ParseCoord(lng) * (lngDirection == "E" ? 1 : -1);
                    result.Speed = float.Parse(speed) * 0.514444f;
                } catch {
                    return null;
                }
                return result;
            }

            return null;
        }

        /* Convert from the degrees, minutes, and seconds format to the decimal coord */
        public static float ParseCoord(string coord) {
            float degrees, minutes;
            if (coord.Length == 10) {
                degrees = float.Parse(coord.Substring(0, 2));
                minutes = float.Parse(coord.Substring(2));
            } else {
                degrees = float.Parse(coord.Substring(0, 3));
                minutes = float.Parse(coord.Substring(3));
            }
            return degrees + minutes / 60;
        }

        /* Calculate the distance between 2 points */
        public static double Haversine(double lat1, double lng1, double lat2, double lng2) {
            const double r = 6371;

            var sdlat = Math.Sin((lat2 - lat1) / 2);
            var sdlon = Math.Sin((lng2 - lng1) / 2);
            var q = sdlat * sdlat + Math.Cos(lat1) * Math.Cos(lat2) * sdlon * sdlon;
            var d = 2 * r * Math.Asin(Math.Sqrt(q));

            return d;
        }
    }
}
