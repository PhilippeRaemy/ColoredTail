using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ConsoleClassLibrary;

namespace ConsoleHelpers
{
    using System.Collections.Generic;

    public abstract class ConsoleColorsBase : IDisposable
    {
        public abstract ConsoleColorsBase Swap();

        public abstract void Dispose();
        public abstract ConsoleColorsBase ResetDefaultColors();
        public abstract ConsoleColorsBase InferColorFromText(string text);
        public abstract ConsoleColorsBase SetConsoleTitle(string text);
        public abstract ConsoleColorsBase SetColorRgb(int r, int g, int b);

        static bool _noColor;
        public static ConsoleColorsBase GetConsoleColor()
        {
            try
            {
                return _noColor ? (ConsoleColorsBase) new ConsoleNoColors() : new ConsoleColors();
            }
            catch
            {
                _noColor = true;
                return new ConsoleNoColors();
            }
        }
    }

    class ConsoleNoColors : ConsoleColorsBase
    {
        protected internal ConsoleNoColors(){}
        public override ConsoleColorsBase Swap() => this;
        public override void Dispose(){}
        public override ConsoleColorsBase ResetDefaultColors() => this;
        public override ConsoleColorsBase InferColorFromText(string text)  => this;
        public override ConsoleColorsBase SetConsoleTitle(string text)     => this;
        public override ConsoleColorsBase SetColorRgb(int r, int g, int b) => this;
    }

    public class ConsoleColors : ConsoleColorsBase
    {
        readonly ConsoleColor _foregroundColor;
        readonly ConsoleColor _backgroundColor;
        readonly CONSOLE_SCREEN_BUFFER_INFO_EX _consoleScreenBufferInfoEx;

        protected internal ConsoleColors()
        {
            _foregroundColor = Console.ForegroundColor;
            _backgroundColor = Console.BackgroundColor;
            _consoleScreenBufferInfoEx = GetScreenBufferInfoEx();
        }

        public override ConsoleColorsBase Swap()
        {
            var foregroundColor = Console.ForegroundColor;
            Console.ForegroundColor = Console.BackgroundColor;
            Console.BackgroundColor = foregroundColor;
            return this;
        }

        public override void Dispose()
        {
            Console.ForegroundColor = _foregroundColor;
            Console.BackgroundColor = _backgroundColor;
            SetScreenBufferInfoEx(_consoleScreenBufferInfoEx);
            Console.ResetColor();
            Swap().Swap();
        }

        static int GetColorIndex(ConsoleColor color)
        {
            return Array.FindIndex(Enum.GetValues(typeof (ConsoleColor)).Cast<ConsoleColor>().ToArray(), c=>c==color);
        }

        public override ConsoleColorsBase InferColorFromText(string text)
        {
            var colorComponents = Encoding.Unicode.GetBytes(text)
                .Select((c, i) => new { Component = i % 3, C = (int)c } )
                .GroupBy(c => c.Component)
                .Select(c => c.Sum( v => v.C ) % 256)
                .ToArray();
            return SetColorRgb(colorComponents[0], colorComponents[1] % 256, colorComponents[2] % 256);
        }

        public override ConsoleColorsBase SetColorRgb(int r, int g, int b)
            => SetColorRgbImpl(r % 256, g % 256, b % 256);

        static readonly Color[] DefaultColors = new[]
        {
            Color.FromArgb(0, 0, 0),
            Color.FromArgb(0, 0, 128),
            Color.FromArgb(0, 128, 0),
            Color.FromArgb(128, 0, 0),
            Color.FromArgb(0, 128, 128),
            Color.FromArgb(128, 0, 128),
            Color.FromArgb(128, 128, 0),
            Color.FromArgb(196, 196, 196),
            Color.FromArgb(128, 128, 128),
            Color.FromArgb(0, 0, 255),
            Color.FromArgb(0, 255, 0),
            Color.FromArgb(255, 0, 0),
            Color.FromArgb(0, 255, 255),
            Color.FromArgb(255, 0, 255),
            Color.FromArgb(255, 255, 0),
            Color.FromArgb(255, 255, 255)
        };

        public override ConsoleColorsBase ResetDefaultColors() => SetConsoleColors(DefaultColors);

        double DistanceRgb(Color a, Color b) => Math.Sqrt(
            Math.Pow(a.A - b.A, 2) +
            Math.Pow(a.R - b.R, 2) +
            Math.Pow(a.G - b.G, 2) +
            Math.Pow(a.B - b.B, 2));

