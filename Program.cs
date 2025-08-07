using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
				var (heartRates, cadences, powers, speeds) = ParseTcx(path);

				PrintStats("Heart Rate", heartRates, "bpm");
				PrintStats("Cadence", cadences, "rpm");
				PrintStats("Power", powers, "watts");
				PrintStats("Speed", speeds, "km/h");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error: {ex.Message}");
			}
		}

		static (List<double> heartRates, List<double> cadences, List<double> powers, List<double> speeds) ParseTcx(string path)
		{
			var doc = XDocument.Load(path);

			XNamespace ns = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2";
			XNamespace ns_ext = "http://www.garmin.com/xmlschemas/ActivityExtension/v2";

			var trackpoints = doc.Descendants(ns + "Trackpoint");

			var heartRates = new List<double>();
			var cadences = new List<double>();
			var powers = new List<double>();
			var speeds = new List<double>();

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

						// Speed (in m/s, convert to km/h)
						var speedElem = tpxElem.Element(ns_ext + "Speed");
						if (speedElem != null && double.TryParse(speedElem.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double speedVal))
							speeds.Add(speedVal * 3.6);
					}
				}
			}

			return (heartRates, cadences, powers, speeds);
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
	}
}