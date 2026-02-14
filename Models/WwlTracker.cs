using System;
using System.Collections.Generic;
using System.Linq;

namespace EdiFix.Models
{
    internal class WwlTracker
    {
        public string callsign;
        public string SelfReported;
        public string External;
        public Dictionary<string, HashSet<string>> H = new();
        public HashSet<string> relatedCallsigns = new();

        public void OtherReport(string wwl, string reporterCallsign)
        {
            if (string.IsNullOrEmpty(wwl)) return;

            if (!H.TryGetValue(wwl, out var reporters))
            {
                reporters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                H[wwl] = reporters;
            }
            reporters.Add(reporterCallsign);

            relatedCallsigns.Add(reporterCallsign);
        }

        public void Log()
        {
            Console.WriteLine($"Callsign: {callsign}  ActualWWL: {SelfReported} \t  External: {External}");
            Console.WriteLine($"\t{string.Join(",", H.Select(kv => $"{kv.Key} ({kv.Value.Count})"))}");
        }

        public string? Truth(out string? comment)
        {
            if (!string.IsNullOrEmpty(SelfReported))
            {
                comment = "99% self reported";
                return SelfReported;
            }

            var sorted = H.Where(x => x.Key.Length == 6).OrderByDescending(x => x.Value.Count).ToArray();

            if (sorted.Length > 0)
            {
                var firstCount = sorted[0].Value.Count;
                var truths = sorted.Where(x => x.Value.Count == firstCount).Select(x => x.Key).ToHashSet();

                if (truths.Count == 1 && truths.First().Length == 6)
                {
                    comment = "";
                    foreach (var c in sorted)
                    {
                        comment += $"\t{c.Key} was reported {c.Value.Count} times\n";
                    }

                    if (!string.IsNullOrEmpty(External) && truths.First() != External &&
                        (External.Length > 4 || External.Substring(0, 4) != truths.First().Substring(0, 4)))
                    {
                        var distance = MaidenheadLocatorUtils.DistanceBetweenLocators(truths.First(), External);
                        if (distance > 30 && sorted[0].Value.Count == 1)
                        {
                            comment = null;
                            return null;
                        }
                    }

                    return truths.First();
                }

                if (truths.Contains(External))
                {
                    comment = "CALL3 data, re-check";
                    return External;
                }
            }

            comment = null;
            return null;
        }
    }
}
