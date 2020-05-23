# NUnit .NET Core Executor

This executor allows to run tests written with NUnit framework. It uses .NET core and has full support for NUnit 3.

Follow execution params are possible for NUnit Executor: `concurrency`, `iterations`, `hold-for`, `throughput` and `ramp-up`.

Usage:
```yaml
execution:
- executor: nunit-dotnet
  ramp-up: 2m
  concurrency: 10
  iterations: 500
  throughput: 20
  scenario:
    script: bin/Release/TestAssembly.dll
    testname: BrowserExample
```

Two threads executing two tests per second (1 each thread).
```yaml
execution:
- executor: nunit-dotnet
  concurrency: 2
  throughput: 0.5
  scenario:
    script: bin/Release/TestAssembly.dll  # assembly with tests
```

Taurus will run NUnit through a custom runner that will extract all tests from DLL assembly
and pass them to NUnit to run them.

Note that Taurus won't try to build your test suite solution. You should build it yourself,
either with Visual Studio or with command line tools. You can find the example of
test suite project with building instructions in Taurus's repo at
[Github](https://github.com/Blazemeter/taurus/tree/master/examples/selenium/nunit-dotnetcore/).

Taurus requires the [.NET Core SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1) to be installed. 3.1 is the preferred
version.

Note that `ramp-up` applies to both concurrency and throughput at the same time.
E.g.
```yaml
ramp-up: 1
concurrency: 6
throughput: 3
```

Will result in a new thread every 10 seconds, and the throughput will increase from 0 RPS to 3 RPS over the 1 minute ramp-up time.
