@echo off
set NET=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
"%NET%\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /lib:"%NET%\WPF" ^
  /win32icon:"%~dp0arca.ico" /resource:"%~dp0arca.ico",arca.ico /resource:"%~dp0src\Ui\Styles.xaml",Styles.xaml ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Xaml.dll ^
  /r:PresentationFramework.dll /r:PresentationCore.dll /r:WindowsBase.dll /r:UIAutomationClient.dll /r:UIAutomationTypes.dll ^
  /out:"%~dp0arca.exe" /recurse:"%~dp0src\*.cs"
