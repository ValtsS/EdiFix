using System.IO;
using System.Linq;
using EdiFix.Models;
using EdiFix.Services;
using ylac_judge.contract;

namespace EdiFix
{
    internal class Program
    {
        private const int ThresholdMinutes = 5;
        private static readonly string[] TerritoryPrefixes = { "YL" };

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

            var callsignToFile = data.Values
                .Where(v => !string.IsNullOrEmpty(v.log.Call))
                .GroupBy(v => v.log.Call)
                .ToDictionary(g => g.Key, g => g.First().filename, StringComparer.OrdinalIgnoreCase);

            if (!CheckMissingEdiFiles(data, allQso, callsignToFile))
            {
                return;
            }

            var wwlTruths = wwlResolver.ResolveTruths(allQso);
            var wwlPatches = wwlResolver.GenerateWwlPatches(allQso, wwlTruths);
            var signalPatches = ediProcessor.GenerateSignalPatches(allQso, ThresholdMinutes);

            var allPatches = wwlPatches.Concat(signalPatches).ToList();
            Console.WriteLine($"{allPatches.Count} RST/WWL should be fixed");

            ediProcessor.ApplyPatches(allPatches, data, callsignToFile);
        }

        private static bool CheckMissingEdiFiles(IDictionary<string, EdiData> data, QSORecord[] allQso, IDictionary<string, string> callsignToFile)
        {
            var expectedCallsigns = data.Values.Select(v => v.log.Call)
                .Concat(allQso.Select(q => q.Callsign))
                .Where(c => !string.IsNullOrEmpty(c))
                .Where(c => TerritoryPrefixes.Any(p => c.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            var missingFiles = expectedCallsigns.Where(c => !callsignToFile.ContainsKey(c)).ToList();

            if (missingFiles.Count > 0)
            {
                Console.WriteLine("\nMissing .EDI files for following stations:");
                foreach (var callsign in missingFiles)
                {
                    Console.WriteLine($" - {callsign}");
                }
                Console.WriteLine("\nTotal missing: " + missingFiles.Count + " / " + expectedCallsigns.Count + " expected stations found matching prefixes.");
                Console.WriteLine("\nContinue anyway? (Y/N)");
                var confirm = Console.ReadLine();
                if (confirm?.Trim().ToUpper() != "Y")
                {
                    Console.WriteLine("Aborted.");
                    return false;
                }
            }
            return true;
        }
    }
}
