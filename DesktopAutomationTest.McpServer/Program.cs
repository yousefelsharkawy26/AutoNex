using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    // Serializes access to the shared driver/engine: MCP clients may send concurrent
    // requests, and FlaUI/UIA COM objects are not thread-safe.
    private static readonly SemaphoreSlim _toolLock = new(1, 1);

    public static async Task Main(string[] args)
    {
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
                    await SendResponseAsync(writer, id, new
                    {
                        protocolVersion = "2024-11-05",
<<<<<<< HEAD
                        capabilities = new { tools = new { listChanged = false } },
                        serverInfo = new { name = "autonex-desktop-mcp", version = "1.2.0" }
=======
                        capabilities = new { tools = new { } },
                        serverInfo = new { name = "autonex-desktop-mcp", version = "1.6.0" }
>>>>>>> f70b1d2
                    });
                }
                else if (method == "ping")
                {
                    // MCP heartbeat: must be answered or some clients consider the server dead.
                    if (hasId)
                    {
                        await SendResponseAsync(writer, id, new { });
                    }
                }
                else if (method == "notifications/initialized")
                {
                    // No response required for notification
                }
                else if (method == "tools/list")
                {
                    await SendResponseAsync(writer, id, new
                    {
                        tools = GetTools()
                    });
                }
                else if (method == "tools/call")
                {
                    var paramsEl = root.GetProperty("params");
                    var toolName = paramsEl.GetProperty("name").GetString();
                    var arguments = paramsEl.TryGetProperty("arguments", out var argsEl) ? argsEl : default;

                    string result;
                    bool isError;
                    byte[]? imagePng = null;
                    await _toolLock.WaitAsync();
                    try
                    {
                        var call = HandleToolCall(toolName, arguments);
                        result = call.Text;
                        isError = call.IsError;
                        imagePng = call.ImagePng;
                    }
                    finally
                    {
                        _toolLock.Release();
                    }

                    // MCP content blocks: text first, optional base64 PNG for vision agents.
                    var contentBlocks = new List<object>
                    {
                        new { type = "text", text = result }
                    };
                    if (imagePng != null && imagePng.Length > 0)
                    {
                        contentBlocks.Add(new
                        {
                            type = "image",
                            data = Convert.ToBase64String(imagePng),
                            mimeType = "image/png"
                        });
                    }

                    await SendResponseAsync(writer, id, new
                    {
                        content = contentBlocks.ToArray(),
                        isError
                    });
                }
                else
                {
                    // JSON-RPC 2.0: notifications (requests without id) must NOT be answered.
                    if (hasId)
                    {
                        await SendErrorAsync(writer, id, -32601, "Method not found");
                    }
                }
            }
            catch (JsonException jex)
            {
                // Malformed JSON -> JSON-RPC parse error (-32700). We cannot know the id.
                await writer.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = (object?)null,
                    error = new { code = -32700, message = jex.Message }
                }));
            }
            catch (Exception ex)
            {
                // Internal error (-32603). Echo a null id since parsing the original id failed.
                await writer.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = (object?)null,
                    error = new { code = -32603, message = ex.Message }
                }));
            }
        }
    }

    private static object[] GetTools()
    {
        return new object[]
        {
            new
            {
                name = "launch_application",
                description = "Launches a desktop application (.exe) and initializes UI automation driver.",
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
                name = "inspect_screen",
                description = "Returns a compact, token-minimized representation of the interactive UI elements on screen. Costs ~90% fewer tokens than raw UI trees or screenshots.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        format = new { type = "string", @enum = new[] { "text", "json" }, description = "Format: 'text' (default, minimal tokens) or 'json'." }
                    }
                }
            },
            new
            {
                name = "execute_plan",
                description = "Executes a multi-step automation plan in a SINGLE tool call with self-healing locators and UI state delta tracking. Avoids expensive step-by-step chat round trips.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        plan_json = new
                        {
                            type = "string",
                            description = "JSON array of actions. Actions: 'click', 'type', 'select', 'check', 'uncheck', 'double_click', 'right_click', 'press_key', 'send_keys', 'hover', 'wait' (optional per-step \"timeout\" seconds), 'assert_text'. All locator actions self-heal approximate IDs. Example: [{\"action\":\"type\",\"id\":\"FirstNameTextBox\",\"value\":\"John\"},{\"action\":\"click\",\"id\":\"SubmitButton\"}]"
                        }
                    },
                    required = new[] { "plan_json" }
                }
            },
            new
            {
                name = "wait_for_element",
                description = "Polls the UI until an element with the given automation ID appears (visible/exists). Useful after navigation, dialogs, or async operations. Returns immediately when found.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string", description = "Automation ID of the element to wait for." },
                        timeout_seconds = new { type = "integer", description = "Maximum seconds to wait. Default: 10." }
                    },
                    required = new[] { "id" }
                }
            },
            new
            {
                name = "get_element_text",
                description = "Returns the current text/value of a specific UI element by its automation ID. Lightweight read — does not re-scan the whole screen.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string", description = "Automation ID of the element whose text to retrieve." }
                    },
                    required = new[] { "id" }
                }
            },
            new
            {
                name = "take_screenshot",
                description = "Captures the application window as a PNG. Returns the saved file path; when include_image=true it ALSO returns a base64 image content block for vision-capable agents. Use sparingly — prefer inspect_screen to save tokens.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        output_path = new { type = "string", description = "Optional file path to save the screenshot (e.g. C:\\screenshots\\step1.png). If omitted, a timestamped file is created in the system temp folder." },
                        include_image = new { type = "boolean", description = "When true, the PNG is also returned inline as a base64 MCP image content block." }
                    }
                }
            },
            new
            {
<<<<<<< HEAD
                name = "attach_to_process",
                description = "Attaches to an ALREADY-RUNNING application process (by process_id or process_name) instead of launching a new instance. Enables session reuse so repeated CLI/MCP calls do not relaunch the app.",
=======
                name = "record_session",
                description = "Executes a multi-step plan AND records every action + result to a JSON session file for ZERO-TOKEN future replay. Use this the first time you automate a flow.",
>>>>>>> f70b1d2
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
<<<<<<< HEAD
                        process_id = new { type = "integer", description = "OS process id to attach to." },
                        process_name = new { type = "string", description = "Executable name (with or without .exe) of a running process to attach to. Used when process_id is absent." }
                    }
