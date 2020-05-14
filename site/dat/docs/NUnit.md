# NUnit Executor

This executor allows to run tests written with NUnit framework. It uses dotnet core and has full support for NUnit 3.

Follow execution params are possible for NUnit Executor: `concurrency`, `iterations`, `hold-for` and `ramp-up`.

Usage:
```yaml
execution:
- executor: nunit
  concurrency: 2
  iterations: 5
  scenario:
    script: bin/Release/TestAssembly.dll  # assembly with tests
```

Taurus will run NUnit through a custom runner that will extract all tests from DLL assembly
and pass them to NUnit to run them.

Note that Taurus won't try to build your test suite solution. You should build it yourself,
either with VisualStudio or with command line tools. You can find the example of
test suite project with building instructions in Taurus's repo at
[Github](https://github.com/Blazemeter/taurus/tree/master/examples/selenium/nunit/).

Taurus requires the dotnet core SDK to be installed. 3.1 is the preferred
version.

Note: when running tests, you should have NUnit's DLL (nunit.framework.dll) placed in the same
directory as the DLL assembly with your tests. Furthermore, any DLLs referenced from your DLL should also be in that same directory. 
