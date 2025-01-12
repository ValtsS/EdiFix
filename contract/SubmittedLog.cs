using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ylac_judge.contract
{

    enum EnumBand : ushort
    {
        Band6m = 1,
        Band4m = 2,
        Band2m = 4,
        Band70cm = 8,
        Band23cm = 16,
    }

    internal class QSORecord
    {
        public DateTime Time { get; set; }
        public string Callsign { get; set; }
        public string Mode { get; set; }
        public string SentRST { get; set; }
        public string SentNumber { get; set; }
        public string ReceivedRST { get; set; }
        public string ReceivedNumber { get; set; }
        public string ReceivedExchange { get; set; }
        public string ReceivedWWL { get; set; }

        public string OpCallsign { get; set; }
        public string OpWWL { get; set; }
        public EnumBand? Band { get; set; }

        public int LineNumber { get; set; }


        public string GetID()
        {
                return $"{OpCallsign};{Callsign}";

            return $"{Callsign};{OpCallsign}";
        }

        public string GetIDReverse()
        {
            return $"{Callsign};{OpCallsign}";
        }


    }


    internal class SubmittedLog
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }

        public EnumBand? Band { get; set; }
        public string PWWLo { get; set; }
        public string Call { get; set; }
        public string PExch { get; set; }

        public QSORecord[] QSORecords { get; set; }

    }

    enum JudgeResult : ushort {
        Unknown = 0,
        Success = 1,
        Dupe = 2,
        OutOfTime = 3,
    }


    internal class ContestQSO
    {
        public DateTime UTC { get; set; }
        public string QTH { get; set; }
        public string Callsign { set; get; }
        public string Correspondent { get; set; }
        public string Mode { get; set; }
        public string WWLoc { get; set; }
        public string Sent { get; set; }
        public string Recvd { get; set; }
        public string Points { get; set; }
        public string Remark { get; set; }
    }


    internal class Contest
    {

        public DateTime  StartDate { get; set; }
        public DateTime   EndDate { get; set; }
        public EnumBand Band { get;set; }




    }


}
