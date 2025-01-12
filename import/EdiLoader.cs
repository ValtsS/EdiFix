using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using ylac_judge.contract;

namespace ylac_judge.import
{

    internal interface ILoader
    {
        public abstract SubmittedLog Load(Stream stream);
    }


    internal class EdiParams
    {
        Dictionary<string, string> parms;
        public EdiParams(Dictionary<string,string> parms)
        {
            this.parms = parms;
        }

        public DateOnly? getTDateStart()
        {
            if (parms.TryGetValue("TDATE", out string val))
            {
                if (val.Length == 17 && val[8] == ';')
                {
                    if (DateOnly.TryParseExact(val.Substring(0, 8), "yyyyMMdd", out DateOnly startDate))
                    {
                        return startDate;
                    }
                }
            }
            return null;
        }

        public DateOnly? getTDateEnd()
        {
            if (parms.TryGetValue("TDATE", out string val))
            {
                if (val.Length == 17 && val[8] == ';')
                {
                    if (DateOnly.TryParseExact(val.Substring(9), "yyyyMMdd", out DateOnly startDate))
                    {
                        return startDate;
                    }
                }
            }
            return null;
        }

        public string? getSpecific(string name)
        {
            if (parms.TryGetValue(name.ToUpper(), out string callsign))
            {
                return callsign.Trim();
            }
            return null;
        }


        public string? getCallsign()
        {
            if (parms.TryGetValue("PCALL", out string callsign))
            {
                return callsign.Trim().ToUpper();
            }

            if (parms.TryGetValue("RCALL", out string rcallsign))
            {
                return rcallsign.Trim().ToUpper();
            }

            return null;
        }


        public EnumBand? getBand()
        {
            if (parms.TryGetValue("PBAND", out string band))
            {
                var fb = band.Replace(" ", "").Replace(",", ".").Trim().ToUpper();

                switch (fb)
                {
                    case "50MHZ":
                        return EnumBand.Band6m;
                    case "70MHZ":
                        return EnumBand.Band4m;
                    case "144MHZ":
                        return EnumBand.Band2m;
                    case "432MHZ":
                        return EnumBand.Band70cm;
                    case "1.3GHZ":
                        return EnumBand.Band23cm;
                }
            }
            return null;
        }



    }


    internal class EdiLoader : ILoader
    {
        private const int qsoSemisCount = 14;

        private bool IsQSO(string line)
        {
            return line.Count(x => x == ';') == qsoSemisCount;
        }

        private bool IsParam(string line)
        {
            var inx = line.IndexOf('=');
            if (inx > 0)
            {
                for (int i = 0; i < inx; i++)
                {
                    var valid = line[i] switch
                    {

                        >= 'A' and <= 'Z' => true ,
                        >= 'a' and <= 'z' => true,
                        _ => false
                    };

                    if (!valid)
                        return valid;

                }
                return true;
            }

            return false;
        }

        private void RecordParam(Dictionary<string, string> Params, string line)
        {
            var inx = line.IndexOf('=');
            Params[line.Substring(0, inx).ToUpper().Trim()] = line.Substring(inx + 1);

        }

        private void RecordQSO(List<(string[],int)> RawQSOs, string line, int lineNumber)
        {
            RawQSOs.Add((line.Split(';'), lineNumber));
        }

        private QSORecord parseQSO(SubmittedLog log, string[] data, int lineNumber)
        {
            var yearPrefix = log.StartDate.Value.Year.ToString().Substring(0, 2);

            return new QSORecord()
            {
                Time = DateTime.ParseExact($"{yearPrefix}{data[0]} {data[1]}", "yyyyMMdd HHmm", CultureInfo.InvariantCulture),
                Callsign = data[2].Trim().ToUpper(),
                Mode = data[3],
                SentRST = data[4],
                SentNumber = data[5],
                ReceivedRST = data[6],
                ReceivedNumber = data[7],
                ReceivedExchange = data[8],
                ReceivedWWL=data[9].Trim().ToUpper(),
                OpCallsign = log.Call,
                OpWWL = log.PWWLo,
                Band = log.Band,
                LineNumber = lineNumber
            };
        }

        public SubmittedLog LoadFromFile(string filename)
        {
            using (FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                return Load(fileStream);
            }
        }


        public SubmittedLog Load(Stream stream)
        {

            var Params = new Dictionary<string, string>();
            var RawQSOs = new List<(string[],int)>();


            using (StreamReader reader = new StreamReader(stream))
            {
                string line;
                int counter = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    if (IsParam(line))
                        RecordParam(Params, line);
                    else if (IsQSO(line))
                        RecordQSO(RawQSOs, line, counter);

                    counter++;
                }
            }

            var ediParms = new EdiParams(Params);

            var log = new SubmittedLog();

            log.StartDate = ediParms.getTDateStart();
            log.EndDate = ediParms.getTDateEnd();

            log.Band = ediParms.getBand();
            log.Call = ediParms.getCallsign();
            log.PWWLo = ediParms.getSpecific("PWWLo").ToUpper();
            log.PExch = ediParms.getSpecific("PExch").ToUpper();

            log.QSORecords = RawQSOs.Select(x => parseQSO(log, x.Item1, x.Item2)).ToArray();

            return log;
        }
    }
}