=======
                        plan_json = new
                        {
                            type = "string",
                            description = "JSON array of actions (same format as execute_plan)."
                        },
                        session_path = new { type = "string", description = "Optional output path for the session JSON file. Auto-generated in temp folder if omitted." },
                        label = new { type = "string", description = "Optional human-readable label for this session (e.g. 'login flow')." }
                    },
                    required = new[] { "plan_json" }
>>>>>>> f70b1d2
                }
            },
            new
            {
<<<<<<< HEAD
                name = "list_windows",
                description = "Lists all top-level windows of the automated application, including modal dialogs (MessageBox, file dialogs, secondary windows).",
=======
                name = "replay_session",
                description = "Replays a previously recorded session JSON file with ZERO token cost — no LLM needed. Uses self-healing locators to tolerate minor UI changes.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        session_path = new { type = "string", description = "Path to the session JSON file produced by record_session." }
                    },
                    required = new[] { "session_path" }
                }
            },
                        new
            {
                name = "list_windows",
                description = "Returns a list of all currently open top-level windows for the application. Useful for multi-window flows and dialog detection.",
>>>>>>> f70b1d2
                inputSchema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "switch_window",
<<<<<<< HEAD
                description = "Switches automation context to the application window whose title contains the given text (case-insensitive). Use after a dialog opens to bring it to the foreground and target its elements.",
=======
                description = "Switches the active UI automation context to a window matching the specified title (exact or partial substring).",
>>>>>>> f70b1d2
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
<<<<<<< HEAD
                        title = new { type = "string", description = "Substring of the target window title (e.g. Save As, Confirm)." }
=======
                        title = new { type = "string", description = "Title or substring of the window to focus and automate." },
                        timeout_seconds = new { type = "integer", description = "Optional timeout in seconds. Default: 5." }
>>>>>>> f70b1d2
                    },
                    required = new[] { "title" }
                }
            },
