using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdiFix.Models;
using ylac_judge.import;
using ylac_judge.contract;

namespace EdiFix.Services
{
    internal class EdiProcessor
    {
        private readonly EdiLoader _loader = new();

        public ConcurrentDictionary<string, EdiData> LoadEdiFiles(IEnumerable<string> filePaths)
        {
            var data = new ConcurrentDictionary<string, EdiData>();
            Parallel.ForEach(filePaths, fileName => data[fileName] = new EdiData
            {
                filename = fileName,
                lines = File.ReadAllLines(fileName),
                log = _loader.LoadFromFile(fileName)
            });
            return data;
        }

        public void CheckBands(IEnumerable<EdiData> data)
        {
            var bands = data.Select(v => v.log.Band)
                           .Where(v => v.HasValue)
                           .Select(v => v.Value)
                           .GroupBy(w => w)
                           .ToDictionary(g => g.Key, g => g.Count());

            if (bands.Count > 1)
            {
                foreach (var kvp in bands)
                {
                    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                }
                throw new Exception("Band data is mixed/wrong");
            }
        }

        public List<Patch> GenerateSignalPatches(QSORecord[] allQso, int thresholdMinutes)
        {
            var patches = new List<Patch>();
            var q = new LinkedList<QSORecord>();
            var counter = new Dictionary<string, HashSet<LinkedListNode<QSORecord>>>();

            foreach (var newQso in allQso)
            {
                var added = q.AddLast(newQso);
                var addedId = added.Value.GetID();
                var reverse = added.Value.GetIDReverse();

                if (!counter.ContainsKey(addedId))
                    counter[addedId] = new();

                counter[addedId].Add(added);

                while (q.Count > 0 && (q.Last.Value.Time - q.First.Value.Time) > TimeSpan.FromMinutes(thresholdMinutes))
                {
                    var first = q.First;
                    counter[first.Value.GetID()].Remove(first);
                    q.RemoveFirst();
                }

                if (counter.TryGetValue(reverse, out var nodes) && nodes.Count > 0)
                {
                    foreach (var qso in nodes)
                    {
                        if (qso.Value.OpCallsign != added.Value.Callsign)
                            throw new Exception("logic broken, should not happen");

                        if (qso.Value.SentRST != added.Value.ReceivedRST)
                        {
                            patches.Add(new Patch
                            {
                                callsign = added.Value.Callsign,
                                line = added.Value.LineNumber,
                                recRST = qso.Value.SentRST,
                                CallsignReportToPatch = added.Value.OpCallsign
                            });
                        }

                        if (qso.Value.ReceivedRST != added.Value.SentRST)
                        {
                            patches.Add(new Patch
                            {
                                callsign = qso.Value.Callsign,
                                line = qso.Value.LineNumber,
                                recRST = added.Value.SentRST,
                                CallsignReportToPatch = qso.Value.OpCallsign
                            });
                        }
                    }
                }
            }
            return patches;
        }

        public void ApplyPatches(IEnumerable<Patch> patches, IDictionary<string, EdiData> data, IDictionary<string, string> callsignToFile)
        {
            var toSave = new HashSet<string>();

            foreach (var p in patches)
            {
                if (!callsignToFile.TryGetValue(p.CallsignReportToPatch, out var fileName))
                    continue;

                var entry = data[fileName];
                var splitted = entry.lines[p.line].Split(';');

                if (splitted[2] != p.callsign)
                    throw new Exception($"Did not find {p.CallsignReportToPatch} in {fileName}");

                if (!string.IsNullOrEmpty(p.recRST))
                    splitted[6] = p.recRST;

                if (!string.IsNullOrEmpty(p.WWL))
                    splitted[9] = p.WWL;

                var newStuff = string.Join(";", splitted);
                Console.WriteLine($"{fileName} : \n\t{entry.lines[p.line]}\n\t{newStuff}\n");
                if (!string.IsNullOrEmpty(p.Comment))
                    Console.WriteLine(p.Comment);

                entry.lines[p.line] = newStuff;
                toSave.Add(fileName);
            }

            if (toSave.Count > 0)
            {
                Console.WriteLine("Apply fixes? (Y)");
                var confirm = Console.ReadLine();

                if (confirm?.ToUpper() == "Y")
                {
                    foreach (var fileName in toSave)
                    {
                        Console.WriteLine($"Saving...{fileName}");
                        File.WriteAllLines(fileName, data[fileName].lines);
                    }
                }
                else
                {
                    Console.WriteLine("Aborted");
                }
            }
            else
            {
                Console.WriteLine("No changes required");
            }
        }
    }
}
