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

    public static async Task Main(string[] args)
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var id = root.TryGetProperty("id", out var idProp) ? idProp.Clone() : default;
                var method = root.TryGetProperty("method", out var methodProp) ? methodProp.GetString() : null;

                if (method == "initialize")
                {
                    await SendResponseAsync(writer, id, new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new { tools = new { } },
                        serverInfo = new { name = "autonex-desktop-mcp", version = "1.3.0" }
                    });
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

                    var result = HandleToolCall(toolName, arguments);
                    await SendResponseAsync(writer, id, new
                    {
                        content = new[]
                        {
                            new { type = "text", text = result }
                        }
                    });
                }
                else
                {
                    // Unknown method fallback response
                    if (id.ValueKind != JsonValueKind.Undefined)
                    {
                        await SendResponseAsync(writer, id, new { error = "Method not found" });
                    }
                }
            }
            catch (Exception ex)
            {
                if (line.Contains("\"id\""))
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        error = new { code = -32603, message = ex.Message }
                    }));
                }
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
                        arguments = new { type = "string", description = "Optional command line arguments." }
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
                            description = "JSON array of actions. Actions: 'click', 'type', 'select', 'check', 'uncheck', 'press_key', 'hover', 'assert_text'. Example: [{\"action\":\"type\",\"id\":\"FirstNameTextBox\",\"value\":\"John\"},{\"action\":\"click\",\"id\":\"SubmitButton\"}]"
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
                description = "Captures the current screen as a PNG file and returns the file path. Use sparingly — prefer inspect_screen to save tokens.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        output_path = new { type = "string", description = "Optional file path to save the screenshot (e.g. C:\\screenshots\\step1.png). If omitted, a timestamped file is created in the system temp folder." }
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
                        plan_json = new
                        {
                            type = "string",
                            description = "JSON array of actions (same format as execute_plan)."
                        },
                        session_path = new { type = "string", description = "Optional output path for the session JSON file. Auto-generated in temp folder if omitted." },
                        label = new { type = "string", description = "Optional human-readable label for this session (e.g. 'login flow')." }
                    },
                    required = new[] { "plan_json" }
                }
            },
            new
            {
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
                inputSchema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "switch_window",
                description = "Switches the active UI automation context to a window matching the specified title (exact or partial substring).",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string", description = "Title or substring of the window to focus and automate." },
                        timeout_seconds = new { type = "integer", description = "Optional timeout in seconds. Default: 5." }
                    },
                    required = new[] { "title" }
                }
            },
                        new
            {
                name = "close_application",
                description = "Closes the running application and frees automation resources.",
                inputSchema = new { type = "object", properties = new { } }
            }
        };
    }

    private static string HandleToolCall(string? toolName, JsonElement arguments)
    {
        switch (toolName)
        {
            case "launch_application":
                var path = arguments.GetProperty("path").GetString();
                var args = arguments.TryGetProperty("arguments", out var a) ? a.GetString() : null;

                if (string.IsNullOrEmpty(path))
                {
                    return "Error: Application path is required.";
                }

                _driver?.Dispose();
                _driver = DriverFactory.CreateFlauiDriver();
                _driver.Initialize(path, args);
                _engine = new AgentAutomationEngine(_driver);

                return $"Application launched successfully: {path}";

            case "inspect_screen":
                if (_engine == null)
                {
                    return "Error: No application is currently launched. Use launch_application first.";
                }

                var format = arguments.TryGetProperty("format", out var f) ? f.GetString() ?? "text" : "text";
                return _engine.InspectScreen(format);

            case "execute_plan":
                if (_engine == null)
                {
                    return "Error: No application is currently launched. Use launch_application first.";
                }

                var plan = arguments.GetProperty("plan_json").GetString();
                if (string.IsNullOrEmpty(plan))
                {
                    return "Error: plan_json is required.";
                }

                return _engine.ExecutePlan(plan);

            case "wait_for_element":
                if (_engine == null)
                {
                    return "Error: No application is currently launched. Use launch_application first.";
                }

                var waitId = arguments.GetProperty("id").GetString();
                if (string.IsNullOrEmpty(waitId))
                {
                    return "Error: id is required.";
                }

                var timeoutSec = arguments.TryGetProperty("timeout_seconds", out var ts) ? ts.GetInt32() : 10;
                return _engine.WaitForElement(waitId, timeoutSec);

            case "get_element_text":
                if (_engine == null)
                {
                    return "Error: No application is currently launched. Use launch_application first.";
                }

                var textId = arguments.GetProperty("id").GetString();
                if (string.IsNullOrEmpty(textId))
                {
                    return "Error: id is required.";
                }

                return _engine.GetElementText(textId);

            case "take_screenshot":
                if (_engine == null)
                {
                    return "Error: No application is currently launched. Use launch_application first.";
                }

                var outPath = arguments.TryGetProperty("output_path", out var op) ? op.GetString() : null;
                return _engine.TakeScreenshot(outPath);

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

            case "close_application":
                _driver?.Dispose();
                _driver = null;
                _engine = null;
                return "Application closed successfully.";

            default:
                return $"Error: Unknown tool '{toolName}'.";
        }
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
}
