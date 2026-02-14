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
        public Dictionary<string, int> H = new();
        public HashSet<string> relatedCallsigns = new();

        public void OtherReport(string wwl, string callsign)
        {
            if (!H.TryAdd(wwl, 1))
                H[wwl]++;

            relatedCallsigns.Add(callsign);
        }

        public void Log()
        {
            Console.WriteLine($"Callsign: {callsign}  ActualWWL: {SelfReported} \t  External: {External}");
            Console.WriteLine($"\t{string.Join(",", H.Select(kv => $"{kv.Key} ({kv.Value})"))}");
        }

        public string Truth(out string comment)
        {
            if (!string.IsNullOrEmpty(SelfReported))
            {
                comment = "99% self reported";
                return SelfReported;
            }

            var sorted = H.Where(x => x.Key.Length == 6).OrderByDescending(x => x.Value).ToArray();

            if (sorted.Length > 0)
            {
                var firstCount = sorted[0].Value;
                var truths = sorted.Where(x => x.Value == firstCount).Select(x => x.Key).ToHashSet();

                if (truths.Count == 1 && truths.First().Length == 6)
                {
                    comment = "";
                    foreach (var c in sorted)
                    {
                        comment += $"\t{c.Key} was reported {c.Value} times\n";
                    }

                    if (!string.IsNullOrEmpty(External) && truths.First() != External &&
                        (External.Length > 4 || External.Substring(0, 4) != truths.First().Substring(0, 4)))
                    {
                        var distance = MaidenheadLocatorUtils.DistanceBetweenLocators(truths.First(), External);
                        if (distance > 30 && sorted[0].Value == 1)
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
