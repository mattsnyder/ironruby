@echo off

if exist %~dp0perf rmdir /S /Q %~dp0perf
mkdir %~dp0perf
pushd %~dp0perf

set RUBY=%~dp0..\..\bin\release\ir.exe

<nul (set/p z=IronRuby Rack ... )
%RUBY% %~dp0test.rb rack 1> ironruby-rack.txt 2>&1
if "%ERRORLEVEL%" equ "0" ( echo [pass] ) else ( echo [fail] )

<nul (set/p z=IronRuby Sinatra ... )
%RUBY% %~dp0test.rb sinatra 1> ironruby-sinatra.txt 2>&1
if "%ERRORLEVEL%" equ "0" ( echo [pass] ) else ( echo [fail] )

<nul (set/p z=IronRuby Rails ... )
%RUBY% %~dp0test.rb rails 1> ironruby-rails.txt 2>&1
if "%ERRORLEVEL%" equ "0" ( echo [pass] ) else ( echo [fail] )

set RUBY=C:\Ruby\bin\ruby.exe

<nul (set/p z=MRI Rack ... )
%RUBY% %~dp0test.rb rack 1> ruby-rack.txt 2>&1
if "%ERRORLEVEL%" equ "0" ( echo [pass] ) else ( echo [fail] )

<nul (set/p z=MRI Sinatra ... )
%RUBY% %~dp0test.rb sinatra 1> ruby-sinatra.txt 2>&1
if "%ERRORLEVEL%" equ "0" ( echo [pass] ) else ( echo [fail] )

<nul (set/p z=MRI Rails ... )
%RUBY% %~dp0test.rb rails 1> ruby-rails.txt 2>&1
if "%ERRORLEVEL%" equ "0" ( echo [pass] ) else ( echo [fail] )

echo DONE. See the "perf" directory for results.
popd
