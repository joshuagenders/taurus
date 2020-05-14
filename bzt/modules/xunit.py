"""
Copyright 2017 BlazeMeter Inc.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

   http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
"""
import os

from bzt import TaurusConfigError
from bzt.modules import SubprocessedExecutor
from bzt.engine import HavingInstallableTools
from bzt.utils import get_full_path, is_windows, RequiredTool, RESOURCES_DIR, CALL_PROBLEMS


class XUnitExecutor(SubprocessedExecutor, HavingInstallableTools):
    def __init__(self):
        super(XUnitExecutor, self).__init__()
        self.runner_dir = os.path.join(RESOURCES_DIR, "XUnitRunner")
        self.runner_executable = os.path.join(self.runner_dir, "XUnitRunner.exe")
        self.dotnetcore = None

    def install_required_tools(self):
        if is_windows():
            return

        self.dotnetcore = self._get_tool(DotNetCore)
        self.log.debug("Checking for dotnetcore")
        if not self.dotnetcore.check_if_installed():
            self.dotnetcore.install()

    def prepare(self):
        super(XUnitExecutor, self).prepare()
        self.script = get_full_path(self.get_script_path())
        if not self.script:
            raise TaurusConfigError("Script not passed to runner %s" % self)

        self.install_required_tools()
        self.reporting_setup(suffix=".ldjson")

    def startup(self):
        cmdline = []
        if not is_windows():
            if self.dotnetcore.tool_path:
                cmdline.append(self.dotnetcore.tool_path)

        cmdline += [self.runner_executable,
                    "--target", self.script,
                    "--report-file", self.report_file]

        load = self.get_load()
        if load.iterations:
            cmdline += ['--iterations', str(load.iterations)]
        if load.hold:
            cmdline += ['--duration', str(int(load.hold))]
        if load.concurrency:
            cmdline += ['--concurrency', str(int(load.concurrency))]
        if load.ramp_up:
            cmdline += ['--ramp_up', str(int(load.ramp_up))]
        if not is_windows(): # required?
            self.env.add_path({"DOTNETCORE_PATH": self.runner_dir})

        self.process = self._execute(cmdline)


class DotNetCore(RequiredTool):
    def __init__(self, **kwargs):
        super(DotNetCore, self).__init__(tool_path="dotnetcore", installable=False, **kwargs)

    def check_if_installed(self):
        self.log.debug('Trying %s: %s', self.tool_name, self.tool_path)
        try:
            out, err = self.call([self.tool_path, '--version'])
        except CALL_PROBLEMS as exc:
            self.log.warning("%s check failed: %s", self.tool_name, exc)
            return False

        self.log.debug("%s check stdout: %s", self.tool_name, out)
        if err:
            self.log.warning("%s check stderr: %s", self.tool_name, err)
        return True
