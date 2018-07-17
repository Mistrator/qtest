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
        static List<string> CheckerParameters;

        // flags
        const string TRUNCATE_FLAG = "-a";
        static bool TruncateOutput = true; // limit lines shown when printing test results

        const string EXECUTOR_FLAG = "-p";
        static string ProgramExecutor = String.Empty; // use another program to run tested program, has to be used with, for example, Python and Java

        const string TIME_LIMIT_FLAG = "-t";
        static int TimeLimitOverride = 0;

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
         *          _in
         *          1000000000000 1000000000000
         *          _out
         *          #any
         */
#if WINDOWS
        const string INPUT_FILE = "in.txt";
        const string OUTPUT_FILE = "out.txt";
        const string LIMIT_FILE = "limits.txt";
        const string TEST_FILE = "tests.txt";
        const string PATH_SEPARATOR = "\\";
#elif LINUX
        const string INPUT_FILE = "in";
        const string OUTPUT_FILE = "out";
        const string LIMIT_FILE = "limits";
        const string TEST_FILE = "tests";
        const string PATH_SEPARATOR = "/";
#endif

        const string INPUT_START = "_in";
        const string OUTPUT_START = "_out";

        const string ANY_ANSWER = "#any"; // if specified, user answer is always correct. Allows large test cases for which correct answers are not known

        const string COMMENT = "//";

        const int DEFAULT_TIME_LIMIT = 1000;
        
        /* Checker input format: 
         * <user token count> <correct token count> <parameter count>
         * <user tokens>
         * <correct tokens>
         * [parameters]
         */
        const int CHECKER_TIME_LIMIT = 500;
        const string CHECKER_ACCEPTED = "OK";
        const string CHECKER_WRONG_ANSWER = "WA";

        static List<Result> TestResults = new List<Result>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args">Program to test, test file folder, checker program</param>
        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: qtest <program> <test folder> [checker program] [checker parameters]");
                Console.WriteLine("Flags:"); 
                Console.WriteLine(TRUNCATE_FLAG + " = show all output");
                Console.WriteLine(EXECUTOR_FLAG + " <executor program> = execute tested program with another program (with Java virtual machine/Python interpreter, for example). Tested program is passed to executor as a command line argument");
                Console.WriteLine(TIME_LIMIT_FLAG + " <time limit (ms)> = override time limit for this test run");
                Error(String.Empty);
            }

            string[] cleanedArgs = ProcessFlags(args);

            QTestDirectory = AppDomain.CurrentDomain.BaseDirectory;
            LaunchDirectory = Environment.CurrentDirectory + PATH_SEPARATOR;
            TestedProgram = LaunchDirectory + cleanedArgs[0];
            TestFolder = LaunchDirectory + cleanedArgs[1] + PATH_SEPARATOR;
            CheckerProgram = String.Empty;
            if (cleanedArgs.Length >= 3) CheckerProgram = QTestDirectory + PATH_SEPARATOR + cleanedArgs[2];

            CheckerParameters = new List<string>();
            for (int i = 3; i < cleanedArgs.Length; ++i)
            {
                CheckerParameters.Add(cleanedArgs[i]);
            }

            int timeLimit = 0;
            long memoryLimit = 0;

            if (File.Exists(TestFolder + LIMIT_FILE))
            {
                try
                {
                    string[] limits = File.ReadAllLines(TestFolder + LIMIT_FILE);
                    if (limits.Length < 1)
                    {
                        //Console.WriteLine("Invalid limits.txt, correct format:\n <time limit, ms>\n <memory limit, bytes>");
                        Error("Invalid limits.txt, correct format:\n <time limit, ms>");
                    }

                    timeLimit = int.Parse(limits[0]);
                    //memoryLimit = long.Parse(limits[1]);
                }
                catch (DirectoryNotFoundException)
                {
                    Error("Test directory does not exist.");
                }
                catch (FormatException)
                {
                    Error("Invalid number format in limits.txt");
                }
            }
            else
            {
                timeLimit = DEFAULT_TIME_LIMIT;
            }

            if (TimeLimitOverride != 0)
            {
                timeLimit = TimeLimitOverride;
            }
            

            List<string[]>[] tCases = GetTestCases();

            List<string[]> testInputs = tCases[0];
            List<string[]> testOutputs = tCases[1];
            List<string[]> testComments = tCases[2];

            //Console.WriteLine("Time limit: " + timeLimit + " ms, memory limit: " + (memoryLimit / 1000) + " kB."); // TODO: uncomment when memory bug is fixed
            if (CheckerProgram != String.Empty)
            {
                Console.WriteLine("Checker: " + cleanedArgs[2]);
            }
            else
            {
                Console.WriteLine("Checker: Default comparer");
            }
            Console.WriteLine("Time limit: " + timeLimit + " ms");
            Console.WriteLine("Running " + testInputs.Count + " test(s)...");
            Console.WriteLine();

            for (int i = 0; i < testInputs.Count; ++i)
            {
                // run current test case
                Result curResult = RunTest(testInputs[i], timeLimit, memoryLimit);

                // either TLE, MLE or RTE, no need to do checking
                if (curResult.result != Verdict.ExecutionOk)
                {
                    TestResults.Add(curResult);
                    continue;
                }
                curResult = Check(testOutputs[i], curResult);
                TestResults.Add(curResult);
            }

            PrintResults(timeLimit, memoryLimit, testComments);
        }

        /// <summary>
        /// Print error message and terminate process
        /// </summary>
        /// <param name="message"></param>
        static void Error(string message)
        {
            Console.WriteLine(message);
            Environment.Exit(1);
        }

        /// <summary>
        /// Process and remove flags from command line parameters and return other parameters
        /// </summary>
        /// <param name="args"></param>
        /// <returns>Command line parameters without flags</returns>
        static string[] ProcessFlags(string[] args)
        {
            List<string> arguments = new List<string>();

            for (int i = 0; i < args.Length; ++i)
            {
                if (args[i] == TRUNCATE_FLAG)
                {
                    TruncateOutput = false;
                }
                else if (args[i] == EXECUTOR_FLAG)
                {
                    if (i == args.Length - 1)
                    {
                        Error("External executor flag is set, but flag parameter is missing");
                    }
                    i++;
                    ProgramExecutor = args[i];
                }
                else if (args[i] == TIME_LIMIT_FLAG)
                {
                    if (i == args.Length - 1)
                    {
                        Error("Time limit flag is set, but flag parameter is missing");
                    }
                    i++;
                    if (!int.TryParse(args[i], out TimeLimitOverride))
                    {
                        Error("Specified time limit is invalid");
                    }
                }
                else
                {
                    arguments.Add(args[i]);
                }
            }
            return arguments.ToArray();
        }

        /// <summary>
        /// Read test cases from tests.txt and test folders
        /// </summary>
        /// <returns></returns>
        static List<string[]>[] GetTestCases()
        {
            List<string[]> testInputs = new List<string[]>();
            List<string[]> testOutputs = new List<string[]>();
            List<string[]> testComments = new List<string[]>();

            if (!Directory.Exists(TestFolder))
            {
                Error("Test folder doesn't exist");
            }

            // all lines from tests.txt
            string[] testFileCont = null;

            // doesn't have to exist, it's allowed to have all tests in separate files
            if (File.Exists(TestFolder + TEST_FILE))
            {
                testFileCont = File.ReadAllLines(TestFolder + TEST_FILE);
            }

            if (testFileCont != null)
            {
                // read tests.txt
                bool isInput = false; // are we currently reading input or output
                List<string> curData = new List<string>();
                List<string> curComment = new List<string>(); // only a single line, but implemented like this to return the comments with test data

                for (int i = 0; i <= testFileCont.Length; ++i) // intentional <= to register last test
                {
                    bool inStart = (i < testFileCont.Length && testFileCont[i].Length >= 3 && testFileCont[i].Substring(0, 3) == INPUT_START);
                    bool outStart = (i < testFileCont.Length && testFileCont[i].Length >= 4 && testFileCont[i].Substring(0, 4) == OUTPUT_START);
                    if (i == testFileCont.Length || inStart || outStart) // end of test case
                    {
                        if (curData.Count != 0)
                        {
                            if (isInput)
                            {
                                testInputs.Add(curData.ToArray());
                                if (curComment.Count == 0)
                                {
                                    curComment.Add(String.Empty);
                                }
                                testComments.Add(curComment.ToArray());
                            }
                            else testOutputs.Add(curData.ToArray());
                            curData.Clear();
                            curComment.Clear();
                        }
                        if (i < testFileCont.Length)
                        {
                            bool nIsInput = inStart;
                            if (nIsInput == isInput)
                            {
                                Error("Multiple test inputs or outputs in a row");
                            }
                            isInput = nIsInput;
                        }
                        if (inStart && testFileCont[i].Length > 3)
                        {
                            curComment.Add(testFileCont[i].Substring(3, testFileCont[i].Length - 3).Trim());
                        }
                    }
                    else
                    {
                        // skip comment lines
                        if (!(testFileCont[i].Length >= 2 && testFileCont[i].Substring(0, 2) == COMMENT))
                        {
                            curData.Add(testFileCont[i]);
                        }
                    }
                }
            }

            // get all test subfolders
            // each folder should contain in.txt and out.txt
            string[] testFolders = Directory.GetDirectories(TestFolder);

            for (int i = 0; i < testFolders.Length; ++i)
            {
                string currentFolder = testFolders[i] + PATH_SEPARATOR;
                string[] testInput = null;
                string[] testOutput = null;
                // add empty string so that input, output and comment counts match
                string[] testComment = { String.Empty }; // TODO: support comments in separate test files
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
                testComments.Add(testComment);
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

            List<string[]>[] res = new List<string[]>[3];
            res[0] = testInputs;
            res[1] = testOutputs;
            res[2] = testComments;
            return res;
        }

        /// <summary>
        /// Run external program with specified input and capture its output.
        /// Does not handle exceptions that are raised on execution.
        /// </summary>
        /// <param name="input">Data fed to standard input</param>
        /// <param name="timeLimitMillis">Maximum allowed runtime</param>
        /// <returns>Output, exit code, runtime in milliseconds</returns>
        static Result RunExternalProgram(string program, string[] input, int timeLimitMillis)
        {
            ProcessStartInfo stInfo = new ProcessStartInfo();
            stInfo.UseShellExecute = false;
            stInfo.RedirectStandardInput = true;
            stInfo.RedirectStandardOutput = true;

            // pass tested program to another program as a command line argument
            if (ProgramExecutor != String.Empty)
            {
                stInfo.FileName = ProgramExecutor;
                stInfo.Arguments = program;
            }
            else
            {
                stInfo.FileName = program;
            }

            Process testProc = new Process();
            testProc.StartInfo = stInfo;

            Stopwatch runTime = new Stopwatch();

            testProc.Start();
            runTime.Start();

            List<string> output = new List<string>();
            testProc.OutputDataReceived += delegate(object sender, DataReceivedEventArgs ea)
            {
                output.Add(ea.Data);
            };
            testProc.BeginOutputReadLine();

            for (int i = 0; i < input.Length; ++i)
            {
                testProc.StandardInput.WriteLine(input[i]);
            }

            bool tle;
            if ((timeLimitMillis - (int)runTime.ElapsedMilliseconds) <= 0) tle = true; // check if limit is already exceeded
            else tle = !testProc.WaitForExit(timeLimitMillis - (int)runTime.ElapsedMilliseconds); // true if tle
            runTime.Stop();

            if (tle) testProc.Kill();

            testProc.WaitForExit(); // wait for kill

            Result res = new Result();
            res.exitCode = testProc.ExitCode;
            res.timeMillis = (int)runTime.ElapsedMilliseconds;

            output.RemoveAt(output.Count - 1); // outputdatareceived adds an extra line to output, remove it
            res.output = output.ToArray();

            if (tle)
            {
                res.result = Verdict.TimeLimitExceeded;
            }
            else if (res.exitCode != 0)
            {
                res.result = Verdict.RuntimeError;
            }
            else
            {
                res.result = Verdict.ExecutionOk;
            }

            testProc.Close();
            return res;
        }

        /// <summary>
        /// Does a test run with given test input and reports results.
        /// </summary>
        /// <param name="testInput">Input file contents, are fed to program's standard input</param>
        /// <param name="timeLimitMillis">Time limit for execution</param>
        /// <param name="memLimitBytes">Memory limit for execution</param>
        /// <returns></returns>
        static Result RunTest(string[] testInput, int timeLimitMillis, long memLimitBytes)
        {
            Result runResult = null;
            try
            {
                runResult = RunExternalProgram(TestedProgram, testInput, timeLimitMillis);
            }
            catch (Win32Exception)
            {
                Console.WriteLine("The specified program is invalid and can not be executed.");
                Environment.Exit(1);
            }
            return runResult;
        }

        /// <summary>
        /// Checks if the user output was correct.
        /// </summary>
        /// <param name="correctOutput">Correct output for current test case</param>
        /// <param name="runResult">Execution results</param>
        /// <returns></returns>
        static Result Check(string[] correctOutput, Result runResult)
        {
            if (correctOutput.Length != 0 && runResult.output.Length != 0 && correctOutput[0] == ANY_ANSWER)
            {
                runResult.result = Verdict.TimeLimitPassed;
                return runResult;
            }

            // Use external checker
            if (CheckerProgram != "")
            {
                return CheckExternal(correctOutput, runResult);
            }

            // Otherwise compare outputs

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
        /// Check answer using specified external checker.
        /// </summary>
        /// <param name="correctOutput"></param>
        /// <param name="runResult"></param>
        /// <returns></returns>
        static Result CheckExternal(string[] correctOutput, Result runResult)
        {
            Result checkRunResult = null;

            try
            {
                List<string> checkerInput = new List<string>();
                checkerInput.Add(runResult.output.Length.ToString());
                checkerInput.Add(correctOutput.Length.ToString());
                checkerInput.Add(CheckerParameters.Count.ToString());

                checkerInput.AddRange(runResult.output);
                checkerInput.AddRange(correctOutput);
                if (CheckerParameters.Count != 0)
                {
                    checkerInput.AddRange(CheckerParameters);
                }
                checkRunResult = RunExternalProgram(CheckerProgram, checkerInput.ToArray(), CHECKER_TIME_LIMIT);
            }
            catch (Win32Exception)
            {
                Console.WriteLine("Checker could not be executed.");
                Environment.Exit(1);
            }

            if (checkRunResult.result == Verdict.TimeLimitExceeded)
            {
                runResult.output = new string[1] { "Checker timed out." };
                runResult.result = Verdict.CheckerError;
            }
            if (checkRunResult.result == Verdict.RuntimeError)
            {
                runResult.output = new string[1] { "Checker exited with non-zero exit code." };
                runResult.result = Verdict.CheckerError;
            }
            if (checkRunResult.output.Length == 0)
            {
                runResult.output = new string[1] { "Checker returned nothing." };
                runResult.result = Verdict.CheckerError;
            }
            else if (checkRunResult.output[0] == CHECKER_WRONG_ANSWER)
            {
                runResult.result = Verdict.WrongAnswer;
            }
            else if (checkRunResult.output[0] == CHECKER_ACCEPTED)
            {
                runResult.result = Verdict.Accepted;
            }
            else
            {
                runResult.output = new string[2] { "Unexpected verdict from checker: ", checkRunResult.output[0] };
                runResult.result = Verdict.CheckerError;
            }
            return runResult;
        }

        /// <summary>
        /// Output formatted test results.
        /// </summary>
        /// <param name="timeLimit"></param>
        /// <param name="memoryLimit"></param>
        static void PrintResults(int timeLimit, long memoryLimit, List<string[]> testComments)
        {
            bool accepted = true;
            for (int i = 0; i < TestResults.Count; ++i)
            {
                if (TestResults[i].result != Verdict.Accepted && TestResults[i].result != Verdict.TimeLimitPassed) accepted = false;
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
                if (TestResults[i].result == Verdict.Accepted || TestResults[i].result == Verdict.TimeLimitPassed) Console.ForegroundColor = ConsoleColor.Green;
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
                    case Verdict.TimeLimitPassed:
                        Console.Write("Time limit passed");
                        break;
                    case Verdict.CheckerError:
                        Console.Write("Checker error");
                        break;
                    default:
                        break;
                }
                Console.ResetColor();
                Console.Write(" (");
                if (TestResults[i].result == Verdict.TimeLimitExceeded || TestResults[i].result == Verdict.RuntimeError) Console.Write("--");
                else Console.Write(TestResults[i].timeMillis + " ms");
                Console.Write(" / ");
                Console.Write(timeLimit + " ms");
                Console.Write(")");
                /*Console.Write(", ");
                Console.Write((TestResults[i].memoryBytes / 1000) + " kB used"); // TODO: uncomment when memory bug is fixed
                Console.Write(")");*/

                if (testComments[i][0] != String.Empty)
                {
                    Console.Write(" (");
                    Console.Write(testComments[i][0]);
                    Console.Write(")");
                }

                Console.WriteLine();

                // how much of the output do we print
                int MAX_LINES = TruncateOutput ? 5 : int.MaxValue; // 5
                int MAX_CHARS = TruncateOutput ? 64 : int.MaxValue; // 64

                if (TestResults[i].result == Verdict.WrongAnswer)
                {
                    if (TestResults[i].output.Length > 0)
                    {
                        Console.WriteLine("Incorrect output: ");
                        PrintOutput(TestResults[i].output, MAX_LINES, MAX_CHARS);
                    }
                    else
                    {
                        PrintOutput(new string[1] {"(no output)"}, MAX_LINES, MAX_CHARS);
                    }
                }   

                if (TestResults[i].result == Verdict.TimeLimitPassed)
                {
                    Console.WriteLine("Test output: ");
                    PrintOutput(TestResults[i].output, MAX_LINES, MAX_CHARS);
                }

                if (TestResults[i].result == Verdict.CheckerError)
                {
                    PrintOutput(TestResults[i].output, MAX_LINES, MAX_CHARS);
                }

                if (TestResults[i].result == Verdict.RuntimeError)
                {
                    Console.Write("Exit code: ");
#if WINDOWS
                    PrintOutput(new string[1] { TestResults[i].exitCode.ToString() }, MAX_LINES, MAX_CHARS);
#elif LINUX
                    List<string> t = new List<string>();
                    t.Add(TestResults[i].exitCode.ToString());
                    switch (TestResults[i].exitCode)
                    {
                        case 134:
                            t[0] += " (aborted)";
                            t.Add("[SIGIOT: signed integer overflow]");
                            break;
                        case 136:
                            t[0] += " (floating point exception)";
                            t.Add("[SIGFPE: division by zero]");
                            break;
                        case 139:
                            t[0] += " (segmentation fault)";
                            t.Add("[SIGSEGV: invalid memory access or stack overflow]");
                            break;
                    }
                    PrintOutput(t.ToArray(), MAX_LINES, MAX_CHARS);
#endif
                }
            }
        }

        /// <summary>
        /// Print a part of the output.
        /// </summary>
        /// <param name="output">Test output</param>
        /// <param name="maxLines">Max lines printed</param>
        /// <param name="maxChars">Max chars on a single line</param>
        static void PrintOutput(string[] output, int maxLines, int maxChars)
        {
            for (int j = 0; j < Math.Min(output.Length, maxLines); ++j)
            {
                if (output[j].Length > maxChars)
                {
                    Console.WriteLine(output[j].Substring(0, maxChars) + " (+ " + (output[j].Length - maxChars) + " chars)");
                }
                else
                {
                    Console.WriteLine(output[j]);
                }
            }
            if (output.Length > maxLines)
            {
                Console.WriteLine("(+ " + (output.Length - maxLines) + " lines)");
            }
        }
    }
}