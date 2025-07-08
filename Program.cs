using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ylac_judge.contract;
using ylac_judge.import;

namespace EdiFix
{
    internal class Program
    {
        private const int thresholdMinutes = 5;

        static string pwd()
        {
            return Directory.GetCurrentDirectory();
        }

        class Data
        {
            public string filename;
            public string[] lines;
            public SubmittedLog log;
        }


        class PatchMulti
        {
            public string callsign;
            public int line;

            public string CallsignReportToPatch;

            public string? recRST;
            public string? WWL;
            public string? Comment;
        }

        class TrueWWL
        {
            public string WWL;
            public string Comment;
        }


        class WWLx
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
                        foreach(var c in sorted)
                        {
                            comment += $"\t{c.Key} was reported {c.Value} times\n";
                        }

                        if (!string.IsNullOrEmpty(External) && truths.First() != External &&
                            (External.Length >4 || External.Substring(0, 4) != truths.First().Substring(0, 4)))
                        {
                            var distance = MaidenheadLocatorUtils.DistanceBetweenLocators(truths.First(), External);
                            if (distance > 30 && sorted[0].Value == 1 )
                                return null;
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

        static Dictionary<string, WWLx> wwls = new();
        static Call3 call3;

        static WWLx getForWWL(string callsign)
        {
            if (wwls.TryGetValue(callsign, out WWLx found))
                return found;

            found = new WWLx();
            wwls[callsign] = found;
            wwls[callsign].callsign = callsign;
            wwls[callsign].External = call3.GetGridSquare(callsign);
            return found;
        }


        static void Main(string[] args)
        {

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = Path.Combine(baseDir, "call3.txt");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Call3.txt File not found.", filePath);
            }

            call3 = new Call3();
            call3.Load(filePath);

            IEnumerable<String> matchingFilePaths2 = System.IO.Directory.EnumerateFiles(pwd(), "*.edi", System.IO.SearchOption.TopDirectoryOnly);


            var badFileNames = matchingFilePaths2.Select(f => Path.GetFileName(f)).Where(f => !f.All(c => c <= 127)).ToArray();

            if (badFileNames.Length > 0)
            {
                Console.WriteLine(string.Join("\n", badFileNames));
                throw new Exception("Detected filename with non ascii characters");
            }


            var loader = new EdiLoader();

            ConcurrentDictionary<string, Data> data = new();
            Parallel.ForEach(matchingFilePaths2, fileName => data[fileName] = new Data
            {
                filename = fileName,
                lines = File.ReadAllLines(fileName),
                log = loader.LoadFromFile(fileName)

            });


            Console.WriteLine($"Loaded {data.Count} files");

            CheckBands(data);

            var ALLQso = data.SelectMany(x => x.Value.log.QSORecords).ToArray();
            Array.Sort(ALLQso, (a, b) => a.Time.CompareTo(b.Time));

            LinkedList<QSORecord> q = new LinkedList<QSORecord>();
            Dictionary<string, HashSet<LinkedListNode<QSORecord>>> counter = new();

            Dictionary<string, TrueWWL> WWLTruth = prepareTrueWWLs(ALLQso);

            List<PatchMulti> patch = preparePatches(ALLQso, q, counter, WWLTruth);

            Console.WriteLine($"{patch.Count} RST/WWL should be fixed");



            var files = data.ToDictionary(x => x.Value.log.Call, x => x.Key);


            HashSet<string> ToSave = new();

            foreach (var p in patch)
            {

                var filenName = files[p.CallsignReportToPatch];
                var entry = data[filenName];

                var currLine = entry.lines[p.line];

                var splitted = currLine.Split(';');

                if (splitted[2] != p.callsign)
                    throw new Exception($"Did not find {p.CallsignReportToPatch} in {filenName}");

                // fix Rec RST
                if (!string.IsNullOrEmpty(p.recRST))
                    splitted[6] = p.recRST;

                if (!string.IsNullOrEmpty(p.WWL))
                    splitted[9] = p.WWL;

                var newStuff = string.Join(";", splitted);

                Console.WriteLine($"{filenName} : \n\t{entry.lines[p.line]}\n\t{newStuff}\n");
                Console.WriteLine(p.Comment);

                entry.lines[p.line] = newStuff;

                ToSave.Add(filenName);


            }

            if (ToSave.Count > 0)
            {

                Console.WriteLine("Apply fixes? (Y)");
                var confirm = Console.ReadLine();

                if (confirm.ToUpper() == "Y")
                {
                    foreach (var fileName in ToSave)
                    {
                        Console.WriteLine($"Saving...{fileName}");
                        File.WriteAllLines(fileName, data[fileName].lines);
                    }
                }
                else Console.WriteLine("Aborted");

            }
            else Console.WriteLine("No changes required");



        }

        private static void CheckBands(ConcurrentDictionary<string, Data> data)
        {
            var bands = data.Values.Select(v => v.log.Band).Where(v => v.HasValue).Select(v => v.Value).GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count());

