using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace ConsoleApp4
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<HashSetDictionaryBenchmark>();
        }
    }

    [MemoryDiagnoser]
    public class HashSetDictionaryBenchmark
    {
        private static Random random = new Random();
        private static string[] randomStringArray = RandomStringGenerator(100000);
        private readonly int halfpoint = 100000 / 2;
        private HashSet<string> testSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, bool> testDict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        [GlobalSetup]
        public void Setup()
        {
            for (int i = 0; i < this.halfpoint; ++i)
            {
                testSet.Add(randomStringArray[i]);
            }

            for (int i = 0; i < this.halfpoint; ++i)
            {
                testDict[randomStringArray[i]] = true;
            }

        }

        [Benchmark]
        public void HashSet()
        {
            for (int i = this.halfpoint; i < 100000; ++i)
            {
                testSet.Contains(randomStringArray[i]);
            }
        }

        [Benchmark]
        public void Dictionary()
        {
            for (int i = this.halfpoint; i < 100000; ++i)
            {
                testDict.ContainsKey(randomStringArray[i]);
            }
        }

        private static string[] RandomStringGenerator(int number)
        {
            string[] testStringArr = new string[number];
            const string chars = "ABCDEFGHIJKLMnopqrstuvwxyz";
            for (int i = 0; i < number; ++i)
            {
                var curStr = new string(Enumerable.Repeat(chars, 3)
                .Select(s => s[random.Next(s.Length)]).ToArray());
                testStringArr[i] = curStr;
            }

            return testStringArr;
        }
    }

}
