# WindowsService.NETCore
How to create a Windows Service using a .NET console App<br />
Credits:<br />
https://www.pmichaels.net/2019/01/08/creating-a-windows-service-using-net-core-2-2/ and https://dzone.com/articles/generate-an-exe-for-net-core-console-apps-net-core

Simple steps:<br />
1- Create a Console App (.NET Core)<br />
2- Install the Windows Compatibility NuGet package (Install-Package Microsoft.Windows.Compatibility)<br />
3- Create a service (like the one at CoreserviceTest/Program.cs)<br />
4- Create and start the service by running the cmd as admin and typing the following instructions:<br />
>sc create [service name] binpath=[full path to binary]<br />
>sc start [service name]<br />
*The binary must be an executable<br />
To turn the dll into an executable run the cmd at the solution directory and type: <br />
>dotnet publish -c Debug -r win10-x64
