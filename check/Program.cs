// Scoring for the interviewer: dotnet run --project check
// Prints one line per task. Do not edit this folder: its fingerprint is printed at the end.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Claims;

var root = FindRoot();
Console.WriteLine("Checking your work (about a minute)...\n");

// T1 sorting
int t1 = 0;
t1 += Ok(() => Ids(R().Search(1, "City Pharmacy", "amount", "asc")).SequenceEqual(new long[] { 104, 105 }));
t1 += Ok(() => Ids(R().Search(1, "Bright Smiles Dental", "date", "desc")).SequenceEqual(new long[] { 103, 101 }));
t1 += Ok(() => Ids(R().Search(1, "City Pharmacy", "status", "asc")).SequenceEqual(new long[] { 105, 104 }));
t1 += Ok(() => Rejects("City Pharmacy", "amount, (SELECT name FROM members)") && Rejects("City Pharmacy", "amount;DROP TABLE claims")
               && Rejects("City Pharmacy", "submitted_at"));
Console.WriteLine($"T1 Sorting: {t1}/4 checks pass");

// T2 search
int t2 = 0;
t2 += Ok(() => Ids(R().Search(1, "dental")).OrderBy(x => x).SequenceEqual(new long[] { 101, 103 }));
t2 += Ok(() => Ids(R().Search(1, "O'Brien")).SequenceEqual(new long[] { 102 }));
t2 += Ok(() => R().Search(1, "' OR 1=1 --").Count == 0);
t2 += Ok(() => R().Search(1, "crown").Count == 0 && R().Search(1, "").All(c => c.Id < 200));
Console.WriteLine($"T2 Search: {t2}/4 checks pass");
var literal = Ok(() => Ids(R().Search(1, "100%")).SequenceEqual(new long[] { 104 })) == 1;
Console.WriteLine($"   Extra: \"100%\" finds only the claim that really contains 100%: {(literal ? "yes" : "no")}");

// T3 your tests
var myTestsFile = Path.Combine(root, "tests", "Claims.Tests", "MyTests.cs");
var mine = RunTests(Path.Combine(root, "tests", "Claims.Tests", "Claims.Tests.csproj"), "FullyQualifiedName~Claims.Tests.MyTests");
var agentOnYours = RunTests(Path.Combine(root, "tests", "Claims.Tests", "Claims.Tests.csproj"), "FullyQualifiedName~Claims.Tests.AgentTests");
var vsAgent = RunMyTestsAgainstAgentVersion(myTestsFile);
if (mine.Total == 0)
    Console.WriteLine("T3 Your tests: 0 tests in MyTests.cs");
else
    Console.WriteLine($"T3 Your tests: {mine.Total} tests | pass on your code: {mine.Passed}/{mine.Total} | catch the agent's version: {vsAgent.Failed} of {vsAgent.Total}");

// T4 notes
var notes = File.Exists(Path.Combine(root, "NOTES.md")) ? File.ReadAllText(Path.Combine(root, "NOTES.md")) : "";
var sections = Regex.Split(notes, @"^## ", RegexOptions.Multiline).Skip(1)
    .Count(s => Regex.Matches(string.Join(" ", s.Split('\n').Skip(1)), @"[A-Za-z]{2,}").Count >= 10);
Console.WriteLine($"T4 Notes: {Math.Min(sections, 3)}/3 sections written");

Console.WriteLine();
Console.WriteLine($"The agent's tests on your code: {agentOnYours.Passed} passed, {agentOnYours.Failed} failed");
Console.WriteLine($"Check files: {Fingerprint()}");
return 0;

// ---------- helpers ----------
static ClaimsRepository R() => new(Db.CreateSeeded());
static IEnumerable<long> Ids(IEnumerable<Claim> claims) => claims.Select(c => c.Id).ToList();
static int Ok(Func<bool> f) { try { return f() ? 1 : 0; } catch { return 0; } }
static bool Rejects(string text, string sortBy)
{
    try { R().Search(1, text, sortBy, "asc"); return false; }
    catch (ArgumentException) { return true; }
    catch { return false; }
}

static string FindRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d != null && !File.Exists(Path.Combine(d.FullName, "Directory.Build.props"))) d = d.Parent;
    return d?.FullName ?? Directory.GetCurrentDirectory();
}

static (int Total, int Passed, int Failed) RunTests(string project, string filter)
{
    var results = Path.Combine(Path.GetTempPath(), "check-" + Guid.NewGuid().ToString("N"));
    var p = Process.Start(new ProcessStartInfo("dotnet",
        $"test \"{project}\" --nologo --filter \"{filter}\" --logger \"trx;LogFileName=r.trx\" --results-directory \"{results}\"")
        { RedirectStandardOutput = true, RedirectStandardError = true })!;
    p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit();
    var trx = Path.Combine(results, "r.trx");
    if (!File.Exists(trx)) return (0, 0, 0);
    var m = Regex.Match(File.ReadAllText(trx), "<Counters total=\"(\\d+)\" executed=\"\\d+\" passed=\"(\\d+)\" failed=\"(\\d+)\"");
    return m.Success ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)) : (0, 0, 0);
}

(int Total, int Passed, int Failed) RunMyTestsAgainstAgentVersion(string myTests)
{
    var dir = Path.Combine(Path.GetTempPath(), "agent-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    foreach (var f in Directory.GetFiles(Path.Combine(root, "check", "agent_version"), "*.cs"))
        File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
    File.Copy(myTests, Path.Combine(dir, "MyTests.cs"));
    File.WriteAllText(Path.Combine(dir, "AgentRun.csproj"), """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework><RollForward>Major</RollForward>
            <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><IsPackable>false</IsPackable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.10" />
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
            <PackageReference Include="xunit" Version="2.9.2" />
            <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
          </ItemGroup>
          <ItemGroup><Using Include="Xunit" /><Using Include="Claims" /></ItemGroup>
        </Project>
        """);
    return RunTests(Path.Combine(dir, "AgentRun.csproj"), "FullyQualifiedName~MyTests");
}

string Fingerprint()
{
    var files = new[] { "check/Program.cs", "check/Check.csproj", "check/agent_version/ClaimsRepository.cs", "check/agent_version/Db.cs" };
    var all = string.Concat(files.Select(f => File.ReadAllText(Path.Combine(root, f)).Replace("\r\n", "\n")));
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(all)))[..8].ToLowerInvariant();
}
