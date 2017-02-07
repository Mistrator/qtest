using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;

namespace qtest
{
    public class QTest
    {
        /// <summary>
        /// Directory of QTest executable.
        /// </summary>
        static string QTestDirectory;

        /// <summary>
        /// Directory of currently tested program.
        /// </summary>
        static string LaunchDirectory;
        static string TestedProgram;
        static string TestFolder;
        static string CheckerProgram;


        /*  Example folder structure:
         *  program.exe
         *  tests
         *      limits.txt
         *      example1
         *          in.txt
         *          out.txt
         *      example2
         *          in.txt
         *          out.txt
         */
        const string INPUT_FILE = "in.txt";
        const string OUTPUT_FILE = "out.txt";
        const string LIMIT_FILE = "limits.txt";
        
        const string DEFAULT_CHECKER = "checker.exe";

        static List<Result> TestResults = new List<Result>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args">Program to test, test file folder, checker program</param>
        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: qtest <program> <test folder>"); // TODO: add checker program to list when implemented
                return;
            }

            QTestDirectory = AppDomain.CurrentDomain.BaseDirectory;
            LaunchDirectory = Environment.CurrentDirectory + "\\";
            TestedProgram = LaunchDirectory + args[0];
            TestFolder = LaunchDirectory + args[1] + "\\";
            CheckerProgram = QTestDirectory + DEFAULT_CHECKER;
            if (args.Length >= 3) CheckerProgram = args[2];

            int timeLimit = 0;
            long memoryLimit = 0;

            try
            {
                string[] limits = File.ReadAllLines(TestFolder + LIMIT_FILE);
                if (limits.Length != 2)
                {
                    Console.WriteLine("Invalid limits.txt, correct format:\n <time limit, ms>\n <memory limit, bytes>");
                }

                timeLimit = int.Parse(limits[0]);
                memoryLimit = long.Parse(limits[1]);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("limits.txt is missing");
                return;
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid number format in limits.txt");
                return;
            }

            // get all test subfolders
            // each folder should contain in.txt and out.txt
            string[] testFolders = Directory.GetDirectories(TestFolder);

            if (testFolders.Length == 0)
            {
                Console.WriteLine("No tests found");
                return;
            }

            //Console.WriteLine("Time limit: " + timeLimit + " ms, memory limit: " + (memoryLimit / 1000) + " kB."); // TODO: uncomment when memory bug is fixed
            Console.WriteLine("Time limit: " + timeLimit + " ms.");
            Console.WriteLine("Running " + testFolders.Length + " test(s)...");
            Console.WriteLine();

            for (int i = 0; i < testFolders.Length; ++i)
            {
                string currentFolder = testFolders[i] + "\\";
                string[] testInput;
                string[] testOutput;
                try
                {
                    testInput = File.ReadAllLines(currentFolder + INPUT_FILE);
                    testOutput = File.ReadAllLines(currentFolder + OUTPUT_FILE);
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("Test #" + (i+1) + ": A test input or output is missing.");
                    return;
                }

                // run current test case
                Result curResult = Execute(testInput, timeLimit, memoryLimit);

                // either TLE, MLE or RTE, no need to compare outputs
                if (curResult.result != Verdict.ExecutionOk)
                {
                    TestResults.Add(curResult);
                    continue;
                }

                curResult = Check(testOutput, curResult);
                TestResults.Add(curResult);
            }

