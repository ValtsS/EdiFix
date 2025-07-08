using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace EdiFix
{


    public class Call3
    {
        private readonly Dictionary<string, string> _callsignToGridSquare = new();
        private readonly Dictionary<string, HashSet<string>> _square4ToCallsign = new();
        private readonly Dictionary<string, HashSet<string>> _square6ToCallsign = new();

        /// <summary>
        /// Loads data from the specified file.
        /// </summary>
        /// <param name="filePath">Path to call3.txt</param>
        public void Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found.", filePath);
            }

            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');

                if (parts.Length < 2)
                    continue;

                var callsign = parts[0].Trim();
                var gridSquare = parts[1].Trim();

                if (!string.IsNullOrEmpty(callsign) && !string.IsNullOrEmpty(gridSquare))
                {
                    _callsignToGridSquare[callsign] = gridSquare;
                    var grid4 = gridSquare.Substring(0, 4);
                    if (!_square4ToCallsign.TryAdd(grid4, new HashSet<string>() { callsign }))
                        _square4ToCallsign[grid4].Add(callsign);

                    if (!_square6ToCallsign.TryAdd(gridSquare, new HashSet<string>() { callsign }))
                        _square6ToCallsign[gridSquare].Add(callsign);

                }
            }
        }

        /// <summary>
        /// Gets the grid square for a given callsign.
        /// </summary>
        /// <param name="callsign">The callsign to look up.</param>
        /// <returns>The grid square or null if not found.</returns>
        public string? GetGridSquare(string callsign)
        {
            return _callsignToGridSquare.TryGetValue(callsign, out var gridSquare)
                ? gridSquare
                : null;
        }

        private static string getprefix(string callsign)
        {
            for (int i = 0; i < callsign.Length; i++)
            {
                if (callsign[i]=='/')
                {
                    return callsign.Substring(0, i);
                }
                else if (char.IsDigit(callsign[i]))
                {
                    return callsign.Substring(0, i+1);
                }

            }

            return callsign;
        }

        public string[] GetPrefixes4(string grid)
        {
            var grid4 = grid.Substring(0, 4).ToUpperInvariant();
            if (!_square4ToCallsign.ContainsKey(grid4))
                return [];
            return _square4ToCallsign[grid4].Select(v => getprefix(v)).ToHashSet().ToArray();
        }


        public string[] GetPrefixes6(string grid)
        {
            if (!_square6ToCallsign.ContainsKey(grid.ToUpperInvariant()))
                return [];

            return _square6ToCallsign[grid.ToUpperInvariant()].Select(v => getprefix(v)).ToHashSet().ToArray();
        }

    }

}
