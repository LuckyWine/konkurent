using System;
using System.IO;
using System.Linq;

namespace LogParsing.LogParsers
{
    public class PLinqLogParser : ILogParser
    {
        private readonly FileInfo file;
        private readonly Func<string, string?> tryGetIdFromLine;

        public PLinqLogParser(FileInfo file, Func<string, string?> tryGetIdFromLine)
        {
            this.file = file;
            this.tryGetIdFromLine = tryGetIdFromLine;
        }

        public string[] GetRequestedIdsFromLogFile()
        {
            var lines = File.ReadLines(file.FullName);
            return lines
                .AsParallel()
                .Select(tryGetIdFromLine)
                .Where(id => id != null)
                .ToArray();
        }
    }
    
    public class ParallelLogParser : ILogParser
    {
        private readonly FileInfo file;
        private readonly Func<string, string?> tryGetIdFromLine;

        public ParallelLogParser(FileInfo file, Func<string, string?> tryGetIdFromLine)
        {
            this.file = file;
            this.tryGetIdFromLine = tryGetIdFromLine;
        }


        public string[] GetRequestedIdsFromLogFile()
        {
            var lines = File.ReadLines(file.FullName);
            var resultCollection = new ConcurrentBag<string>();

            Parallel.ForEach(lines, line =>
            {
                var id = tryGetIdFromLine(line);
                if (id != null)
                    resultCollection.Add(id);
            });
            return resultCollection.ToArray();
        }
    }
    
    public class ThreadLogParser : ILogParser
    {
        private readonly FileInfo file;
        private readonly Func<string, string> tryGetIdFromLine;

        public ThreadLogParser(FileInfo file, Func<string, string> tryGetIdFromLine)
        {
            this.file = file;
            this.tryGetIdFromLine = tryGetIdFromLine;
        }

        public string[] GetRequestedIdsFromLogFile()
        {
            var lines = File.ReadLines(file.FullName)
                .Select((s, i) => new {Value = s, Index = i})
                .GroupBy(x => x.Index / 1000);
            var threads = new List<Thread>();
            var result = new ConcurrentBag<string>();
            foreach (var lineGroup in lines)
            {
                var thread = new Thread(() =>
                {
                    foreach (var line in lineGroup)
                    {
                        var id = tryGetIdFromLine(line.Value);
                        if (id != null)
                            result.Add(id);
                    }
                });
                
                threads.Add(thread);
                thread.Start();
            }

            foreach (var thread in threads)
                thread.Join();
            return result.ToArray();
        }
    }
}