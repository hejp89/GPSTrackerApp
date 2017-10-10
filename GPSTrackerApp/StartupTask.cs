using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.ApplicationModel.Background;
using Windows.Devices.SerialCommunication;
using Windows.Devices.Enumeration;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using System.Diagnostics;
using System.IO;
using Windows.Storage;

namespace GPSTrackerApp {
    public sealed class StartupTask : IBackgroundTask {

        private FileStream WaypointDataFile;
        private bool InTrip = false;
        private DateTime TimeOfLastValidWaypoint = DateTime.MinValue;
        private List<GPRMCInfo> Waypoints = new List<GPRMCInfo>(20);
        
        public async void Run(IBackgroundTaskInstance taskInstance) {
            BackgroundTaskDeferral deferral = taskInstance.GetDeferral();
            
            /* Look for the serial and create a data reader using it's input stream */
            string deviceSelector = SerialDevice.GetDeviceSelector("UART0");
            var devices = await DeviceInformation.FindAllAsync(deviceSelector);

            SerialDevice serialPort = await SerialDevice.FromIdAsync(devices[0].Id);
            var DataReader = new DataReader(serialPort.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

            bool quit = false;

            LineQueue lineQueue = new LineQueue();

            while (!quit) {
                /* Read the GPS data from the serial device (the GPS model continously sends NMEA hence only reading is required) */
                string gpsData = await ReadAsync(DataReader);
                lineQueue.AddChunk(gpsData);

                while (lineQueue.HasMore()) {
                    string nmeaSentence = lineQueue.Next();
                    Debug.Write(nmeaSentence);

                    /* Attempt to parse the NMEA sentence if valid add to waypoint list */
                    if (nmeaSentence.StartsWith("$GPRMC")) {
                        GPRMCInfo info = GPRMCInfo.Parse(nmeaSentence);
                        if (info != null) {
                            TimeOfLastValidWaypoint = DateTime.Now;
                            Waypoints.Add(info);
                        }
                    }

                    /* If the waypoint list is full then check whether there has been any movement and start/end trip as appropriate */
                    if (Waypoints.Count == Waypoints.Capacity) {
                        if (!HaveWaypointsMoved()) {
                            EndTrip();
                        } else {
                            if (!InTrip) {
                                StartTrip();
                            }
                            WriteWaypointsToFile();
                        }
                        Waypoints.Clear();
                    }

                    /* If there haven't been any valid GPRMC messages for 2min end the trip */
                    if ((TimeOfLastValidWaypoint - DateTime.Now).Minutes >= 2) {
                        EndTrip();
                    }
                }
            }

            deferral.Complete();
        }

        private void StartTrip() {
            WaypointDataFile = new FileStream(ApplicationData.Current.LocalFolder.Path + $"/trip{DateTime.Now.ToString("yyyy-MM-dd HHmmss")}.csv", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            InTrip = true;
        }
        
        private void EndTrip() {
            if (InTrip) {
                WriteWaypointsToFile();
                Waypoints.Clear();

                /* Upload the trip to Cosmos DB */
                UploadWaypointsUtil.UploadWaypointData(WaypointDataFile);
                WaypointDataFile.Dispose();
                WaypointDataFile = null;

                InTrip = false;
            }
        }

        private void WriteWaypointsToFile() {
            foreach (var waypoint in Waypoints) {
                var bytes = Encoding.ASCII.GetBytes($"{waypoint.Date.ToString("yyyy-MM-dd HH:mm:ss")}, {waypoint.Lat}, {waypoint.Lng}, {waypoint.Speed}\n");
                WaypointDataFile.Write(bytes, 0, bytes.Length);
            }
            WaypointDataFile.Flush(true);
        }

        /* If all waypoints are with a 20m radius of their centre consider there to be no movement */
        private bool HaveWaypointsMoved() {
            float averageLat = Waypoints.Average(info => info.Lat);
            float averageLng = Waypoints.Average(info => info.Lng);
            
            foreach (var waypoint in Waypoints) {
                if (GPRMCInfo.Haversine(averageLat, averageLng, waypoint.Lat, waypoint.Lng) > 20) {
                    return true;
                }
            }

            return false;
        }

        private async Task<string> ReadAsync(DataReader dataReader, uint bufferSize = 1024) {
            UInt32 bytesRead = await dataReader.LoadAsync(bufferSize);
            return dataReader.ReadString(bytesRead);
        }
    }
}
