@ECHO OFF
IF NOT "%~f0" == "~f0" GOTO :WinNT
@"ir.exe" "C:/dev/ironruby/Merlin/External.LCA_RESTRICTED/Languages/Ruby/ruby-1.8.6p368/lib/ruby/gems/1.8/bin/rake" %1 %2 %3 %4 %5 %6 %7 %8 %9
GOTO :EOF
:WinNT
@"ir.exe" "%~dpn0" %*
