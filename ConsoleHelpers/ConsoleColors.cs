using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ConsoleClassLibrary;

namespace ConsoleHelpers
{
    public abstract class ConsoleColorsBase : IDisposable
    {
        public abstract ConsoleColorsBase Swap();

        public abstract void Dispose();
        public abstract ConsoleColorsBase InferColorFromText(string text);
        public abstract ConsoleColorsBase SetConsoleTitle(string text);
        public abstract ConsoleColorsBase SetColorRgb(int r, int g, int b);
    }

    public class ConsoleNoColors : ConsoleColorsBase
    {
        public override ConsoleColorsBase Swap() => this;
        public override void Dispose(){}
        public override ConsoleColorsBase InferColorFromText(string text)  => this;
        public override ConsoleColorsBase SetConsoleTitle(string text)     => this;
        public override ConsoleColorsBase SetColorRgb(int r, int g, int b) => this;
    }

    public class ConsoleColors : ConsoleColorsBase
    {
        readonly ConsoleColor _foregroundColor;
        readonly ConsoleColor _backgroundColor;
        readonly CONSOLE_SCREEN_BUFFER_INFO_EX _consoleScreenBufferInfoEx;

        public ConsoleColors()
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
        {
            var screenInfo = GetScreenBufferInfoEx();
            var fgIndex = GetColorIndex(Console.ForegroundColor);
            var bgIndex = GetColorIndex(Console.BackgroundColor);
            screenInfo.ColorTable[bgIndex].SetColor(Color.FromArgb(r, g, b));
            var f = (r + g + b) / 3 >= 128 ? 0 : 255;
            screenInfo.ColorTable[fgIndex].SetColor(Color.FromArgb(f, f, f));
            SetScreenBufferInfoEx(screenInfo);
            Console.ResetColor();
            return Swap().Swap();
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