#!/usr/bin/env python3

import glob
import os
import sys

def sanitycheck(pattern, allow_utf8 = False):
    error_count = 0

    for filename in glob.glob(pattern, recursive=True):
        if not os.path.isfile(filename):
            continue
        with open(filename, 'rb') as file:
            content = file.read()
            error = []
            lineno = 1
            for line in content.splitlines():
                if allow_utf8 and lineno == 1 and line.startswith(b'\xef\xbb\xbf'):
                    line = line[3:]
                if any(b > 127 for b in line):
                    error.append('  Non-ASCII character found at Ln:{} {}'.format(lineno, line))
                if line[-1:] == b' ' or line[-1:] == b'\t':
                    error.append('  Trailing space found at Ln:{} {}'.format(lineno, line))
                lineno += 1
            if error:
                error_count += 1
                print('{} [FAIL]'.format(filename), file=sys.stderr)
                for msg in error:
                    print(msg, file=sys.stderr)
            else:
                # print('{} [PASS]'.format(filename))
                pass

    return error_count

retval = 0
retval += sanitycheck('**/*.cmd')
retval += sanitycheck('**/*.cs', allow_utf8 = True)
retval += sanitycheck('**/*.csproj', allow_utf8 = True)
retval += sanitycheck('**/*.md')
retval += sanitycheck('**/*.proj', allow_utf8 = True)
retval += sanitycheck('**/*.py')
retval += sanitycheck('**/*.xml', allow_utf8 = True)

sys.exit(retval)
