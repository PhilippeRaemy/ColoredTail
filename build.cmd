@echo off
setlocal
for %%r in (recolor.exe) do set recolor=^| "%%~$PATH:r" green=.*
cd "%~dp0"
call :prestore         || exit /b 1
call :build Debug %*   || exit /b 1
call :build Release %* || exit /b 1
echo All builds successful %recolor%
goto :EOF

:prestore
for /r %%s in (*.sln) do (nuget.exe restore "%%s" || exit /b 1)
goto :EOF

:build
for /r %%s in (*.sln) do (msbuild.exe "%%s" /p:AllowUnsafeBlocks=true;Configuration=%1 /v:m %2 %3 %4 %5 %6 %7 %8 %9 || (echo error building %1 solution %%s & exit /b 1))
echo %1 build is successful %recolor%
goto :EOF