            PrintResults(timeLimit, memoryLimit);
        }

        /// <summary>
        /// Does a test run with given test input and reports results.
        /// </summary>
        /// <param name="testInput">Input file contents, are fed to program's standard input</param>
        /// <param name="timeLimitMillis">Time limit for execution</param>
        /// <param name="memLimitBytes">Memory limit for execution</param>
        /// <returns></returns>
        static Result Execute(string[] testInput, int timeLimitMillis, long memLimitBytes)
        {
            ProcessStartInfo stInfo = new ProcessStartInfo();
            stInfo.UseShellExecute = false;
            stInfo.RedirectStandardInput = true;
            stInfo.RedirectStandardOutput = true;
            stInfo.FileName = TestedProgram;

            Process testProc = new Process();
            testProc.StartInfo = stInfo;

            Stopwatch runTime = new Stopwatch();

            try
            {
                testProc.Start();
            }
            catch (Win32Exception)
            {
                Console.WriteLine("The specified program is invalid and can not be executed.");
                Environment.Exit(1);
            }
            runTime.Start();

            for (int i = 0; i < testInput.Length; ++i)
            {
                testProc.StandardInput.WriteLine(testInput[i]); // check: do testInput strings already end with line terminators?
            }

            bool tle = !testProc.WaitForExit(timeLimitMillis - (int)runTime.ElapsedMilliseconds); // true if tle
            if (tle) testProc.Kill();
            runTime.Stop();
            testProc.WaitForExit(); // wait for kill

            // long usedMem = testProc.PeakWorkingSet64;
            long usedMem = 1; // TODO: find out how to get memory usage after process termination
            bool mle = usedMem > memLimitBytes;
            int exitCode = testProc.ExitCode;

            List<string> output = new List<string>();

            if (!tle)
            {
                while (!testProc.StandardOutput.EndOfStream)
                {
                    output.Add(testProc.StandardOutput.ReadLine());
                }
            }

            Result res = new Result();
            if (tle)
            {
                res.result = Verdict.TimeLimitExceeded;
            }
            else if (mle)
            {
                res.result = Verdict.MemoryLimitExceeded;
            }
            else if (exitCode != 0)
            {
                res.result = Verdict.RuntimeError;
            }
            else
            {
                res.result = Verdict.ExecutionOk;
            }
            res.timeMillis = (int)runTime.ElapsedMilliseconds;
            res.memoryBytes = usedMem;
            res.output = output.ToArray();
            return res;
        }

        /// <summary>
        /// Checks if the user output was correct.
        /// TODO: Implement support for external checker programs
        /// </summary>
        /// <param name="correctOutput">Correct output for current test case</param>
        /// <param name="runResult">Execution results</param>
        /// <returns></returns>
        static Result Check(string[] correctOutput, Result runResult)
        {
            if (correctOutput.Length != runResult.output.Length)
            {
                runResult.result = Verdict.WrongAnswer;
                return runResult;
            }

            for (int i = 0; i < correctOutput.Length; ++i)
            {
                if ((correctOutput[i].Trim()) != (runResult.output[i].Trim()))
                {
                    runResult.result = Verdict.WrongAnswer;
                    return runResult;
                }
            }
            runResult.result = Verdict.Accepted;
            return runResult;
        }

        static void PrintResults(int timeLimit, long memoryLimit)
        {
            bool accepted = true;
            for (int i = 0; i < TestResults.Count; ++i)
            {
                if (TestResults[i].result != Verdict.Accepted) accepted = false;
            }

            Console.Write("Verdict: ");
            if (accepted)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Accepted");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed");
            }
            Console.ResetColor();
            Console.WriteLine();
            

            for (int i = 0; i < TestResults.Count; ++i)
            {
                Console.ResetColor();
                Console.Write("Test #" + (i + 1) + ": ");
                if (TestResults[i].result == Verdict.Accepted) Console.ForegroundColor = ConsoleColor.Green;
                else Console.ForegroundColor = ConsoleColor.Red;
                switch (TestResults[i].result)
                {
                    case Verdict.Accepted:
                        Console.Write("Accepted");
                        break;
                    case Verdict.WrongAnswer:
                        Console.Write("Wrong answer");
                        break;
                    case Verdict.TimeLimitExceeded:
                        Console.Write("Time limit exceeded");
                        break;
                    case Verdict.MemoryLimitExceeded:
                        Console.Write("Memory limit exceeded");
                        break;
                    case Verdict.RuntimeError:
                        Console.Write("Runtime error");
                        break;
                    default:
                        break;
                }
                Console.ResetColor();
                Console.Write(" (");
                if (TestResults[i].result == Verdict.TimeLimitExceeded) Console.Write("--");
                else Console.Write(TestResults[i].timeMillis + " ms");
                Console.Write(" / ");
                Console.Write(timeLimit + " ms");
                Console.Write(")");
                /*Console.Write(", ");
                Console.Write((TestResults[i].memoryBytes / 1000) + " kB used"); // TODO: uncomment when memory bug is fixed
                Console.Write(")");*/
                Console.WriteLine();
                if (TestResults[i].result == Verdict.WrongAnswer)
                {
                    Console.WriteLine("Incorrect output: ");
                    for (int j = 0; j < TestResults[i].output.Length; ++j)
                    {
                        Console.WriteLine(TestResults[i].output[j]);
                    }
                }
            }
        }
    }
}