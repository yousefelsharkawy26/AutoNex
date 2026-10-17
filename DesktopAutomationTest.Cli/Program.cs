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
///   autonex record  --app <path> --plan <file.json> [--out <session.json>] [--label <text>]
///   autonex replay  --app <path> --session <file.json>
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
            "wait"    => RunWait(parsed),
            "text"    => RunText(parsed),
            "shot"    => RunShot(parsed),
            "record"  => RunRecord(parsed),
            "replay"  => RunReplay(parsed),
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


    static int RunRecord(ParsedArgs p)
    {
        RequireArg(p, "app");
        RequireArg(p, "plan");

        var planFile = p.Get("plan")!;
        if (!File.Exists(planFile))
        {
            Console.Error.WriteLine($"Error: plan file not found: {planFile}");
            return 1;
        }

        var planJson    = File.ReadAllText(planFile);
        var sessionPath = p.Get("out");
        var label       = p.Get("label");

        using var driver = CreateAndLaunch(p);
        var engine = new AgentAutomationEngine(driver);

        var result = engine.ExecuteAndRecord(planJson, sessionPath, label);
        Console.WriteLine(result);
        return result.Contains("FAILED") ? 1 : 0;
    }

    static int RunReplay(ParsedArgs p)
    {
        RequireArg(p, "app");
        RequireArg(p, "session");

        var sessionFile = p.Get("session")!;
        if (!File.Exists(sessionFile))
        {
            Console.Error.WriteLine($"Error: session file not found: {sessionFile}");
            return 1;
        }

        using var driver = CreateAndLaunch(p);
        var engine = new AgentAutomationEngine(driver);

        var result = engine.ReplaySession(sessionFile);
        Console.WriteLine(result);
        return result.Contains("FAILED") || result.StartsWith("error") ? 1 : 0;
    }

    static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Error: Unknown command '{cmd}'. Run 'autonex help' for usage.");
        return 1;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static IDriver CreateAndLaunch(ParsedArgs p)
    {
        var appPath = p.Get("app")!;
        var appArgs = p.Get("args");

        var driver = DriverFactory.CreateFlauiDriver();
        driver.Initialize(appPath, appArgs);
        return driver;
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
              record    Execute a plan AND save it as a replayable session JSON
              replay    Replay a recorded session — ZERO LLM tokens
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
              --out <file>       Output path for screenshot PNG / session JSON [shot, record]
              --session <file>   Path to session JSON file                  [replay]
              --label <text>     Label for the recorded session             [record]

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
