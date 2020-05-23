#!/bin/bash

cat <<EOF > $TAURUS_ARTIFACTS_DIR/NUnitDotNetExecutor.ldjson
{"start_time":1501590114,"duration":0.011648,"test_case":"ErroringTest","test_suite":"TestSuite.BrowserTests","status":"FAILED","error_msg":"System.Exception : Ima error","error_trace":"at TestSuite.BrowserTests.ErroringTest () <0x401e3ee0 + 0x0002f> in <filename unknown>:0 \n  at System.Reflection.Method:InternalInvoke (System.Reflection.Method,object,object[],System.Exception&)\n  at System.Reflection.Method.Invoke (System.Object obj, BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) <0x401bfe00 + 0x000b7> in <filename unknown>:0","extras":null}
{"start_time":1501590114,"duration":0.020751,"test_case":"FailingTest","test_suite":"TestSuite.BrowserTests","status":"FAILED","error_msg":"Expected: 6\n  But was:  8","error_trace":"at TestSuite.BrowserTests.FailingTest () <0x401ee190 + 0x0005b> in <filename unknown>:0","extras":null}
{"start_time":1501590114,"duration":0.000577,"test_case":"PassingTest","test_suite":"TestSuite.BrowserTests","status":"PASSED","error_msg":"","error_trace":"","extras":null}
{"start_time":1501590114,"duration":0.000627,"test_case":"SkippedTest","test_suite":"TestSuite.BrowserTests","status":"SKIPPED","error_msg":"Skippy","error_trace":"","extras":null}
EOF

