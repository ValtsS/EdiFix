using System;
using System.Collections.Generic;
using System.Linq;
using EdiFix.Models;
using ylac_judge.contract;

namespace EdiFix.Services
{
    internal class WwlResolver
    {
        private readonly Call3 _call3;
        private readonly Dictionary<string, WwlTracker> _trackers = new();

        public WwlResolver(Call3 call3)
        {
            _call3 = call3;
        }

        private WwlTracker GetTracker(string callsign)
        {
            if (_trackers.TryGetValue(callsign, out WwlTracker found))
                return found;

            found = new WwlTracker
            {
                callsign = callsign,
                External = _call3.GetGridSquare(callsign)
            };
            _trackers[callsign] = found;
            return found;
        }

        public Dictionary<string, WwlTruth> ResolveTruths(QSORecord[] allQso)
        {
            foreach (var qso in allQso)
            {
                var opTracker = GetTracker(qso.OpCallsign);
                opTracker.SelfReported = qso.OpWWL;
                opTracker.relatedCallsigns.Add(qso.OpCallsign);
                opTracker.OtherReport(qso.OpWWL, qso.OpCallsign);

                var targetTracker = GetTracker(qso.Callsign);
                targetTracker.OtherReport(qso.ReceivedWWL, qso.OpCallsign);
            }

            var truths = new Dictionary<string, WwlTruth>();
            var needHelp = false;

            foreach (var kv in _trackers)
            {
                var candidate = kv.Value.Truth(out string comment);

                if (candidate == null || (comment != null && comment.Contains("CALL3")))
                {
                    kv.Value.Log();
                    candidate = SelectSquare(kv.Value.H, kv.Value.External);
                }

                if (!string.IsNullOrEmpty(candidate))
                {
                    truths[kv.Key] = new WwlTruth
                    {
                        WWL = candidate,
                        Comment = comment
                    };
                }
                else
                {
                    needHelp = true;
                }
            }

            if (needHelp)
                throw new Exception("Could not determine truth...");

            return truths;
        }

        public List<Patch> GenerateWwlPatches(QSORecord[] allQso, Dictionary<string, WwlTruth> truths)
        {
            var patches = new List<Patch>();
            foreach (var qso in allQso)
            {
                if (truths.TryGetValue(qso.Callsign, out WwlTruth truth) && truth.WWL != qso.ReceivedWWL)
                {
                    patches.Add(new Patch
                    {
                        callsign = qso.Callsign,
                        line = qso.LineNumber,
                        CallsignReportToPatch = qso.OpCallsign,
                        WWL = truth.WWL,
                        Comment = truth.Comment,
                    });
                }
            }
            return patches;
        }

        private string SelectSquare(Dictionary<string, HashSet<string>> options, string? external)
        {
            var keys = options.Keys.ToArray();
            Console.WriteLine("\nSelect the grid or enter correct one: ");
            if (!string.IsNullOrEmpty(external))
            {
                Console.WriteLine($"[External (call3.txt)]: {external}");
            }

            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                var prfx = _call3.GetPrefixes6(key);
                string distanceInfo = "";
                if (!string.IsNullOrEmpty(external) && MaidenheadLocatorUtils.IsValidMaidenhead6(key) && MaidenheadLocatorUtils.IsValidMaidenhead6(external))
                {
                    var dist = MaidenheadLocatorUtils.DistanceBetweenLocators(key, external);
                    distanceInfo = $" - {dist:F1} km from external";
                }
                var reporters = string.Join(", ", options[key]);
                Console.WriteLine($"[{i + 1}] {key} ({string.Join(" ", prfx)}...){distanceInfo} reported by: {reporters}");
            }

            while (true)
            {
                Console.Write("\n?=");
                var line = Console.ReadLine()?.Trim();
                if (int.TryParse(line, out int idx) && idx >= 1 && idx <= keys.Length)
                {
                    return keys[idx - 1];
                }

                if (!string.IsNullOrEmpty(line) && MaidenheadLocatorUtils.IsValidMaidenhead6(line))
                {
                    return line;
                }
                
                Console.WriteLine("\nDid not seem right...");
            }
        }
    }
}
