# qtest
qtest is a local testing utility for competitive programming.

qtest tests a given program with a batch of test cases and for each test case checks that
* the output is correct
* the program terminates within the time limit
* the program didn't crash

qtest doesn't check memory limits.

## Usage

qtest &lt;program&gt; &lt;test folder&gt; [checker program] [checker parameters]

Flags:
* -a: show all output
* -p &lt;executor program&gt;: execute tested program with another program, for example Python interpreter or Java virtual machine. Tester program is passed to executor as a command line argument
* -t &lt;time limit in ms&gt;: override time limit for this run
  
  
## Test folder structure

The test cases should be located in a folder in the same directory as the tested program. The name of the folder can be arbitrary and there can be multiple test folders.

Within a test folder there can be
* tests.txt
* limits.txt
* an arbitrary number of subfolders for single test cases

None of these are required.

### tests.txt

*tests.txt* is a file that can contain multiple test inputs and outputs. It is the recommended place for small test cases, since it is faster to add many tests to a single file during a contest than to create separate folders and files for each. A single test consists of a test input and expected output. The input starts with tag **_in** and input ends and expected output starts with tag **_out**.

An example *tests.txt* with two test cases:
```
_in
2
3 5
_out
8
_in
3
2 4 1
_out
7
```

### limits.txt

*limits.txt* is a file where the time limit for test cases is defined. The file should contain one line with a single integer, the time limit in milliseconds. If *limits.txt* does not exists, the time limit is by default 1000 milliseconds.

### Single test folders

The test folder can contain subfolders with one test case each. It is recommended to use these folders instead of *tests.txt* when the test cases are large.

The folder names don't matter and can be arbitrary. Each folder should contain two text files: *in* and *out*, the test input and output, respectively.
