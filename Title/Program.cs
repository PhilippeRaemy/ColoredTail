using System;
using System.Linq;
using ConsoleHelpers;

namespace ColoredTitle
{
    static class Program
    {
        static void Main(string[] args)
        {
            var title = args.Any() ? string.Join(" ", args) : Console.Title;
            new ConsoleColors().InferColorFromText(title).SetConsoleTitle(title);
//                cc.Swap();
//                cc.Swap();
//                Console.WriteLine(DateTime.Now);
            Console.WriteLine(title);
            Console.ResetColor();
        }
    }
}
