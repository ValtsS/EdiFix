using System;
using System.Collections.Generic;
using System.IO;

namespace EdiFix
{


    public class Call3
    {
        private readonly Dictionary<string, string> _callsignToGridSquare = new();

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
    }

}
