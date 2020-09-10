#!/usr/bin/env python3

import glob
import os
import sys

CR = b'\r'
CRLF = b'\r\n'
LF = b'\n'

def sanitycheck(pattern, allow_utf8 = False, allow_eol = (CRLF, LF)):
    error_count = 0

    for filename in glob.glob(pattern, recursive=True):
        if not os.path.isfile(filename):
            continue
        with open(filename, 'rb') as file:
            content = file.read()
            error = []
            eol = None
            lineno = 1
            if not content:
                error.append('  Empty file found')
            elif content[-1] != 10: # LF
                error.append('  Missing a blank line before EOF')
            for line in content.splitlines(True):
                if allow_utf8 and lineno == 1 and line.startswith(b'\xef\xbb\xbf'):
                    line = line[3:]
                if any(b > 127 for b in line):
                    error.append('  Non-ASCII character found at Ln:{} {}'.format(lineno, line))
                if line[-2:] == CRLF:
                    if not eol:
                        eol = CRLF
                    elif eol != CRLF:
                        error.append('  Inconsistent line ending found at Ln:{} {}'.format(lineno, line))
                    line = line[:-2]
                elif line[-1:] == LF:
                    if not eol:
                        eol = LF
                    elif eol != LF:
                        error.append('  Inconsistent line ending found at Ln:{} {}'.format(lineno, line))
                    line = line[:-1]
                elif line[-1:] == CR:
                    error.append('  CR found at Ln:{} {}'.format(lineno, line))
                    line = line[:-1]
                if eol:
                    if eol not in allow_eol:
                        error.append('  Line ending {} not allowed at Ln:{}'.format(eol, lineno))
                        break
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
retval += sanitycheck('**/*.cmd', allow_eol = (CRLF,))
retval += sanitycheck('**/*.config', allow_utf8 = True, allow_eol = (LF,))
retval += sanitycheck('**/*.cs', allow_utf8 = True, allow_eol = (LF,))
retval += sanitycheck('**/*.cshtml', allow_utf8 = True, allow_eol = (LF,))
retval += sanitycheck('**/*.csproj', allow_utf8 = True, allow_eol = (LF,))
retval += sanitycheck('**/*.htm', allow_eol = (LF,))
retval += sanitycheck('**/*.html', allow_eol = (LF,))
retval += sanitycheck('**/*.md', allow_eol = (LF,))
retval += sanitycheck('**/*.proj', allow_eol = (LF,))
retval += sanitycheck('**/*.props', allow_eol = (LF,))
retval += sanitycheck('**/*.py', allow_eol = (LF,))
retval += sanitycheck('**/*.ruleset', allow_utf8 = True, allow_eol = (LF,))
retval += sanitycheck('**/*.sln', allow_utf8 = True, allow_eol = (LF,))
retval += sanitycheck('**/*.xml', allow_eol = (LF,))

sys.exit(retval)
