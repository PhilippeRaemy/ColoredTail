using System;
using System.IO;
using System.Linq;
using System.Threading;
using ConsoleHelpers;

namespace Tail
{
    using System.Diagnostics;
    using System.Text.RegularExpressions;

    enum FileStatus {Unknown, NotExist, Shrunk, Idle}

    class FileState
    {
        public FileStatus Status { get; set; }
        public int        Ptr    { get; set; }
    }

    static class Program
    {
        static ConsoleColorsBase _consoleColors;
        static bool _nocolor;

        static void Main(string[] args)
        {
            // Debugger.Launch();

            if(!args.Any() || args.Any(a => new[]{"/?", "-?", "-h", "--help", "-H", "--HELP" }.Contains(a)))
            {
                Console.WriteLine("Usage is Tail.exe filename [--nocolors] [--filter-regex=filter-regex] [--filter=filter-exact] [-i] [-v]");
                return;
            }

            var filePattern = args.FirstOrDefault(a => !a.StartsWith("--"));
            if(filePattern == null)
            {
                Console.WriteLine("Please specify a valid folder, file name or file pattern in the command line.");
                return;
            }

            var filter = args.FirstOrDefault(a => a.StartsWith("--filter="))?.Split(new[] { '=' }, 2).Skip(1).Single();
            var filterRegex = args.FirstOrDefault(a => a.StartsWith("--filter-regex="))?.Split(new []{'='}, 2).Skip(1).Single();
            var filterString = string.IsNullOrEmpty(filterRegex) ? filter : filterRegex;
            var writer = new Writer(
                filterString,
                args.Any(a => string.Equals(a, "--i")),
                args.Any(a => string.Equals(a, "--v")),
                !string.IsNullOrEmpty(filterRegex)
            );

            _nocolor = args.Any(a => a.Equals("--nocolors", StringComparison.InvariantCultureIgnoreCase));
            Console.CancelKeyPress += Console_CancelKeyPress;

            var filename = FindLastFileLike(filePattern);
            if (filename == null)
            {
                Console.WriteLine(new FileNotFoundException().Message);
                filename = args[0];
                Console.WriteLine($"No valid folder, file name or file pattern in the command line. Waiting for {filename} to be created.");
            }
            
            Console.Title = $"Tailing file {filename}" + (string.IsNullOrWhiteSpace(filter) ? string.Empty : $" - {filterString}");
            Console.WriteLine(Console.Title);
            using (var cColor = ConsoleColorsBase.GetConsoleColor())
            {
                _consoleColors = cColor; // keep a reference for use in the CancelKeyPress event, if ever...
                cColor.InferColorFromText(Console.Title);
                var fileState = new FileState {Status = FileStatus.Unknown};
                while (true)
                {
                    try
                    {
                        var newfileState = DoTail(filename, fileState, writer.Write);
                        if (fileState.Status != newfileState.Status)
                        {

                            Console.WriteLine();
                            switch (newfileState.Status)
                            {
                                case FileStatus.NotExist:
                                    using (cColor.Swap())
                                        Console.WriteLine($"Waiting for {filename} to be created.");
                                    break;
                                case FileStatus.Shrunk:
                                    using (cColor.Swap())
                                        Console.WriteLine($"Restarting {filename}.");
                                    break;
                                case FileStatus.Unknown:
                                case FileStatus.Idle:
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }
                        fileState = newfileState;
                    }
                    catch
                    {
                        fileState = new FileState {Status = FileStatus.Unknown};
                    }
                    if (Console.KeyAvailable)
                    {
                        Console.ReadKey();
                        Console_CancelKeyPress(null, null);
                        break;
                    }
                    Thread.Sleep(100);
                }
            }
        }

        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            using (ConsoleColorsBase.GetConsoleColor().Swap())
            {
                Console.WriteLine();
                Console.WriteLine("Done.");
            }
            _consoleColors?.Dispose();
        }

        static string FindLastFileLike(string filename)
        {
            var folder = new DirectoryInfo(filename);
            if (!folder.Exists)
            {
                var foldername = Path.GetDirectoryName(filename) ?? Environment.CurrentDirectory;
                folder = new DirectoryInfo(foldername);
                if (!folder.Exists)
                    return null;
            }

            return folder
                .EnumerateFiles(Path.GetFileName(filename) ?? "*")
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => f.FullName)
                .FirstOrDefault();
        }

        static FileState DoTail(string filename, FileState state, Action<string> writer)
        {
            const int maxDataChunk = 1024 * 512; //1024 * 1024;

            var fileInfo = new FileInfo(filename);
            if (!fileInfo.Exists)
            {
                return new FileState {Status = FileStatus.NotExist};
            }
            // Console.WriteLine($"fileInfo.Length={fileInfo.Length}, state.Ptr={state.Ptr}");
            if (fileInfo.Length == state.Ptr)
            {
                return new FileState { Status = FileStatus.Idle, Ptr = state.Ptr };
            }
            if (fileInfo.Length < state.Ptr)
            {
                return new FileState { Status = FileStatus.Shrunk, Ptr = 0};
            }
            state.Status = FileStatus.Idle;
            var buffer = new char[maxDataChunk];
            var bytesRead = 0;
            var bytesToBeSkipped = state.Ptr;
            using (var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var textReader = new StreamReader(fileStream))
            {
                while (bytesRead < fileInfo.Length)
                {
                    var bytes = textReader.ReadBlock(buffer, 0, maxDataChunk);
                    if (bytes > bytesToBeSkipped)
                    {
                        writer(new string(buffer, bytesToBeSkipped, bytes - bytesToBeSkipped));
                    }
                    bytesToBeSkipped = bytesToBeSkipped > bytes ? bytesToBeSkipped - bytes : 0;
                    bytesRead += bytes;
                }
            }
            state.Ptr = bytesRead;
            // Debug.Assert(fileInfo.Length >= state.Ptr);
            return state;
        }
    }

    class Writer
    {
        readonly Regex _filterRegex;
        readonly Regex _splitter = new Regex(@"(?'fullLines'.*)([\r\n]+)(?'lastLine'[^\r\n]*)$", RegexOptions.Singleline);
        string _remains;

        public Writer(string filter, bool caseInsentive, bool reverse, bool isRegex)
        {
            if (isRegex) filter = Regex.Replace(filter, @"[\#&\[\]\(\)\{\}\?\<\>\.\^\$\+\*\\]", ma => @"\\" + ma.Value);

            _filterRegex = new Regex(
                reverse ? $"^(?!.*{filter}).*$" : $"^.*{filter}.*$", 
                RegexOptions.CultureInvariant | RegexOptions.Multiline | 
                (caseInsentive ? RegexOptions.IgnoreCase : RegexOptions.None)
            );
        }

        public void Write(string buffer)
        {
            if (_filterRegex == null)
            {
                Console.Write(buffer);
                return;
            }

            var split = _splitter.Match(_remains + buffer);
            if (!split.Success)
            {
                _remains += buffer;
                return;
            }

            foreach (Match line in _filterRegex.Matches(split.Groups["fullLines"].Value))
            {
                Console.WriteLine(line.Value);
            }

            _remains = split.Groups["lastLines"].Value;
        }
    }
}
