// C:\Code\TcxStatsCalculator\PF 08-05-25.tcx
// C:\Code\TcxStatsCalculator\PF 08-07-25.tcx
using System.Globalization;
using System.Xml.Linq;

namespace TcxStatsCalculator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Enter path to TCX file:");
            string path = Console.ReadLine();

            if (!File.Exists(path))
            {
                Console.WriteLine("File not found.");
                return;
            }

            try
            {
                var (heartRates, cadences, powers, speeds, times, distances, altitudes) = ParseTcx(path);

                PrintStats("Heart Rate", heartRates, "bpm");
                PrintStats("Cadence", cadences, "rpm");
                PrintStats("Power", powers, "watts");

				// Calculate average speed using total distance / total time
				double avgSpeed = CalculateAverageSpeed(times, distances); // mph
				var segmentSpeeds = CalculateSegmentSpeeds(times, distances, 5.0);

				if (avgSpeed > 0 && segmentSpeeds.Count > 0)
				{
					double speedStdDev = StdDev(segmentSpeeds);
					Console.WriteLine($"Speed: Avg = {avgSpeed:F2} mph, StdDev = {speedStdDev:F2} mph (n={segmentSpeeds.Count})");
				}
				else
				{
					Console.WriteLine("Speed: No data");
				}
				// Calculate and display segment speeds, ignoring segments < 5 mph
                if (segmentSpeeds.Count > 0)
                {
                    //Console.WriteLine("\nSegment Speeds (mph, ignoring < 5 mph):");
                    //for (int i = 0; i < segmentSpeeds.Count; i++)
                    //{
                    //    Console.WriteLine($"  Segment {i + 1}: {segmentSpeeds[i]:F2} mph");
                    //}
                    double segAvg = segmentSpeeds.Average();
                    double segStd = StdDev(segmentSpeeds);
                    Console.WriteLine($"\nSegment Speed Stats: Avg = {segAvg:F2} mph, StdDev = {segStd:F2} mph, Min = {segmentSpeeds.Min():F2} mph, Max = {segmentSpeeds.Max():F2} mph");
                }
                else
                {
                    Console.WriteLine("No segment speeds >= 5 mph could be calculated.");
                }

                // Calculate and display total elevation change in feet
                double totalElevationChangeFeet = CalculateTotalElevationChangeInFeet(altitudes);
                Console.WriteLine($"\nTotal Elevation Change: {totalElevationChangeFeet:F2} feet");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static (List<double> heartRates, List<double> cadences, List<double> powers, List<double> speeds, List<DateTime> times, List<double> distances, List<double> altitudes)
            ParseTcx(string path)
        {
            var doc = XDocument.Load(path);

            XNamespace ns = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2";
            XNamespace ns_ext = "http://www.garmin.com/xmlschemas/ActivityExtension/v2";

            var trackpoints = doc.Descendants(ns + "Trackpoint");

            var heartRates = new List<double>();
            var cadences = new List<double>();
            var powers = new List<double>();
            var speeds = new List<double>();
            var times = new List<DateTime>();
            var distances = new List<double>();
            var altitudes = new List<double>();

            foreach (var tp in trackpoints)
            {
                // Heart Rate
                var hrElem = tp.Element(ns + "HeartRateBpm")?.Element(ns + "Value");
                if (hrElem != null && double.TryParse(hrElem.Value, out double hrVal))
                    heartRates.Add(hrVal);

                // Cadence
                var cadenceElem = tp.Element(ns + "Cadence");
                if (cadenceElem != null && double.TryParse(cadenceElem.Value, out double cadenceVal))
                    cadences.Add(cadenceVal);

                // Extensions
                var extElem = tp.Element(ns + "Extensions");
                if (extElem != null)
                {
                    var tpxElem = extElem.Element(ns_ext + "TPX");
                    if (tpxElem != null)
                    {
                        // Power
                        var wattsElem = tpxElem.Element(ns_ext + "Watts");
                        if (wattsElem != null && double.TryParse(wattsElem.Value, out double wattsVal))
                            powers.Add(wattsVal);

                        // Speed (in m/s, convert to mph)
                        var speedElem = tpxElem.Element(ns_ext + "Speed");
                        if (speedElem != null && double.TryParse(speedElem.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double speedVal))
                            speeds.Add(MetersPerSecondToMph(speedVal));
                    }
                }

                // Time
                var timeElem = tp.Element(ns + "Time");
                if (timeElem != null && DateTime.TryParse(timeElem.Value, null, DateTimeStyles.AdjustToUniversal, out DateTime timeVal))
                    times.Add(timeVal);

                // Distance
                var distElem = tp.Element(ns + "DistanceMeters");
                if (distElem != null && double.TryParse(distElem.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double distVal))
                    distances.Add(distVal);

                // Altitude
                var altElem = tp.Element(ns + "AltitudeMeters");
                if (altElem != null && double.TryParse(altElem.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double altVal))
                    altitudes.Add(altVal);
            }

            return (heartRates, cadences, powers, speeds, times, distances, altitudes);
        }

        static void PrintStats(string label, List<double> values, string unit)
        {
            if (values.Count == 0)
            {
                Console.WriteLine($"{label}: No data");
                return;
            }

            double avg = values.Average();
            double stddev = StdDev(values);
            Console.WriteLine($"{label}: Avg = {avg:F2} {unit}, StdDev = {stddev:F2} {unit} (n={values.Count})");
        }

        static double StdDev(List<double> values)
        {
            if (values.Count == 0) return 0;
            double avg = values.Average();
            double sumSq = values.Sum(v => (v - avg) * (v - avg));
            return Math.Sqrt(sumSq / values.Count);
        }

        static double CalculateAverageSpeed(List<DateTime> times, List<double> distances)
        {
            if (times.Count < 2 || distances.Count < 2)
                return 0.0;

            // Use first and last time/distance
            double totalTimeSeconds = (times.Last() - times.First()).TotalSeconds;
            double totalDistanceMeters = distances.Last() - distances.First();

            if (totalTimeSeconds <= 0 || totalDistanceMeters <= 0)
                return 0.0;

            double avgSpeedMs = totalDistanceMeters / totalTimeSeconds;
            double avgSpeedMph = MetersPerSecondToMph(avgSpeedMs);
            return avgSpeedMph;
        }

        static double MetersPerSecondToMph(double mps)
        {
            return mps * 2.2369362920544;
        }

        /// <summary>
        /// Calculates segment speeds in mph, ignoring segments with speed less than minSpeedMph.
        /// </summary>
        static List<double> CalculateSegmentSpeeds(List<DateTime> times, List<double> distances, double minSpeedMph = 5.0)
        {
            var segmentSpeeds = new List<double>();
            if (times.Count < 2 || distances.Count < 2)
                return segmentSpeeds;

            for (int i = 1; i < times.Count; i++)
            {
                double segmentDistance = distances[i] - distances[i - 1]; // meters
                double segmentTime = (times[i] - times[i - 1]).TotalSeconds; // seconds

                if (segmentTime > 0 && segmentDistance >= 0)
                {
                    double segmentSpeedMps = segmentDistance / segmentTime;
                    double segmentSpeedMph = MetersPerSecondToMph(segmentSpeedMps);
                    if (segmentSpeedMph >= minSpeedMph)
                    {
                        segmentSpeeds.Add(segmentSpeedMph);
                    }
                }
            }
            return segmentSpeeds;
        }

        /// <summary>
        /// Calculates total elevation change (sum of all positive and negative changes) in feet.
        /// </summary>
        static double CalculateTotalElevationChangeInFeet(List<double> altitudes)
        {
            if (altitudes == null || altitudes.Count < 2)
                return 0.0;

            double totalChangeMeters = 0.0;
            for (int i = 1; i < altitudes.Count; i++)
            {
                totalChangeMeters += Math.Abs(altitudes[i] - altitudes[i - 1]);
            }
            return totalChangeMeters * 3.28084; // convert meters to feet
        }
    }
}