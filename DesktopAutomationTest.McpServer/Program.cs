using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DesktopAutomationTest.Core;
using DesktopAutomationTest.Core.Agent;

namespace DesktopAutomationTest.McpServer;

/// <summary>
/// A lightweight Model Context Protocol (MCP) server implementation for stdio transport.
/// Exposes token-efficient desktop UI automation tools to any AI Agent (Claude, Cursor, Antigravity, etc.).
/// </summary>
public class Program
{
    private static IDriver? _driver;
    private static AgentAutomationEngine? _engine;
    private static bool _verbose = false;

    // Serializes access to the shared driver/engine: MCP clients may send concurrent
    // requests, and FlaUI/UIA COM objects are not thread-safe.
    private static readonly SemaphoreSlim _toolLock = new(1, 1);

    private static readonly string SessionFilePath = Path.Combine(Path.GetTempPath(), "autonex_mcp_session.json");

    private record McpPersistedSession(int ProcessId, string? AppPath, string DriverType, DateTime LastActive);

    public static async Task Main(string[] args)
    {
        _verbose = args.Length > 0 && Array.Exists(args, a => a.Equals("--verbose", StringComparison.OrdinalIgnoreCase))
                   || Environment.GetEnvironmentVariable("AUTONEX_VERBOSE") == "1";

        // Never let console encoding mangle tool output (Arabic/Unicode app labels).
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
        }
        catch
        {
            // Some hosts have no real console; ignore.
        }

        Log("INFO", "SERVER", "AutoNex MCP Server starting up...");

