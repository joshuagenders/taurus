import json
import time

from bzt.modules.dotnet import NUnitDotNetCoreExecutor
from bzt.utils import is_windows
from tests import RESOURCES_DIR
from tests.modules.selenium import SeleniumTestCase


RUNNER_EXECUTABLE = "runner-mock" + (".bat" if is_windows() else ".sh")


class TestNUnitDotNetCoreExecutor(SeleniumTestCase):
    def setup_mock(self):
        self.assertIsInstance(self.obj.runner, NUnitDotNetCoreExecutor)

        if self.obj.runner.dotnet:
            self.obj.runner.dotnet.tool_path = None

        self.obj.runner.runner_dir = RESOURCES_DIR + "nunit/bin/"
        self.obj.runner.runner_executable = RESOURCES_DIR + "nunit/bin/" + RUNNER_EXECUTABLE

    def test_startup(self):
        self.obj.execution.merge({
            "scenario": {
                "script": RESOURCES_DIR + "nunit/assemblies/TestSuite.dll"
            }
        })
        self.obj.prepare()
        self.setup_mock()
        self.obj.startup()
        while not self.obj.check():
            time.sleep(self.obj.engine.check_interval)
        self.obj.shutdown()
        self.obj.post_process()
        samples = [json.loads(line) for line in open(self.obj.runner.report_file).readlines()]
        statuses = [sample["status"] for sample in samples]
        self.assertEqual(statuses, ["FAILED", "FAILED", "PASSED", "SKIPPED"])