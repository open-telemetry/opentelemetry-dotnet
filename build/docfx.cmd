SETLOCAL
SETLOCAL ENABLEEXTENSIONS

type docs\docfx.json > docfx.json
type docs\toc.yml > toc.yml
docfx build docfx.json > docfx.log
@IF NOT %ERRORLEVEL% == 0 (
  type docfx.log
  ECHO Error: docfx build failed. 1>&2
  EXIT /B %ERRORLEVEL%
)
@type docfx.log
@type docfx.log | findstr /C:"Build succeeded."
@IF NOT %ERRORLEVEL% == 0 (
  ECHO Error: you have introduced build warnings. 1>&2
  EXIT /B %ERRORLEVEL%
)
