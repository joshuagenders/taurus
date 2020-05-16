# NUnit .NET Core Executor

This executor allows to run tests written with NUnit framework. It uses .NET core and has full support for NUnit 3.

Follow execution params are possible for NUnit Executor: `concurrency`, `iterations`, `hold-for`, `throughput` and `ramp-up`.

Usage:
```yaml
execution:
- executor: nunit-dotnet
  ramp-up: 120
  concurrency: 10
  iterations: 5
  scenario:
    script: bin/Release/TestAssembly.dll  # assembly with tests
```


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

TODO

Test cases
===========
unit test project + example tests:
variable scopes across threads - counter, static, concurrentdictionary, constructor readonly
dependency injection - http client
failures, exceptions
use a http interceptor

system tests run from python

Implementation
===========
options are:
ramp up
concurrency = max number of threads
iterations = max total executions
throughput = RPS target
hold for = length of test


control thread execution using semaphores
thread marshal maintains a semaphore for thread creation and another for task execution

when the application starts the ramp-up period interval is calculated
interval = rampup seconds / concurrency
loop, wait interval 

the application then releases threads at desired rate
each thread takes a task execution token, executes and returns the token once completed.
if throughput is specified then tokens are not released by threads, and instead are created by a worker to support an open workload.
when concurrency is specified without throughput, then the task execution semaphore is not used 
 - threads can execute as fast as they please and a constant number of threads are maintained.
when throughput is specified, concurrency is treated as a maximum thread count.
 - threads are released as normal, but must await a token from the task execution semaphore.
 - a worker thread releases tokens into the semaphore at the desired RPS
 - the rate at which the worker thread checks if it should release tokens depends on the rps
   - the worker calculates tokens per interval - this should always be >= 1
   - if rps is 0.1 (once every 10 seconds), then the interval is set to 10 seconds and tokens per interval = 1
   - the minimum interval is 1 second.
   if rps < 1 then interval = 1/rps 
   tokens per interval = 1
   if rps >= 1 then interval = 1
   tokens per interval = rps
   remainder point values are carried to the next interval
   the worker should not add more tokens to the task execution queue than there are threade so it doesn't try to account for lost time - those tokens get forfeited. e.g. test starts, during ramp up period worker releases thousands of tokens, once threads kick in, then desired rps is exceeded because of earlier unused tokens.
if iterations is specified and reached then the test exits disregarding hold-for.
if hold for is reached then the test exits disregarding iterations if specified. 
 
 
 
 