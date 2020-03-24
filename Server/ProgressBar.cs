using System;
using System.Collections.Generic;
using System.Text;

namespace Server
{
    class Point
    {
        public int x { get; set; } = 0;
        public int y { get; set; } = 0;
        public Point()
        { }
        public Point(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }
    static class ConsoleWrapp
    {
        public static ProgressBar bar { set; get; }
        public static object obj = new object();
        static public void WriteLine(string str)
        {
            lock (obj)
            {
                Console.WriteLine(str);
                if (bar?.point.y == Console.CursorTop + 2)
                    bar.dynamicDraw();
            }
        }
        static public string ReadLine()
        {
            string res;
            res = Console.ReadLine();
            if (bar?.point.y == Console.CursorTop + 2)
                bar.dynamicDraw();
            return res;
        }
    }
    class ProgressBar
    {
        public Point point { get; } = new Point(Console.CursorLeft, Console.CursorTop);
        int total = 100;
        int length = 30;
        int progress = 0;
        object drawLock = new object();
        public ProgressBar()
        {
            ConsoleWrapp.bar = this;
        }
        public ProgressBar(int total, int length) : this()
        {
            this.total = total;
            this.length = length;
        }
        public ProgressBar(int total, int length, Point point) : this(total, length)
        {
            this.point = point;
        }
        public void staticDraw()
        {
            this.progress++;
            lock (drawLock)
                draw();
        }
        public void dynamicDraw()
        {
            clearLine(point.y);
            point.y++;
            lock (drawLock)
                draw();
        }
        private void draw()
        {
            int prev_x = Console.CursorLeft;
            int prev_y = Console.CursorTop;

            clearLine(point.y);
            Console.SetCursorPosition(point.x, point.y);
            Console.Write("[");
            Console.CursorLeft = this.length + 2;
            Console.Write("]");
            Console.CursorLeft = point.x;

            float onechunk = (float)this.length / this.total;
            int position = 1;
            for (int i = 0; i < onechunk * (progress <= total ? progress : total + 1); i++)
            {
                Console.BackgroundColor = ConsoleColor.Green;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }

            for (int i = position; i <= this.length + 1; i++)
            {
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }

            Console.CursorLeft = this.length + 5;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write(progress.ToString() + " of " + this.total.ToString() + "    ");
            Console.SetCursorPosition(prev_x, prev_y);
        }
        private void clearLine(int y)
        {
            int prev_x = Console.CursorLeft;
            int prev_y = Console.CursorTop;

            Console.SetCursorPosition(0, y);

            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write(" ");
            for (int i = 0; i <= length + 20; i++)
            {
                Console.BackgroundColor = ConsoleColor.Black;
                Console.Write(" ");
            }
            Console.SetCursorPosition(prev_x, prev_y);
        }
    }
}