        using var reader = new StreamReader(Console.OpenStandardInput(), System.Text.Encoding.UTF8);
        using var writer = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true };

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var hasId = root.TryGetProperty("id", out var idProp) &&
                            (idProp.ValueKind == JsonValueKind.Number || idProp.ValueKind == JsonValueKind.String);
                var id = hasId ? idProp.Clone() : default;
                var method = root.TryGetProperty("method", out var methodProp) ? methodProp.GetString() : null;

                if (method == "initialize")
                {
                    Log("INFO", "MCP", "Received initialize request");
                    await SendResponseAsync(writer, id, new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { listChanged = false },
                            logging = new { }
                        },
                        serverInfo = new { name = "autonex-desktop-mcp", version = "1.6.0" }
                    });
                }
                else if (method == "ping")
                {
                    await SendResponseAsync(writer, id, new { });
                }
                else if (method == "notifications/initialized")
                {
                    Log("INFO", "MCP", "Client initialized. Attempting session restore...");
                    TryRestoreSession();
                }
                else if (method == "tools/list")
                {
                    await SendResponseAsync(writer, id, new { tools = GetToolsList() });
                }
                else if (method == "tools/call")
                {
                    var @params = root.GetProperty("params");
                    var toolName = @params.GetProperty("name").GetString() ?? "";
                    var arguments = @params.TryGetProperty("arguments", out var argsEl) ? argsEl : default;

                    await _toolLock.WaitAsync();
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        Log("INFO", toolName, $"Calling tool '{toolName}'...");

                        var outcome = ExecuteTool(toolName, arguments);

                        stopwatch.Stop();
                        Log(outcome.IsError ? "WARN" : "INFO", toolName,
                            $"Executed in {stopwatch.ElapsedMilliseconds}ms (Success={!outcome.IsError})");

                        var contentList = new List<object>
                        {
                            new { type = "text", text = outcome.Text }
                        };

                        if (outcome.ImagePng != null && outcome.ImagePng.Length > 0)
                        {
                            contentList.Add(new
                            {
                                type = "image",
                                data = Convert.ToBase64String(outcome.ImagePng),
                                mimeType = "image/png"
                            });
                        }

                        await SendResponseAsync(writer, id, new
                        {
                            content = contentList,
                            isError = outcome.IsError
                        });
                    }
                    finally
                    {
                        _toolLock.Release();
                    }
                }
                else
                {
                    if (hasId)
                    {
                        await SendErrorAsync(writer, id, -32601, $"Method not found: {method}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log("ERROR", "SERVER", $"Exception processing request: {ex.Message}");
                // MCP stdio requires valid JSON-RPC back when id is present
                // Otherwise keep stdout clean
            }
        }
    }

    private static void Log(string level, string component, string message)
    {
        if (!_verbose && level.Equals("DEBUG", StringComparison.OrdinalIgnoreCase)) return;
        var ts = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        Console.Error.WriteLine($"[{ts}] [{level.ToUpperInvariant()}] [{component}] {message}");
    }

    private static void SaveSession(int pid, string? appPath, string driverType)
    {
        try
        {
            var session = new McpPersistedSession(pid, appPath, driverType, DateTime.UtcNow);
            File.WriteAllText(SessionFilePath, JsonSerializer.Serialize(session));
            Log("INFO", "SESSION", $"Persisted session PID {pid} to {SessionFilePath}");
        }
        catch (Exception ex)
        {
            Log("WARN", "SESSION", $"Failed to save session: {ex.Message}");
        }
    }

    private static void ClearPersistedSession()
    {
        try
        {
            if (File.Exists(SessionFilePath))
            {
                File.Delete(SessionFilePath);
                Log("INFO", "SESSION", "Cleared persisted session.");
            }
        }
        catch { }
    }

    private static void TryRestoreSession()
    {
        if (_engine != null) return;

        try
        {
            if (!File.Exists(SessionFilePath)) return;

            var json = File.ReadAllText(SessionFilePath);
            var session = JsonSerializer.Deserialize<McpPersistedSession>(json);
            if (session == null) return;

            Process? proc = null;
            try
            {
                proc = Process.GetProcessById(session.ProcessId);
            }
            catch (ArgumentException)
            {
                ClearPersistedSession();
                return;
            }

            if (proc == null || proc.HasExited)
            {
                ClearPersistedSession();
                return;
            }

            Log("INFO", "SESSION", $"Re-attaching to running process PID {session.ProcessId} ({proc.ProcessName})...");
            _driver?.Dispose();
            _driver = DriverFactory.CreateDriver(session.DriverType);
            _driver.AttachToProcess(session.ProcessId);
            _engine = new AgentAutomationEngine(_driver);
            Log("INFO", "SESSION", $"Successfully re-attached to PID {session.ProcessId}.");
        }
        catch (Exception ex)
        {
            Log("DEBUG", "SESSION", $"Session restore bypassed: {ex.Message}");
        }
    }

    private static object[] GetToolsList()
    {
        return new object[]
        {
            new
            {
                name = "launch_application",
                description = "Launches the target desktop application using the specified driver. Resets any previous session.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the application executable." },
                        arguments = new { type = "string", description = "Optional command line arguments." },
                        driver = new { type = "string", @enum = new[] { "flaui", "avalonia" }, description = "Driver type: 'flaui' (default for Windows UIA3) or 'avalonia' (cross-platform Linux/macOS/Windows)." }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "attach_to_process",
                description = "Attaches to an ALREADY-RUNNING application process (by process_id or process_name) instead of launching a new instance.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        process_id = new { type = "integer", description = "OS process id to attach to." },
                        process_name = new { type = "string", description = "Executable name (with or without .exe) of a running process to attach to." }
                    }
                }
            },
            new
            {
                name = "inspect_screen",
                description = "Returns a compact, token-minimized representation of the interactive UI elements on screen (~90% fewer tokens). Supports diff_only=true, max_depth, and container_id filtering.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        format = new { type = "string", @enum = new[] { "text", "json" }, description = "Format: 'text' (default, minimal tokens) or 'json'." },
                        diff_only = new { type = "boolean", description = "When true, returns '[No UI changes since last inspect]' if UI state is identical to previous inspect, saving significant tokens." },
                        max_depth = new { type = "integer", description = "Optional max hierarchy depth to traverse (useful for complex apps)." },
                        container_id = new { type = "string", description = "Optional container AutomationId to scope the inspection to a specific panel or dialog." }
                    }
                }
            },
            new
            {
                name = "execute_plan",
                description = "Executes a multi-step automation plan in a SINGLE tool call with self-healing locators and UI state delta tracking. Supports dry_run mode.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        plan_json = new
                        {
                            type = "string",
                            description = "JSON array of actions. Actions: 'click', 'type', 'paste', 'select', 'check', 'uncheck', 'double_click', 'right_click', 'press_key', 'send_keys', 'hover', 'wait', 'assert', 'assert_text', 'scroll', 'scroll_into_view', 'clear'."
                        },
                        dry_run = new
                        {
                            type = "boolean",
                            description = "When true, validates all actions and ensures locators exist without executing actual clicks or typing."
                        }
                    },
                    required = new[] { "plan_json" }
                }
            },
            new
            {
                name = "wait_for_element",
                description = "Polls the UI until an element with the given automation ID appears. Returns immediately when found.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string", description = "Automation ID of the element to wait for." },
                        timeout_seconds = new { type = "integer", description = "Maximum seconds to wait (default: 10)." }
                    },
                    required = new[] { "id" }
                }
            },
            new
            {
                name = "click_element",
                description = "Clicks a UI element identified by its automation ID with automatic fuzzy self-healing.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string", description = "Automation ID of the element to click." }
                    },
                    required = new[] { "id" }
                }
            },
            new
            {
                name = "type_text",
                description = "Types text into an input field identified by its automation ID. ValuePattern-first with clear option.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string", description = "Automation ID of the target field." },
                        text = new { type = "string", description = "Text to enter." },
                        clear = new { type = "boolean", description = "When true, clears any existing text before typing." }
                    },
                    required = new[] { "id", "text" }
                }
            },
            new
            {
                name = "get_element_text",
                description = "Reads the current text or value of a UI element.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string", description = "Automation ID of the element." }
                    },
                    required = new[] { "id" }
                }
            },
            new
            {
                name = "take_screenshot",
                description = "Captures a screenshot for visual inspection. Can return base64 PNG directly to vision-capable agents.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        output_path = new { type = "string", description = "Optional file path to save the screenshot." },
                        include_image = new { type = "boolean", description = "When true, PNG bytes are returned inline in MCP image format." }
                    }
                }
            },
            new
            {
                name = "attach_to_process",
                description = "Attaches to an ALREADY-RUNNING application process (by process_id or process_name) instead of launching a new instance. Enables session reuse so repeated CLI/MCP calls do not relaunch the app.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        process_id = new { type = "integer", description = "OS process id to attach to." },
                        process_name = new { type = "string", description = "Executable name (with or without .exe) of a running process to attach to. Used when process_id is absent." }
                    }
                }
            },
            new
            {
                name = "record_session",
                description = "Executes a multi-step plan AND records every action + result to a JSON session file for ZERO-TOKEN future replay. Use this the first time you automate a flow.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        plan_json = new { type = "string", description = "JSON array of actions (same format as execute_plan)." },
                        session_path = new { type = "string", description = "Optional output path for the session JSON file. Auto-generated in temp folder if omitted." },
                        label = new { type = "string", description = "Optional human-readable label for this session (e.g. 'login flow')." }
                    },
                    required = new[] { "plan_json" }
                }
            },
            new
            {
                name = "replay_session",
                description = "Replays a previously recorded session JSON file with zero token cost — no LLM needed.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        session_path = new { type = "string", description = "Path to the session JSON file." }
                    },
                    required = new[] { "session_path" }
                }
            },
            new
            {
                name = "list_windows",
                description = "Lists all top-level windows of the automated application, including modal dialogs (MessageBox, file dialogs, secondary windows).",
                inputSchema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "switch_window",
                description = "Switches automation context to the application window whose title contains the given text (case-insensitive). Use after a dialog opens to bring it to the foreground and target its elements.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string", description = "Substring of the target window title (e.g. Save As, Confirm)." },
                        timeout_seconds = new { type = "integer", description = "Optional timeout in seconds (default: 5)." }
                    },
                    required = new[] { "title" }
                }
            },
            new
            {
                name = "scroll",
                description = "Scrolls a scrollable container or active window in direction ('up', 'down', 'left', 'right').",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        direction = new { type = "string", @enum = new[] { "down", "up", "left", "right" }, description = "Scroll direction." },
                        id = new { type = "string", description = "Optional container AutomationId. If omitted, scrolls active window." }
                    }
                }
            },
            new
            {
                name = "scroll_into_view",
                description = "Scrolls the container until the specified target element is visible on screen.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string", description = "AutomationId of the element to scroll into view." }
                    },
                    required = new[] { "id" }
                }
            },
            new
            {
                name = "export_session_as_test",
                description = "Converts a recorded session JSON file into executable C# xUnit test code or a Gherkin/Reqnroll feature scenario.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        session_path = new { type = "string", description = "Path to the recorded session JSON file." },
                        format = new { type = "string", @enum = new[] { "csharp", "gherkin" }, description = "Output format: 'csharp' (default) or 'gherkin'." }
                    },
                    required = new[] { "session_path" }
                }
            },
            new
            {
                name = "close_application",
                description = "Closes the automated application and cleans up resources and session state.",
                inputSchema = new { type = "object", properties = new { } }
            }
        };
    }

    private static ToolCallOutcome ExecuteTool(string toolName, JsonElement arguments)
    {
        try
        {
            if (_engine == null)
            {
                TryRestoreSession();
            }

            switch (toolName)
            {
                case "launch_application":
                    var path = arguments.GetProperty("path").GetString();
                    var args = arguments.TryGetProperty("arguments", out var a) ? a.GetString() : null;
                    var driverType = arguments.TryGetProperty("driver", out var dt) && !string.IsNullOrEmpty(dt.GetString()) ? dt.GetString()! : DriverFactory.DefaultDriverType;

                    if (string.IsNullOrEmpty(path))
                    {
                        return ToolCallOutcome.Fail("Error: Application path is required.");
                    }

                    _driver?.Dispose();
                    _driver = DriverFactory.CreateDriver(driverType);
                    _driver.Initialize(path, args);
                    _engine = new AgentAutomationEngine(_driver);

                    // Persist session info if possible
                    if (_driver is FlaUIAdapter flaui && flaui.Application != null)
                    {
                        SaveSession(flaui.Application.ProcessId, path, driverType);
                    }

                    return ToolCallOutcome.Ok($"Application launched successfully: {path} (Driver: {driverType})");

                case "attach_to_process":
                    var pidEl = arguments.TryGetProperty("process_id", out var pidProp) && pidProp.ValueKind == JsonValueKind.Number
                        ? (int?)pidProp.GetInt32()
                        : null;
                    var procName = arguments.TryGetProperty("process_name", out var pnProp) ? pnProp.GetString() : null;

                    if (pidEl == null && string.IsNullOrEmpty(procName))
                    {
                        return ToolCallOutcome.Fail("Error: either process_id or process_name is required.");
                    }

                    _driver?.Dispose();
                    _driver = DriverFactory.CreateDriver(DriverFactory.DefaultDriverType);
                    try
                    {
                        var attachEngine = new AgentAutomationEngine(_driver);
                        var attachResult = attachEngine.Attach(pidEl, procName);
                        _engine = attachEngine;

                        if (pidEl.HasValue)
                        {
                            SaveSession(pidEl.Value, null, "flaui");
                        }

                        return attachResult.StartsWith("Error", StringComparison.Ordinal)
                            ? ToolCallOutcome.Fail(attachResult)
                            : ToolCallOutcome.Ok(attachResult);
                    }
                    catch (Exception ex)
                    {
                        _driver?.Dispose();
                        _driver = null;
                        _engine = null;
                        return ToolCallOutcome.Fail($"Error attaching to process: {ex.Message}");
                    }

                case "inspect_screen":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var format = arguments.TryGetProperty("format", out var f) ? f.GetString() ?? "text" : "text";
                    var diffOnly = arguments.TryGetProperty("diff_only", out var dfo) && dfo.GetBoolean();
                    var maxDepth = arguments.TryGetProperty("max_depth", out var md) ? md.GetInt32() : -1;
                    var containerId = arguments.TryGetProperty("container_id", out var cid) ? cid.GetString() : null;

                    return ToolCallOutcome.Ok(_engine.InspectScreen(format, diffOnly, maxDepth, containerId));

                case "execute_plan":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var plan = arguments.TryGetProperty("plan_json", out var pj) ? pj.GetString() : null;
                    if (string.IsNullOrEmpty(plan))
                    {
                        return ToolCallOutcome.Fail("Error: plan_json is required.");
                    }

                    var dryRun = arguments.TryGetProperty("dry_run", out var dr) && dr.GetBoolean();
                    var planResult = _engine.ExecutePlan(plan, dryRun);
                    return planResult.Contains("FAILED", StringComparison.OrdinalIgnoreCase) || planResult.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
                        ? ToolCallOutcome.Fail(planResult)
                        : ToolCallOutcome.Ok(planResult);

                case "wait_for_element":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var waitId = arguments.GetProperty("id").GetString();
                    if (string.IsNullOrEmpty(waitId))
                    {
                        return ToolCallOutcome.Fail("Error: id is required.");
                    }

                    var timeout = arguments.TryGetProperty("timeout_seconds", out var to) ? to.GetInt32() : 10;
                    var waitResult = _engine.WaitForElement(waitId, timeout);
                    return waitResult.StartsWith("Timeout", StringComparison.Ordinal)
                        ? ToolCallOutcome.Fail(waitResult)
                        : ToolCallOutcome.Ok(waitResult);

                case "click_element":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var clickId = arguments.GetProperty("id").GetString();
                    if (string.IsNullOrEmpty(clickId))
                    {
                        return ToolCallOutcome.Fail("Error: id is required.");
                    }

                    var clickResult = _engine.Click(clickId);
                    return clickResult.StartsWith("Error", StringComparison.Ordinal)
                        ? ToolCallOutcome.Fail(clickResult)
                        : ToolCallOutcome.Ok(clickResult);

                case "type_text":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var typeId = arguments.GetProperty("id").GetString();
                    var textVal = arguments.GetProperty("text").GetString();
                    var clearFirst = arguments.TryGetProperty("clear", out var cf) && cf.GetBoolean();

                    if (string.IsNullOrEmpty(typeId))
                    {
                        return ToolCallOutcome.Fail("Error: id is required.");
                    }

                    if (clearFirst)
                    {
                        _engine.Clear(typeId);
                    }

                    var typeResult = _engine.Type(typeId, textVal ?? string.Empty);
                    return typeResult.StartsWith("Error", StringComparison.Ordinal)
                        ? ToolCallOutcome.Fail(typeResult)
                        : ToolCallOutcome.Ok(typeResult);

                case "get_element_text":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var textId = arguments.GetProperty("id").GetString();
                    if (string.IsNullOrEmpty(textId))
                    {
                        return ToolCallOutcome.Fail("Error: id is required.");
                    }

                    var textResult = _engine.GetElementText(textId);
                    return textResult.StartsWith("Error", StringComparison.Ordinal)
                        ? ToolCallOutcome.Fail(textResult)
                        : ToolCallOutcome.Ok(textResult);

                case "take_screenshot":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var outPath = arguments.TryGetProperty("output_path", out var op) ? op.GetString() : null;
                    var includeImage = arguments.TryGetProperty("include_image", out var ii) && ii.GetBoolean();
                    var shotResult = _engine.CaptureScreenshot(outPath, includeImage);
                    return shotResult.IsError
                        ? ToolCallOutcome.Fail(shotResult.Text, shotResult.ImagePng)
                        : ToolCallOutcome.Ok(shotResult.Text, shotResult.ImagePng);

                case "record_session":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var recPlan = arguments.GetProperty("plan_json").GetString();
                    if (string.IsNullOrEmpty(recPlan))
                    {
                        return ToolCallOutcome.Fail("Error: plan_json is required.");
                    }

                    var recPath = arguments.TryGetProperty("session_path", out var rp) ? rp.GetString() : null;
                    var recLabel = arguments.TryGetProperty("label", out var rl) ? rl.GetString() : null;
                    var recResult = _engine.ExecuteAndRecord(recPlan, recPath, recLabel);
                    return ToolCallOutcome.Ok(recResult);

                case "replay_session":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var replayPath = arguments.TryGetProperty("session_path", out var sp) ? sp.GetString() : null;
                    if (string.IsNullOrEmpty(replayPath))
                    {
                        return ToolCallOutcome.Fail("Error: session_path is required.");
                    }

                    var repResult = _engine.ReplaySession(replayPath);
                    return repResult.StartsWith("Error", StringComparison.Ordinal)
                        ? ToolCallOutcome.Fail(repResult)
                        : ToolCallOutcome.Ok(repResult);

                case "list_windows":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }
                    return ToolCallOutcome.Ok(_engine.ListWindows());

                case "switch_window":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var winTitle = arguments.GetProperty("title").GetString();
                    if (string.IsNullOrEmpty(winTitle))
                    {
                        return ToolCallOutcome.Fail("Error: title is required.");
                    }

                    var winTimeout = arguments.TryGetProperty("timeout_seconds", out var wt) ? wt.GetInt32() : 5;
                    var swResult = _engine.SwitchWindow(winTitle, winTimeout);
                    return swResult.StartsWith("Error", StringComparison.Ordinal)
                        ? ToolCallOutcome.Fail(swResult)
                        : ToolCallOutcome.Ok(swResult);

                case "scroll":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var sDir = arguments.TryGetProperty("direction", out var sd) ? sd.GetString() ?? "down" : "down";
                    var sId = arguments.TryGetProperty("id", out var si) ? si.GetString() : null;
                    var scrollRes = _engine.Scroll(sDir, sId);
                    return scrollRes.StartsWith("Error", StringComparison.Ordinal)
                        ? ToolCallOutcome.Fail(scrollRes)
                        : ToolCallOutcome.Ok(scrollRes);

                case "scroll_into_view":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var svId = arguments.GetProperty("id").GetString();
                    if (string.IsNullOrEmpty(svId))
                    {
                        return ToolCallOutcome.Fail("Error: id is required.");
                    }

                    var sivRes = _engine.ScrollIntoView(svId);
                    return sivRes.StartsWith("Error", StringComparison.Ordinal)
                        ? ToolCallOutcome.Fail(sivRes)
                        : ToolCallOutcome.Ok(sivRes);

                case "export_session_as_test":
                    var expSessionPath = arguments.GetProperty("session_path").GetString();
                    if (string.IsNullOrEmpty(expSessionPath))
                    {
                        return ToolCallOutcome.Fail("Error: session_path is required.");
                    }

                    var expFormat = arguments.TryGetProperty("format", out var ef) ? ef.GetString() ?? "csharp" : "csharp";
                    var engineToUse = _engine ?? new AgentAutomationEngine(DriverFactory.CreateFlauiDriver());
                    return ToolCallOutcome.Ok(engineToUse.ExportSessionAsTest(expSessionPath, expFormat));

                case "close_application":
                    ClearPersistedSession();
                    _driver?.Dispose();
                    _driver = null;
                    _engine = null;
                    return ToolCallOutcome.Ok("Application closed successfully.");

                default:
                    return ToolCallOutcome.Fail($"Error: Unknown tool '{toolName}'.");
            }
        }
        catch (Exception ex)
        {
            return ToolCallOutcome.Fail($"Error executing tool '{toolName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Outcome of an MCP tools/call: text payload, error flag, optional inline PNG.
    /// </summary>
    private sealed record ToolCallOutcome(string Text, bool IsError, byte[]? ImagePng = null)
    {
        public static ToolCallOutcome Ok(string text, byte[]? png = null) => new(text, false, png);
        public static ToolCallOutcome Fail(string text, byte[]? png = null) => new(text, true, png);
    }

    private static async Task SendResponseAsync(StreamWriter writer, JsonElement id, object result)
    {
        var response = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.ValueKind switch
            {
                JsonValueKind.Number => id.GetInt64(),
                JsonValueKind.String => id.GetString(),
                _ => null
            },
            ["result"] = result
        };

        var json = JsonSerializer.Serialize(response);
        await writer.WriteLineAsync(json);
    }

    private static async Task SendErrorAsync(StreamWriter writer, JsonElement id, int code, string message)
    {
        var response = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.ValueKind switch
            {
                JsonValueKind.Number => id.GetInt64(),
                JsonValueKind.String => id.GetString(),
                _ => null
            },
            ["error"] = new { code, message }
        };

        var json = JsonSerializer.Serialize(response);
        await writer.WriteLineAsync(json);
    }
}
