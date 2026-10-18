using System;
using System.IO;
using DesktopAutomationTest.Core;
using DesktopAutomationTest.Core.Agent;

namespace DesktopAutomationTest.Cli;

/// <summary>
/// AutoNex CLI — token-efficient desktop UI automation agent tool.
///
/// Commands:
///   autonex inspect  --app &lt;path&gt; [--format text|json]
///   autonex run      --app &lt;path&gt; --plan &lt;file.json&gt;
///   autonex launch   --app &lt;path&gt; [--args &lt;args&gt;]
///   autonex wait     --app &lt;path&gt; --id &lt;automationId&gt; [--timeout &lt;seconds&gt;]
///   autonex text     --app &lt;path&gt; --id &lt;automationId&gt;
///   autonex shot     --app &lt;path&gt; [--out &lt;file.png&gt;]
///   autonex help
/// </summary>
internal class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var parsed = ParseArgs(args[1..]);

        return command switch
        {
            "inspect" => RunInspect(parsed),
            "run"     => RunPlan(parsed),
            "launch"  => RunLaunch(parsed),
            "attach"  => RunAttach(parsed),
            "wait"    => RunWait(parsed),
            "text"    => RunText(parsed),
            "shot"    => RunShot(parsed),
            "windows" => RunWindows(parsed),
            "switch"  => RunSwitchWindow(parsed),
            _         => UnknownCommand(command)
        };
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    static int RunInspect(ParsedArgs p)
    {
        RequireArg(p, "app");
        var format = p.Get("format", "text") ?? "text";

        using var driver = CreateAndLaunch(p);
        var engine = new AgentAutomationEngine(driver);

        Console.WriteLine(engine.InspectScreen(format));
        return 0;
    }

    static int RunPlan(ParsedArgs p)
    {
        RequireArg(p, "app");
        RequireArg(p, "plan");

        var planFile = p.Get("plan")!;
        if (!File.Exists(planFile))
        {
            Console.Error.WriteLine($"Error: plan file not found: {planFile}");
            return 1;
        }

        var planJson = File.ReadAllText(planFile);

        using var driver = CreateAndLaunch(p);
        var engine = new AgentAutomationEngine(driver);

        var result = engine.ExecutePlan(planJson);
        Console.WriteLine(result);
        return result.Contains("FAILED") ? 1 : 0;
    }

    static int RunLaunch(ParsedArgs p)
    {
        RequireArg(p, "app");
        using var driver = CreateAndLaunch(p);
        Console.WriteLine($"✓ Launched: {p.Get("app")}");
        Console.WriteLine("Press Enter to close the application...");
        Console.ReadLine();
        return 0;
    }

    static int RunWait(ParsedArgs p)
    {
        RequireArg(p, "app");
        RequireArg(p, "id");

        var id = p.Get("id")!;
        var timeout = int.Parse(p.Get("timeout", "10")!);

        using var driver = CreateAndLaunch(p);
        var engine = new AgentAutomationEngine(driver);

        var result = engine.WaitForElement(id, timeout);
        Console.WriteLine(result);
        return result.StartsWith("Timeout") ? 1 : 0;
    }

    static int RunText(ParsedArgs p)
    {
        RequireArg(p, "app");
        RequireArg(p, "id");

        using var driver = CreateAndLaunch(p);
        var engine = new AgentAutomationEngine(driver);

        var text = engine.GetElementText(p.Get("id")!);
        Console.WriteLine(text);
        return text.StartsWith("Error:") ? 1 : 0;
    }

    static int RunShot(ParsedArgs p)
    {
        RequireArg(p, "app");
        var outPath = p.Get("out");

        using var driver = CreateAndLaunch(p);
        var engine = new AgentAutomationEngine(driver);

        var result = engine.TakeScreenshot(outPath);
        Console.WriteLine(result);
        return result.StartsWith("Error:") ? 1 : 0;
    }

    static int RunAttach(ParsedArgs p)
    {
        var pidStr = p.Get("pid");
        var name = p.Get("name");

        if (pidStr is null && name is null)
        {
            Console.Error.WriteLine("Error: --pid <processId> or --name <processName> is required.");
            return 1;
        }

        var driver = DriverFactory.CreateFlauiDriver();
        try
        {
            var engine = new AgentAutomationEngine(driver);
            int? pid = int.TryParse(pidStr, out var parsedPid) ? parsedPid : null;
            var result = engine.Attach(pid, name);
            Console.WriteLine(result);
            driver.Dispose();
            return result.StartsWith("Error", StringComparison.Ordinal) ? 1 : 0;
        }
        catch (Exception ex)
        {
            driver.Dispose();
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static int RunWindows(ParsedArgs p)
    {
        RequireAppOrSession(p);
        using var driver = CreateDriverForSession(p, launchIfNeeded: true);
        var engine = new AgentAutomationEngine(driver);
        Console.WriteLine(engine.ListWindows());
        return 0;
    }

    static int RunSwitchWindow(ParsedArgs p)
    {
        RequireAppOrSession(p);
        RequireArg(p, "title");

        using var driver = CreateDriverForSession(p, launchIfNeeded: true);
        var engine = new AgentAutomationEngine(driver);
        var result = engine.SwitchWindow(p.Get("title")!);
        Console.WriteLine(result);
        return result.StartsWith("Error", StringComparison.Ordinal) ? 1 : 0;
    }

    static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Error: Unknown command '{cmd}'. Run 'autonex help' for usage.");
        return 1;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Path of the JSON file that remembers a running AutoNex session so subsequent
    /// CLI invocations can ATTACH to the already-open application instead of relaunching it.
    /// </summary>
    static string SessionFilePath =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "autonex_session.json");

    static IDriver CreateAndLaunch(ParsedArgs p)
    {
        var driver = CreateDriverForSession(p, launchIfNeeded: false);
        return driver;
    }

    /// <summary>
    /// Creates a driver for the current invocation:
    /// 1. If --attach &lt;pid|name&gt; is given, attaches to that running process.
    /// 2. Else if a live session file exists, attaches to it (no relaunch, no state loss).
    /// 3. Else launches the app from --app and records the session for later commands.
    /// When launchIfNeeded=false and neither attach nor a live session exists, falls back to launching.
    /// </summary>
    static IDriver CreateDriverForSession(ParsedArgs p, bool launchIfNeeded)
    {
        var driver = DriverFactory.CreateFlauiDriver();

        // Explicit attach requested
        var attachTo = p.Get("attach");
        if (!string.IsNullOrEmpty(attachTo))
        {
            try
            {
                if (int.TryParse(attachTo, out var pid))
                    driver.AttachToProcess(pid);
                else
                    driver.AttachToProcessByName(attachTo!);
                return driver;
            }
            catch (Exception ex)
            {
                driver.Dispose();
                Console.Error.WriteLine($"Error: failed to attach to '{attachTo}': {ex.Message}");
                Environment.Exit(1);
            }
        }

        // Try reusing a live recorded session
        var session = ReadSession();
        if (session != null)
        {
            try
            {
                driver.AttachToProcess(session.Value.Pid);
                return driver;
            }
            catch
            {
                // Session is stale — fall through to fresh launch.
            }
        }

        // Fresh launch
        var appPath = p.Get("app");
        if (string.IsNullOrEmpty(appPath))
        {
            driver.Dispose();
            Console.Error.WriteLine("Error: --app is required (no live session found to attach to).");
            Environment.Exit(1);
        }

        var appArgs = p.Get("args");
        driver.Initialize(appPath!, appArgs);

        // Record the session so the next command attaches instead of relaunching.
        TryRecordSession(driver, appPath!);
        return driver;
    }

    record SessionInfo(int Pid, string AppPath);

    static SessionInfo? ReadSession()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
                return null;

            var json = File.ReadAllText(SessionFilePath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var pid = root.GetProperty("pid").GetInt32();
            var appPath = root.GetProperty("appPath").GetString() ?? "";

            // Verify the process is still alive before trusting the session.
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById(pid);
                proc.Refresh();
                if (proc.HasExited)
                    return null;
            }
            catch
            {
                return null;
            }

            return new SessionInfo(pid, appPath);
        }
        catch
        {
            return null;
        }
    }

    static void TryRecordSession(IDriver driver, string appPath)
    {
        try
        {
            var proc = driver.GetOwnProcess();
            if (proc == null)
                return;

            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                pid = proc.Id,
                appPath
            });
            File.WriteAllText(SessionFilePath, json);
        }
        catch
        {
            // Session recording is best-effort; commands still work without it.
        }
    }

    static void RequireAppOrSession(ParsedArgs p)
    {
        if (p.Get("app") is null && p.Get("attach") is null && ReadSession() is null)
        {
            Console.Error.WriteLine("Error: --app is required (or start a session first / use --attach).");
            Environment.Exit(1);
        }
    }

    static void RequireArg(ParsedArgs p, string name)
    {
        if (p.Get(name) is null)
        {
            Console.Error.WriteLine($"Error: --{name} is required.");
            Environment.Exit(1);
        }
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            AutoNex CLI v1.0 — Token-Efficient Desktop UI Automation
            ──────────────────────────────────────────────────────────

            USAGE: autonex <command> [options]

            COMMANDS:
              inspect   Dump interactive UI elements (compact, token-minimal)
              run       Execute a multi-step automation plan from a JSON file
              launch    Launch an application and keep it open
              wait      Wait until an element appears (with timeout)
              text      Read the text of a specific element
              shot      Take a screenshot of the current screen
              help      Show this help message

            OPTIONS:
              --app <path>       Path to the application .exe  (required for most commands)
              --args <args>      Command-line arguments for the application
              --format <fmt>     Output format: text (default) or json  [inspect]
              --plan <file>      Path to the JSON plan file              [run]
              --id <id>          Automation ID of the target element     [wait, text]
              --timeout <secs>   Wait timeout in seconds (default: 10)  [wait]
              --out <file>       Output path for screenshot PNG          [shot]

            EXAMPLES:
              autonex inspect --app "C:\MyApp\MyApp.exe"
              autonex inspect --app "C:\MyApp\MyApp.exe" --format json
              autonex run     --app "C:\MyApp\MyApp.exe" --plan ./login_plan.json
              autonex wait    --app "C:\MyApp\MyApp.exe" --id SubmitButton --timeout 15
              autonex text    --app "C:\MyApp\MyApp.exe" --id ResultLabel
              autonex shot    --app "C:\MyApp\MyApp.exe" --out C:\screenshots\step1.png

            PLAN FILE FORMAT (JSON array):
              [
                {"action": "type",   "id": "UsernameTextBox", "value": "admin"},
                {"action": "type",   "id": "PasswordTextBox", "value": "secret"},
                {"action": "click",  "id": "LoginButton"},
                {"action": "assert_text", "id": "WelcomeLabel", "value": "Welcome"}
              ]

            EXIT CODES:
              0 = success
              1 = failure / element not found / plan step failed
            """);
    }

    // ── Argument Parser ───────────────────────────────────────────────────────

    static ParsedArgs ParseArgs(string[] args)
    {
        var dict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].StartsWith("--"))
            {
                var key = args[i][2..];
                var val = args[i + 1];
                dict[key] = val;
                i++;
            }
        }
        return new ParsedArgs(dict);
    }
}

internal record ParsedArgs(System.Collections.Generic.Dictionary<string, string> Dict)
{
    public string? Get(string key, string? defaultValue = null)
        => Dict.TryGetValue(key, out var v) ? v : defaultValue;
}
