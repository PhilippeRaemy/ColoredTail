using System;
using System.Linq;
using ConsoleHelpers;

namespace ConsoleColor
{
    using System.Collections.Generic;
    using System.Globalization;
    using ConsoleColor = System.ConsoleColor;

    static class Program
    {
        static void Main(string[] args)
        {
            bool SwitchExists(string s) => args.Any(a => a.Equals(s, StringComparison.InvariantCultureIgnoreCase));

            if (SwitchExists("--help")
             || SwitchExists("-h")
             || SwitchExists("/h")
             || SwitchExists("-?")
             || SwitchExists("/?"))
            {
                Console.WriteLine("Syntax is :");
                Console.WriteLine(">ConsoleColor Free Text From Which To Infer New Color");
                Console.WriteLine(".. or, to set the console title and infer color from it:");
                Console.WriteLine(">ConsoleColor --setTitle New Console Title From Which To Infer New Color");
                Console.WriteLine(".. or, to infer color from current console title");
                Console.WriteLine(">ConsoleColor --setTitle");
                Console.WriteLine(".. or, to set color from RGB components (must be integers)");
                Console.WriteLine(">ConsoleColor --RGB r g b");
                Console.WriteLine(".. or, to reset color to defaults");
                Console.WriteLine(">ConsoleColor --default");
                Console.WriteLine(".. to display a sample color table");
                Console.WriteLine(">ConsoleColor --sample");
                return;
            }
            if (SwitchExists("--rgb"))
            {
                SetColorRgb(args);
            }
            else if (SwitchExists("--default"))
            {
                ConsoleColorsBase.GetConsoleColor().ResetDefaultColors();
            }
            else
            {
                SetColorFromTitle(SwitchExists("--setTitle"), args.Where(a => !a.StartsWith("--")).ToArray());
            }
            Console.ResetColor();
            if (SwitchExists("--sample"))
            {
                var fbg = Console.BackgroundColor;
                var ffg = Console.ForegroundColor;
                for (var bg = 0; bg < 16; bg++)
                {
                    Console.BackgroundColor = (ConsoleColor)bg;
                    for (var fg = 0; fg < 16; fg++)
                    {
                        Console.ForegroundColor = (ConsoleColor)fg;
                        Console.Write($" {bg:X}{fg:X}");
                    }
                    Console.WriteLine(" ");
                }
                Console.BackgroundColor = fbg;
                Console.ForegroundColor = ffg;
            }
        }

        static void SetColorFromTitle(bool setTitle, string[] args)
        {
            var title = args.Any() ? string.Join(" ", args) : Console.Title;
            ConsoleColorsBase.GetConsoleColor().InferColorFromText(title);
            if (setTitle)
            {
                Console.Title = title;
            }
        }

        static void SetColorRgb(IEnumerable<string> args)
        {
            var colors = args
                .Where(a => !a.StartsWith("--"))
                .Select(a =>
                {
                    int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c);
                    return c;
                })
                .Concat(new[] {0, 0, 0})
                .Take(3)
                .ToArray();
            ConsoleColorsBase.GetConsoleColor().SetColorRgb(colors[0], colors[1], colors[2]);
        }
    }
}
