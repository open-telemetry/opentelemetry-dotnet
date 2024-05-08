#!/usr/bin/env python3

import re
import os
import subprocess

issue_number = os.environ['ISSUE_NUMBER']
issue_body = os.environ['ISSUE_BODY']
issue_sender = os.environ['SENDER']

pattern = re.compile(r'^[#]+ Area\n\n(area:\w+)', re.MULTILINE)

tag = pattern.match(issue_body).group(1)

print('Area tag:', tag)

subprocess.run(['gh', 'issue', 'edit', issue_number, '--add-label', tag])