        Color[] CheckConsoleColors(IEnumerable<Color> colorTable)
        {
            var cols = colorTable as Color[] ?? colorTable.ToArray();
            var bg = cols[(int)Console.BackgroundColor];
            for (var c = 0; c < cols.Length; c++)
                if (c != (int) Console.BackgroundColor)
                {
                    var distanceRgb = DistanceRgb(bg, cols[c]);
                    //Console.WriteLine($"{c}: |{bg} - {cols[c]}| = {distanceRgb}");
                    if (distanceRgb < 100)
                        cols[c] = Color.FromArgb(
                            (cols[c].R + 128) % 256, 
                            (cols[c].G + 128) % 256, 
                            (cols[c].B + 128) % 256);
                }

            return cols;
        }

        ConsoleColorsBase SetConsoleColors(Color[] colorTable)
        {
            try
            {
                var screenInfo = GetScreenBufferInfoEx();
                for (var idx = 0; idx < 16; idx++)
                {
                    screenInfo.ColorTable[idx].SetColor(colorTable[idx]);
                }
                SetScreenBufferInfoEx(screenInfo);
                Console.ResetColor();
                return Swap().Swap();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return this;
            }
        }

        ConsoleColorsBase SetColorRgbImpl(int r, int g, int b)
        {
            var fromArgb = Color.FromArgb(r, g, b);
            var baseColorHue = fromArgb.GetHue();
            var baseColorSat = fromArgb.GetSaturation();
            var baseColorLum = fromArgb.GetBrightness();
            // Console.WriteLine($"SetColorRgbImpl({r},{g},{b}). Hue ={baseColorHue}");
            return SetConsoleColors(
                CheckConsoleColors(Enumerable.Range(0, 16).Select(idx => ColorFromHsl(
                    (int) (baseColorHue + 2 * idx * 45) % 360,
                    ((idx < 8 ? baseColorLum : baseColorLum + .5f) + idx / 8f) % 1,
                    0.25f + .75f * ((baseColorSat + 5 * idx / 16f) % 1)
                ))));
        }

        Color ColorFromHsl(int hue, float sat, float lum)
        {
            var c = (1 - Math.Abs(2 * lum - 1)) * sat;
            var x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            var rgb = hue <= 060 ? Tuple.Create(c, x, 0f)
                : hue <= 120 ? Tuple.Create(x, c, 0f)
                : hue <= 180 ? Tuple.Create(0f, c, x)
                : hue <= 240 ? Tuple.Create(0f, x, c)
                : hue <= 300 ? Tuple.Create(x, 0f, c)
                : hue <= 360 ? Tuple.Create(c, 0f, x)
                : Tuple.Create(0f, 0f, 0f);
            var m = lum - c / 2;
            var colorFromHsl = Color.FromArgb((int)(255 * (rgb.Item1 + m)), (int)(255 * (rgb.Item2 + m)), (int)(255 * (rgb.Item3 + m)));
            // Console.WriteLine($"ColorFromHsl({hue}, {sat}, {lum}) => [c={c}, x={x}, m={m}] => ({rgb.Item1:f3}, {rgb.Item2:f3}, {rgb.Item3:f3}) => ({colorFromHsl.R}, {colorFromHsl.G}, {colorFromHsl.B})");
            return colorFromHsl;
        }

        public override ConsoleColorsBase SetConsoleTitle(string text)
        {
            ConsoleFunctions.SetConsoleTitle(text);
            return this;
        }

        static CONSOLE_SCREEN_BUFFER_INFO_EX GetScreenBufferInfoEx()
        {
            var hConsoleOutput = ConsoleFunctions.GetStdHandle(ConsoleFunctions.STD_OUTPUT_HANDLE);
            var csbe = new CONSOLE_SCREEN_BUFFER_INFO_EX
            {
                cbSize = (uint) Marshal.SizeOf(typeof (CONSOLE_SCREEN_BUFFER_INFO_EX))
            };
            if (ConsoleFunctions.GetConsoleScreenBufferInfoEx(hConsoleOutput, ref csbe))
            {
                return csbe;
            }
            throw new SystemException($"Call to API failed and set error code {ConsoleFunctions.GetLastError()}");
        }

        static void SetScreenBufferInfoEx(CONSOLE_SCREEN_BUFFER_INFO_EX csbe)
        {
            var hConsoleOutput = ConsoleFunctions.GetStdHandle(ConsoleFunctions.STD_OUTPUT_HANDLE);
            ++csbe.srWindow.Bottom;
            ++csbe.srWindow.Right;

            if (!ConsoleFunctions.SetConsoleScreenBufferInfoEx(hConsoleOutput, ref csbe))
            {
                Console.Out.WriteLine($"Call to API failed and set error code {Marshal.GetLastWin32Error()}");
            }
        }

    }
}