<<<<<<< HEAD
            new
=======
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
>>>>>>> f70b1d2
            {
                name = "close_application",
                description = "Closes the running application and frees automation resources.",
                inputSchema = new { type = "object", properties = new { } }
            }
        };
    }

    private static ToolCallOutcome HandleToolCall(string? toolName, JsonElement arguments)
    {
        try
        {
            switch (toolName)
            {
                case "launch_application":
                    var path = arguments.TryGetProperty("path", out var p) ? p.GetString() : null;
                    var args = arguments.TryGetProperty("arguments", out var a) ? a.GetString() : null;

                    if (string.IsNullOrEmpty(path))
                    {
                        return ToolCallOutcome.Fail("Error: Application path is required.");
                    }

<<<<<<< HEAD
                    _driver?.Dispose();
                    _driver = DriverFactory.CreateFlauiDriver();
                    _driver.Initialize(path, args);
                    _engine = new AgentAutomationEngine(_driver);
=======
                var driverType = arguments.TryGetProperty("driver", out var dt) ? dt.GetString() ?? "flaui" : "flaui";

                _driver?.Dispose();
                _driver = DriverFactory.CreateDriver(driverType);
                _driver.Initialize(path, args);
                _engine = new AgentAutomationEngine(_driver);
>>>>>>> f70b1d2

                    return ToolCallOutcome.Ok($"Application launched successfully: {path}");

                case "inspect_screen":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var format = arguments.TryGetProperty("format", out var f) ? f.GetString() ?? "text" : "text";
                    return ToolCallOutcome.Ok(_engine.InspectScreen(format));

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

                    var planSummary = _engine.ExecutePlan(plan);
                    // Batch summary starts with FAILED when any step failed -> surface as tool error.
                    return planSummary.StartsWith("FAILED", StringComparison.Ordinal)
                        ? ToolCallOutcome.Fail(planSummary)
                        : ToolCallOutcome.Ok(planSummary);

                case "wait_for_element":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var waitId = arguments.TryGetProperty("id", out var wi) ? wi.GetString() : null;
                    if (string.IsNullOrEmpty(waitId))
                    {
                        return ToolCallOutcome.Fail("Error: id is required.");
                    }

                    var timeoutSec = arguments.TryGetProperty("timeout_seconds", out var ts) ? ts.GetInt32() : 10;
                    var waitResult = _engine.WaitForElement(waitId, timeoutSec);
                    return waitResult.StartsWith("Timeout", StringComparison.Ordinal)
                        ? ToolCallOutcome.Fail(waitResult)
                        : ToolCallOutcome.Ok(waitResult);

                case "get_element_text":
                    if (_engine == null)
                    {
                        return ToolCallOutcome.Fail("Error: No application is currently launched. Use launch_application or attach_to_process first.");
                    }

                    var textId = arguments.TryGetProperty("id", out var ti) ? ti.GetString() : null;
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

<<<<<<< HEAD
                case "attach_to_process":
                    var pidEl = arguments.TryGetProperty("process_id", out var pidProp) && pidProp.ValueKind == JsonValueKind.Number
                        ? (int?)pidProp.GetInt32()
                        : null;
                    var procName = arguments.TryGetProperty("process_name", out var pnProp) ? pnProp.GetString() : null;
=======
            case "record_session":
                if (_engine == null)
                {
                    return "Error: No application is currently launched. Use launch_application first.";
                }

                var recPlan = arguments.GetProperty("plan_json").GetString();
                if (string.IsNullOrEmpty(recPlan))
                {
                    return "Error: plan_json is required.";
                }

                var recPath  = arguments.TryGetProperty("session_path", out var rp) ? rp.GetString() : null;
                var recLabel = arguments.TryGetProperty("label", out var rl) ? rl.GetString() : null;
                return _engine.ExecuteAndRecord(recPlan, recPath, recLabel);

            case "replay_session":
                if (_engine == null)
                {
                    return "Error: No application is currently launched. Use launch_application first.";
                }

                var replayPath = arguments.TryGetProperty("session_path", out var sp) ? sp.GetString() : null;
                if (string.IsNullOrEmpty(replayPath))
                {
                    return "Error: session_path is required.";
                }

                return _engine.ReplaySession(replayPath);

            case "list_windows":
                if (_engine == null)
                {
                    return "Error: No application is currently launched. Use launch_application first.";
                }
                return _engine.ListWindows();

            case "switch_window":
                if (_engine == null)
                {
                    return "Error: No application is currently launched. Use launch_application first.";
                }

                var winTitle = arguments.GetProperty("title").GetString();
                if (string.IsNullOrEmpty(winTitle))
                {
                    return "Error: title is required.";
                }

                var winTimeout = arguments.TryGetProperty("timeout_seconds", out var wt) ? wt.GetInt32() : 5;
                return _engine.SwitchWindow(winTitle, winTimeout);

            case "export_session_as_test":
                var expSessionPath = arguments.GetProperty("session_path").GetString();
                if (string.IsNullOrEmpty(expSessionPath))
                {
                    return "Error: session_path is required.";
                }

                var expFormat = arguments.TryGetProperty("format", out var ef) ? ef.GetString() ?? "csharp" : "csharp";
                // Engine or standalone generator can be called directly
                var engineToUse = _engine ?? new AgentAutomationEngine(DriverFactory.CreateFlauiDriver());
                return engineToUse.ExportSessionAsTest(expSessionPath, expFormat);

            case "scroll":
                if (_engine == null) return "Error: No application is currently launched. Use launch_application first.";
                var sDir = arguments.TryGetProperty("direction", out var sd) ? sd.GetString() ?? "down" : "down";
                var sId = arguments.TryGetProperty("id", out var si) ? si.GetString() : null;
                return _engine.Scroll(sDir, sId);

            case "scroll_into_view":
                if (_engine == null) return "Error: No application is currently launched. Use launch_application first.";
                var svId = arguments.GetProperty("id").GetString();
                if (string.IsNullOrEmpty(svId)) return "Error: id is required.";
                return _engine.ScrollIntoView(svId);

            case "close_application":
                _driver?.Dispose();
                _driver = null;
                _engine = null;
                return "Application closed successfully.";
>>>>>>> f70b1d2

                    if (pidEl == null && string.IsNullOrEmpty(procName))
                    {
                        return ToolCallOutcome.Fail("Error: either process_id or process_name is required.");
                    }

                    _driver?.Dispose();
                    _driver = DriverFactory.CreateFlauiDriver();
                    try
                    {
                        // Attach does not launch a new instance — it reuses the running process.
                        var attachEngine = new AgentAutomationEngine(_driver);
                        var attachResult = attachEngine.Attach(pidEl, procName);
                        _engine = attachEngine;
                        return attachResult.StartsWith("Error", StringComparison.Ordinal)
                            ? ToolCallOutcome.Fail(attachResult)
                            : ToolCallOutcome.Ok(attachResult);
                    }
                    catch (Exception ex)
                    {
                        _driver.Dispose();
                        _driver = null;
                        _engine = null;
                        return ToolCallOutcome.Fail($"Error attaching to process: {ex.Message}");
                    }

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
                    var titleArg = arguments.TryGetProperty("title", out var ta) ? ta.GetString() : null;
                    if (string.IsNullOrEmpty(titleArg))
                    {
                        return ToolCallOutcome.Fail("Error: title is required.");
                    }
                    var switchResult = _engine.SwitchWindow(titleArg);
                    return switchResult.StartsWith("Error")
                        ? ToolCallOutcome.Fail(switchResult)
                        : ToolCallOutcome.Ok(switchResult);

                case "close_application":
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
            // Tool-level failures must be reported via isError, not JSON-RPC errors,
            // so the agent can read the message and self-correct instead of the transport dying.
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
