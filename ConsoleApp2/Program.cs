using System;
using System.Linq;

namespace ConsoleApp2
{
    class Program
    {

        static void Main(string[] args)
        {
            Random random = new Random();
            int number = 100;
            string[] testStringArr = new string[number];
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            for (int i = 0; i < number; ++i)
            {
                var curStr = new string(Enumerable.Repeat(chars, 10)
                .Select(s => s[random.Next(s.Length)]).ToArray());
                 testStringArr[i] = curStr;
            }

            for (int i = 0; i < testStringArr.Length; ++i)
            {
                Console.WriteLine(testStringArr[i]);
            }
        }
    }
}
