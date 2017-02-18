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
         *      tests.txt
         *          (text file example)
         *          _in
         *          3 5
         *          _out
         *          8
         *          _in
         *          2 2
         *          _out
         *          4
         */
        const string INPUT_FILE = "in.txt";
        const string OUTPUT_FILE = "out.txt";
        const string LIMIT_FILE = "limits.txt";
        const string TEST_FILE = "tests.txt";

        const string INPUT_START = "_in";
        const string OUTPUT_START = "_out";
        
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
                if (limits.Length < 1)
                {
                    //Console.WriteLine("Invalid limits.txt, correct format:\n <time limit, ms>\n <memory limit, bytes>");
                    Console.WriteLine("Invalid limits.txt, correct format:\n <time limit, ms>");
                }

                timeLimit = int.Parse(limits[0]);
                //memoryLimit = long.Parse(limits[1]);
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

            List<string[]>[] tCases = GetTestCases();

            List<string[]> testInputs = tCases[0];
            List<string[]> testOutputs = tCases[1];

            //Console.WriteLine("Time limit: " + timeLimit + " ms, memory limit: " + (memoryLimit / 1000) + " kB."); // TODO: uncomment when memory bug is fixed
            Console.WriteLine("Time limit: " + timeLimit + " ms.");
            Console.WriteLine("Running " + testInputs.Count + " test(s)...");
            Console.WriteLine();

            for (int i = 0; i < testInputs.Count; ++i)
            {
                // run current test case
                Result curResult = Execute(testInputs[i], timeLimit, memoryLimit);

                // either TLE, MLE or RTE, no need to compare outputs
                if (curResult.result != Verdict.ExecutionOk)
                {
                    TestResults.Add(curResult);
                    continue;
                }
                curResult = Check(testOutputs[i], curResult);
                TestResults.Add(curResult);
            }

            PrintResults(timeLimit, memoryLimit);
        }

        /// <summary>
        /// Read test cases from tests.txt and test folders
        /// </summary>
        /// <returns></returns>
        static List<string[]>[] GetTestCases()
        {
            List<string[]> testInputs = new List<string[]>();
            List<string[]> testOutputs = new List<string[]>();

            // all lines from tests.txt
            string[] testFileCont = null;
            try
            {
                testFileCont = File.ReadAllLines(TestFolder + TEST_FILE);
            }
            catch (FileNotFoundException)
            {
            }

            if (testFileCont != null)
            {
                // read tests.txt
                bool isInput = false; // are we currently reading input or output
                List<string> curData = new List<string>();

                for (int i = 0; i <= testFileCont.Length; ++i) // intentional <= to register last test
                {
                    if (i == testFileCont.Length || testFileCont[i] == INPUT_START || testFileCont[i] == OUTPUT_START) // end of test case
                    {
                        if (curData.Count != 0)
                        {
                            if (isInput) testInputs.Add(curData.ToArray());
                            else testOutputs.Add(curData.ToArray());
                            curData.Clear();
                        }
                        if (i < testFileCont.Length)
                        {
                            bool nIsInput = testFileCont[i] == INPUT_START;
                            if (nIsInput == isInput)
                            {
                                Console.WriteLine("Multiple test inputs or outputs in a row");
                                Environment.Exit(1);
                            }
                            isInput = nIsInput;
                        }
                    }
                    else
                    {
                        curData.Add(testFileCont[i]);
                    }
                }
            }

            // get all test subfolders
            // each folder should contain in.txt and out.txt
            string[] testFolders = Directory.GetDirectories(TestFolder);

            for (int i = 0; i < testFolders.Length; ++i)
            {
                string currentFolder = testFolders[i] + "\\";
                string[] testInput = null;
                string[] testOutput = null;
                try
                {
                    testInput = File.ReadAllLines(currentFolder + INPUT_FILE);
                    testOutput = File.ReadAllLines(currentFolder + OUTPUT_FILE);
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("Test folder #" + (i + 1) + ": A test input or output is missing.");
                    Environment.Exit(1);
                }
                testInputs.Add(testInput);
                testOutputs.Add(testOutput);
            }

            // sanity checks
            if (testInputs.Count != testOutputs.Count)
            {
                Console.WriteLine("Number of test inputs and outputs doesn't match (" + testInputs.Count + " inputs, " + testOutputs.Count + " outputs)");
                Environment.Exit(1);
            }
            if (testInputs.Count == 0)
            {
                Console.WriteLine("No tests found");
                Environment.Exit(1);
            }

            List<string[]>[] res = new List<string[]>[2];
            res[0] = testInputs;
            res[1] = testOutputs;
            return res;
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

            List<string> output = new List<string>();
            testProc.OutputDataReceived += delegate(object sender, DataReceivedEventArgs ea)
            {
                output.Add(ea.Data);
            };
            testProc.BeginOutputReadLine();

            for (int i = 0; i < testInput.Length; ++i)
            {
                testProc.StandardInput.WriteLine(testInput[i]); // check: do testInput strings already end with line terminators?
            }

            bool tle;
            if ((timeLimitMillis - (int)runTime.ElapsedMilliseconds) <= 0) tle = true;
            else tle = !testProc.WaitForExit(timeLimitMillis - (int)runTime.ElapsedMilliseconds); // true if tle
            if (tle) testProc.Kill();
            runTime.Stop();

            testProc.WaitForExit(); // wait for kill

            // long usedMem = testProc.PeakWorkingSet64;
            long usedMem = 1; // TODO: find out how to get memory usage after process termination
            bool mle = usedMem > memLimitBytes;
            int exitCode = testProc.ExitCode;
            output.RemoveAt(output.Count - 1); // outputdatareceived adds an extra line to output, remove it
 
            Result res = new Result();
            if (tle)
            {
                res.result = Verdict.TimeLimitExceeded;
            }
            /*else if (mle)
            {
                res.result = Verdict.MemoryLimitExceeded;
            }*/
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

        /// <summary>
        /// Output formatted test results.
        /// </summary>
        /// <param name="timeLimit"></param>
        /// <param name="memoryLimit"></param>
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