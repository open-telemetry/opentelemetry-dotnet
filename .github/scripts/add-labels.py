#!/usr/bin/env python3

import os
import subprocess

print(os.environ['ISSUE_NUMBER'])
print(os.environ['ISSUE_BODY'])
print(os.environ['SENDER'])

print(os.environ)

subprocess.run(['gh', 'issue', 'edit', os.environ['ISSUE_NUMBER'], '--add-label', 'help wanted'])
