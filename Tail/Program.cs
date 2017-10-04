using System;
using System.IO;
using System.Linq;
using System.Threading;
using ConsoleHelpers;

namespace Tail
{
    using System.Diagnostics;

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
        static ConsoleColorsBase GetConsoleColor()
        {
            try
            {
                return _nocolor ? (ConsoleColorsBase) new ConsoleNoColors() : new ConsoleColors();
            }
            catch
            {
                _nocolor = true;
                return new ConsoleNoColors();
            }
        }

        static void Main(string[] args)
        {
            // Debugger.Launch();
            if (args.Length == 0)
            {
                Console.WriteLine("Please specify a valid folder, file name or file pattern in the command line.");
                return;
            }

            _nocolor = args.Any(a => a.Equals("--nocolors", StringComparison.InvariantCultureIgnoreCase));
            Console.CancelKeyPress += Console_CancelKeyPress;
            var filename = args.Select(FindLastFileLike).FirstOrDefault();
            if (filename == null)
            {
                Console.WriteLine(new FileNotFoundException().Message);
                filename = args[0];
                Console.WriteLine($"No valid folder, file name or file pattern in the command line. Waiting for {filename} to be created.");
            }
            Console.Title = $"Tailing file {filename}";
            Console.WriteLine($"Tailing file {filename}");
            using (var cColor = GetConsoleColor())
            {
                _consoleColors = cColor; // keep a reference for use in the CancelKeyPress event, if ever...
                cColor.InferColorFromText(Console.Title);
                var fileState = new FileState {Status = FileStatus.Unknown};
                while (true)
                {
                    try
                    {
                        var newfileState = DoTail(filename, fileState);
                        if (fileState.Status != newfileState.Status)
                        {

                            Console.WriteLine();
                            switch (newfileState.Status)
                            {
                                case FileStatus.NotExist:
                                    using (new ConsoleColors().Swap())
                                        Console.WriteLine($"Waiting for {filename} to be created.");
                                    break;
                                case FileStatus.Shrunk:
                                    using (new ConsoleColors().Swap())
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
            using (GetConsoleColor().Swap())
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

        static FileState DoTail(string filename, FileState state)
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
                        Console.Write(new string(buffer, bytesToBeSkipped, bytes - bytesToBeSkipped));
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
}
