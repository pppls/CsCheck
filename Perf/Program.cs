﻿using System.Linq;
using Tests;

namespace Perf
{
    class Program
    {
        static void Main()
        {
            for (int i = 0; i < 10; i++)
            {
                var output = new FakeOutputHelper();
                RunTests(new ArraySerializerTests(output));
                RunTests(new CheckTests(output));
                RunTests(new GenTests());
                RunTests(new HashTests(output));
                RunTests(new StreamSerializerTests());
                RunTests(new ModelTests());
                RunTests(new PCGTests());
                RunTests(new UtilsTests());
                RunTests(new ThreadStatsTests());
            }
        }

        private static void RunTests(object testClass)
        {
            var tests = testClass.GetType().GetMethods().Where(i =>
                                i.IsPublic
                            && i.ReturnType == typeof(void)
                            && i.GetParameters().Length == 0
            ).ToArray();
            foreach (var test in tests)
            {
                test.Invoke(testClass, null);
            }
        }
    }

    class FakeOutputHelper : Xunit.Abstractions.ITestOutputHelper
    {
        public void WriteLine(string message)
        {
        }

        public void WriteLine(string format, params object[] args)
        {
        }
    }
}
