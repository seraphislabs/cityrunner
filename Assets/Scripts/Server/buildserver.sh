dotnet new console -o masterserver
mv master.cs masterserver/Program.cs
mv master.csproj masterserver/masterserver.csproj
mv networkdata.cs masterserver/networkdata.cs
cd masterserver
dotnet build