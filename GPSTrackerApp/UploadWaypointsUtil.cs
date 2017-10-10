using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Windows.Storage.Streams;
using Windows.System.Profile;

namespace GPSTrackerApp {
    class UploadWaypointsUtil {
        public static async void UploadWaypointData(FileStream fileStream) {
            fileStream.Position = 0;

            var hardwareId = GetHardwareId();

            using (var streamReader = new StreamReader(fileStream)) {
                List<Waypoint> waypoints = new List<Waypoint>();
                
                /* Construct a list of waypoints from the csv file */
                string line;
                while ((line = streamReader.ReadLine()) != null) {
                    Regex regex = new Regex(@"(\d.*),(.*),(.*),(.*)");
                    Match match = regex.Match(line);
                    if (match.Success) {
                        var datetime = DateTime.ParseExact(match.Groups[1].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                        var lat = float.Parse(match.Groups[2].Value);
                        var lng = float.Parse(match.Groups[3].Value);
                        var speed = float.Parse(match.Groups[4].Value);

                        var waypoint = new Waypoint { DateTime = datetime, Lat = lat, Lng = lng, Speed = speed };
                        waypoints.Add(waypoint);
                    }
                }

                /* If there are any valid waypoints then create the trip serialise as JSON and post to Azure */
                if (waypoints.Count > 0) {
                    Trip trip = new Trip { TripId = $"{hardwareId}-{waypoints[0].DateTime.ToString("yyyy-MM-dd HH:mm:ss")}", UserId = hardwareId, StartDateTime = waypoints[0].DateTime, Waypoints = waypoints };

                    var jsonSettings = new JsonSerializerSettings {
                        DateFormatString = "yyyy-MM-dd HH:mm:ss"
                    };

                    var json = JsonConvert.SerializeObject(trip, jsonSettings).ToCharArray();

                    WebRequest request = WebRequest.Create("https://trips.azurewebsites.net/api/AddTrip");
                    request.ContentType = "application/json";
                    request.Method = "POST";

                    try {
                        using (var streamWriter = new StreamWriter(await request.GetRequestStreamAsync())) {
                            streamWriter.Write(json, 0, json.Length);
                            streamWriter.Flush();

                            using (HttpWebResponse response = await request.GetResponseAsync() as HttpWebResponse) {
                                using (StreamReader reader = new StreamReader(response.GetResponseStream())) {
                                    string content = reader.ReadToEnd();
                                }
                            }
                        }
                    } catch (Exception e) {
                        Console.Write(e);
                    }
                }
            }


        }

        private static string GetHardwareId() {
            var token = HardwareIdentification.GetPackageSpecificToken(null);
            var hardwareId = token.Id;
            var dataReader = DataReader.FromBuffer(hardwareId);

            byte[] bytes = new byte[hardwareId.Length];
            dataReader.ReadBytes(bytes);
            
            return Convert.ToBase64String(bytes);
        }
        class Waypoint {
            [JsonProperty("datetime")]
            public DateTime DateTime;
            [JsonProperty("lat")]
            public float Lat;
            [JsonProperty("lng")]
            public float Lng;
            [JsonProperty("speed")]
            public float Speed;
        }

        class Trip {
            [JsonProperty("trip_id")]
            public string TripId;
            [JsonProperty("user_id")]
            public string UserId;
            [JsonProperty("start_datetime")]
            public DateTime StartDateTime;
            [JsonProperty("waypoints")]
            public List<Waypoint> Waypoints = new List<Waypoint>();
        }
    }
}
