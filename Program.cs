using System.IO;
using System.Linq;
using EdiFix.Models;
using EdiFix.Services;

namespace EdiFix
{
    internal class Program
    {
        private const int ThresholdMinutes = 5;

        static string pwd()
        {
            return Directory.GetCurrentDirectory();
        }

        static void Main(string[] args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = Path.Combine(baseDir, "call3.txt");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Call3.txt File not found.", filePath);
            }

            var call3 = new Call3();
            call3.Load(filePath);

            var ediProcessor = new EdiProcessor();
            var wwlResolver = new WwlResolver(call3);

            var matchingFilePaths = Directory.EnumerateFiles(pwd(), "*.edi", SearchOption.TopDirectoryOnly).ToList();

            var badFileNames = matchingFilePaths.Select(Path.GetFileName).Where(f => f != null && !f.All(c => c <= 127)).ToArray();

            if (badFileNames.Length > 0)
            {
                Console.WriteLine(string.Join("\n", badFileNames));
                throw new Exception("Detected filename with non-ASCII characters");
            }

            var data = ediProcessor.LoadEdiFiles(matchingFilePaths);
            Console.WriteLine($"Loaded {data.Count} files");

            ediProcessor.CheckBands(data.Values);

            var allQso = data.SelectMany(x => x.Value.log.QSORecords).OrderBy(q => q.Time).ToArray();

            var wwlTruths = wwlResolver.ResolveTruths(allQso);
            var wwlPatches = wwlResolver.GenerateWwlPatches(allQso, wwlTruths);
            var signalPatches = ediProcessor.GenerateSignalPatches(allQso, ThresholdMinutes);

            var allPatches = wwlPatches.Concat(signalPatches).ToList();
            Console.WriteLine($"{allPatches.Count} RST/WWL should be fixed");

            var callsignToFile = data.ToDictionary(x => x.Value.log.Call, x => x.Key);

            ediProcessor.ApplyPatches(allPatches, data, callsignToFile);
        }
    }
}
