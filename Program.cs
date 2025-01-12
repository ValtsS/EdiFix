using System.Collections.Concurrent;
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
        }


        static void Main(string[] args)
        {
            IEnumerable<String> matchingFilePaths2 = System.IO.Directory.EnumerateFiles(pwd(), "*.edi", System.IO.SearchOption.TopDirectoryOnly);


            var loader = new EdiLoader();

            ConcurrentDictionary<string, Data> data = new();
            Parallel.ForEach(matchingFilePaths2, fileName => data[fileName] = new Data
            {
                filename = fileName,
                lines = File.ReadAllLines(fileName),
                log = loader.LoadFromFile(fileName)

            });


            Console.WriteLine($"Loaded {data.Count} files");


            var ALLQso = data.SelectMany(x => x.Value.log.QSORecords).ToArray();
            Array.Sort(ALLQso, (a,b)  => a.Time.CompareTo(b.Time) );

            LinkedList<QSORecord> q = new LinkedList<QSORecord>();
            Dictionary<string, HashSet<LinkedListNode<QSORecord>>> counter = new();

            List<PatchMulti> patch = new List<PatchMulti>();
            Dictionary<string, string[]> TrueWWL = data.ToDictionary(x => x.Value.log.Call, x => new string[] { x.Value.log.PWWLo });


            foreach(var newQso in ALLQso)
            {

                var added = q.AddLast(newQso);
                var addedId = added.Value.GetID();
                var reverse = added.Value.GetIDReverse();

                if (!counter.ContainsKey(addedId))
                    counter[addedId] = new();

                counter[addedId].Add(added);

                while (q.Count > 0 && (q.Last.Value.Time - q.First.Value.Time) > TimeSpan.FromMinutes(thresholdMinutes) )
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

                    foreach(var qso in nodes)
                    {

                        if (qso.Value.OpCallsign != added.Value.Callsign)
                            throw new Exception("logic broken, should not happen");

                        if (TrueWWL.TryGetValue(qso.Value.Callsign, out string[] recWWL) && !recWWL.Contains(qso.Value.ReceivedWWL) )
                        {
                            patch.Add(new PatchMulti() {
                                callsign = added.Value.OpCallsign,
                                line = qso.Value.LineNumber,
                                CallsignReportToPatch = added.Value.Callsign,
                                WWL = recWWL[0]
                            });
                        }


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


            Console.WriteLine($"{patch.Count} RST/WWL should be fixed");


            var files = data.ToDictionary(x => x.Value.log.Call, x => x.Key);


            HashSet<string> ToSave = new();

            foreach(var p in patch)
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

                Console.WriteLine($"{filenName} : \n\t{entry.lines[p.line]}\n\t{newStuff}\n\n");

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
    }
}