            if (bands.Count > 1)
            {
                foreach (var kvp in bands)
                {
                    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                }
                throw new Exception($"Band data is mixed/wrong");
            }
        }
        private static string SelectSquare(string[] options)
        {
            Console.WriteLine("\nSelect the grid or enter correct one: ");

            for (int i = 0; i < options.Length; i++)
            {
                var prfx = call3.GetPrefixes6(options[i]);
                Console.WriteLine($"[{i + 1}] {options[i]} ({string.Join(" ", prfx)}...)");
            }
            var good = false;
            while (!good)
            {
                Console.Write("\n?=");
                var line = Console.ReadLine();
                if (int.TryParse(line.Trim(), out int idx) && idx >= 1 && idx <= options.Length)
                {
                    return options[idx - 1];
                }

                good = MaidenheadLocatorUtils.IsValidMaidenhead6(line.Trim());
                if (good)
                {
                    return line.Trim();
                }
                else
                    Console.WriteLine($"\nDid not seem right...");

            }

            return null;

        }

        private static Dictionary<string, TrueWWL> prepareTrueWWLs(QSORecord[] ALLQso)
        {
            var needHelp = false;

            foreach (var qso in ALLQso)
            {

                var opWWLrecord = getForWWL(qso.OpCallsign);

                opWWLrecord.SelfReported = qso.OpWWL;
                opWWLrecord.relatedCallsigns.Add(qso.OpCallsign);
                opWWLrecord.OtherReport(qso.OpWWL, qso.OpCallsign);


                var WWLrecord = getForWWL(qso.Callsign);
                WWLrecord.OtherReport(qso.ReceivedWWL, qso.Callsign);
            }


            Dictionary<string, TrueWWL> WWLTruth = new();
            foreach (var kv in wwls)
            {
                var candidate = kv.Value.Truth(out string comment);

                if (candidate == null)
                {
                    kv.Value.Log();
                    candidate = SelectSquare(kv.Value.H.Keys.ToArray());
                }

                if (!string.IsNullOrEmpty(candidate))
                {
                    WWLTruth[kv.Key] = new TrueWWL()
                    {
                        WWL = candidate,
                        Comment = comment
                    };
                }
                else
                    needHelp = true;

            }

            if (needHelp)
                throw new Exception($"Could not determine thruth...");

            return WWLTruth;
        }

        private static List<PatchMulti> preparePatches(QSORecord[] ALLQso, LinkedList<QSORecord> q, Dictionary<string, HashSet<LinkedListNode<QSORecord>>> counter, Dictionary<string, TrueWWL> TrueWWL)
        {
            List<PatchMulti> patch = new();

            // Patch WWLs
            foreach (var newQso in ALLQso)
            {

                if (TrueWWL.TryGetValue(newQso.Callsign, out TrueWWL trueWWL) && trueWWL.WWL != newQso.ReceivedWWL)
                {
                    patch.Add(new PatchMulti()
                    {
                        callsign = newQso.Callsign,
                        line = newQso.LineNumber,
                        CallsignReportToPatch = newQso.OpCallsign,
                        WWL = trueWWL.WWL,
                        Comment = trueWWL.Comment,
                    });
                }
            }

            // Patch Signal reports
            foreach (var newQso in ALLQso)
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

                    //                    Console.WriteLine(string.Join("\n", nodes.Select(x => $"{added.Value.Time} {added.Value.OpCallsign} {added.Value.Callsign}  {added.Value.SentRST}  {added.Value.ReceivedRST} @ {added.Value.LineNumber}")));

                    var Sent = added.Value.SentRST;
                    var Rec = added.Value.ReceivedRST;

                    foreach (var qso in nodes)
                    {

                        if (qso.Value.OpCallsign != added.Value.Callsign)
                            throw new Exception("logic broken, should not happen");


                        if (qso.Value.SentRST != added.Value.ReceivedRST)
                        {
                            patch.Add(new PatchMulti()
                            {
                                callsign = added.Value.Callsign,
                                line = added.Value.LineNumber,
                                recRST = qso.Value.SentRST,
                                CallsignReportToPatch = added.Value.OpCallsign
                            });
                        }

                        if (qso.Value.ReceivedRST != added.Value.SentRST)
                        {
                            patch.Add(new PatchMulti()
                            {
                                callsign = qso.Value.Callsign,
                                line = qso.Value.LineNumber,
                                recRST = added.Value.SentRST,
                                CallsignReportToPatch = qso.Value.OpCallsign
                            });
                        }


                    }

                    //   Console.WriteLine(string.Join("\n", nodes.Select(x => $"{x.Value.Time} {x.Value.OpCallsign} {x.Value.Callsign}  {x.Value.SentRST}  {x.Value.ReceivedRST} @ {x.Value.LineNumber}")));


                }
            }

            return patch;

        }
    }
}
