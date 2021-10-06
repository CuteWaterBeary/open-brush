﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using TiltBrush;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;


public class ApiManager : MonoBehaviour
{
    private const string ROOT_API_URL = "/api/v1";
    private const string BASE_USER_SCRIPTS_URL = "/scripts";
    private const string BASE_EXAMPLE_SCRIPTS_URL = "/examplescripts";
    private const string BASE_HTML = @"<!doctype html><html lang='en'>
<head><meta charset='UTF-8'></head>
<body>{0}</body></html>";


    private FileWatcher m_FileWatcher;
    private string m_UserScriptsPath;
    private Queue m_RequestedCommandQueue = Queue.Synchronized(new Queue());
    private Queue m_OutgoingCommandQueue = Queue.Synchronized(new Queue());
    private List<Uri> m_OutgoingApiListeners;
    private static ApiManager m_Instance;
    private Dictionary<string, ApiEndpoint> endpoints;
    private byte[] CameraViewPng;

    private bool cameraViewRequested;
    private bool cameraViewGenerated;

    [NonSerialized] public Vector3 BrushOrigin = new Vector3(0, 13, 3);
    [NonSerialized] public Quaternion BrushInitialRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
    [NonSerialized] public Vector3 BrushPosition = new Vector3(0, 13, 3);  // Good origin for monoscopic
    [NonSerialized] public Quaternion BrushRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
    private Dictionary<string, string> m_UserScripts;
    private Dictionary<string, string> m_ExampleScripts;

    public static ApiManager Instance
    {
        get { return m_Instance; }
    }
    [NonSerialized] public Stack<(Vector3, Quaternion)> BrushTransformStack;
    [NonSerialized] public Dictionary<string, string> CommandExamples;

    public string UserScriptsPath() { return m_UserScriptsPath; }

    void Awake()
    {
        m_Instance = this;
        m_UserScriptsPath = Path.Combine(App.UserPath(), "Scripts");
        App.HttpServer.AddHttpHandler($"/help", InfoCallback);
        App.HttpServer.AddHttpHandler($"/help/commands", InfoCallback);
        App.HttpServer.AddHttpHandler($"/help/brushes", InfoCallback);
        App.HttpServer.AddRawHttpHandler("/cameraview", CameraViewCallback);
        PopulateApi();
        m_UserScripts = new Dictionary<string, string>();
        m_ExampleScripts = new Dictionary<string, string>();
        PopulateExampleScripts();
        PopulateUserScripts();
        BrushTransformStack = new Stack<(Vector3, Quaternion)>();
        if (!Directory.Exists(m_UserScriptsPath))
        {
            Directory.CreateDirectory(m_UserScriptsPath);
        }
        if (Directory.Exists(m_UserScriptsPath))
        {
            m_FileWatcher = new FileWatcher(m_UserScriptsPath, "*.html");
            m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
            m_FileWatcher.FileChanged += OnScriptsDirectoryChanged;
            m_FileWatcher.FileCreated += OnScriptsDirectoryChanged;
            // m_FileWatcher.FileDeleted += OnScriptsDirectoryChanged; TODO
            m_FileWatcher.EnableRaisingEvents = true;
        }

        CommandExamples = new Dictionary<string, string>
        {
            {"draw.paths", "[[0,0,0],[1,0,0],[1,1,0]],[[0,0,-1],[-1,0,-1],[-1,1,-1]]"},
            {"draw.path", "[0,0,0],[1,0,0],[1,1,0],[0,1,0]"},
            {"draw.stroke", "[0,0,0,0,180,90,.75],[1,0,0,0,180,90,.75],[1,1,0,0,180,90,.75],[0,1,0,0,180,90,.75]"},
            {"listenfor.strokes", "http://localhost:8000/"},
            {"draw.polygon", "5,1,0"},
            {"draw.text", "hello"},
            {"draw.svg", "M 184,199 116,170 53,209.6 60,136.2 4.3,88"},
            {"brush.type", "ink"},
            {"color.add.hsv", "0.1,0.2,0.3"},
            {"color.add.rgb", "0.1,0.2,0.3"},
            {"color.set.rgb", "0.1,0.2,0.3"},
            {"color.set.hsv", "0.1,0.2,0.3"},
            {"color.set.html", "darkblue"},
            {"brush.size.set", ".5"},
            {"brush.size.add", ".1"},
            {"spectator.move.to", "1,1,1"},
            {"spectator.move.by", "1,1,1"},
            {"spectator.turn.y", "45"},
            {"spectator.turn.x", "45"},
            {"spectator.turn.z", "45"},
            {"spectator.direction", "45,45,0"},
            {"spectator.look.at", "1,2,3"},
            {"spectator.mode", "circular"},
            {"user.move.to", "1,1,1"},
            {"user.move.by", "1,1,1"},
            {"brush.move.to", "1,1,1"},
            {"brush.move.by", "1,1,1"},
            {"brush.move", "1"},
            {"brush.draw", "1"},
            {"brush.turn.y", "45"},
            {"brush.turn.x", "45"},
            {"brush.turn.z", "45"},
            {"brush.look.at", "1,1,1"},
            {"stroke.delete", "0"},
            {"stroke.select", "0"},
            {"strokes.select", "0,3"},
            {"selection.trim", "2"},
            {"selection.points.addnoise", "x,0.5"},
            {"selection.points.quantize", "0.1"},
            {"strokes.join", "0,2"},
            {"stroke.add", "0"},
            {"load.user", "0"},
            {"load.curated", "0"},
            {"load.liked", "0"},
            {"load.drive", "0"},
            {"load.named", "mysketch.sketch"},
            {"showfolder.sketch", "0"},
            {"import.model", "example.glb"}
        };

        App.Instance.StateChanged += RunStartupScript;

    }

    public void RunStartupScript(App.AppState oldState, App.AppState newState)
    {

        if (!(oldState == App.AppState.LoadingBrushesAndLighting && newState == App.AppState.Standard)) return;

        var startupScriptPath = Path.Combine(m_UserScriptsPath, "startup.sketchscript");

        if (File.Exists(startupScriptPath))
        {
            var lines = File.ReadAllLines(startupScriptPath);
            Debug.Log($"Found startup script with {lines.Length} lines");
            foreach (string pair in lines)
            {
                EnqueueCommandString(pair);
            }
        }
        else
        {
            Debug.Log($"No startup script");
        }

    }

    private void EnqueueCommandString(string commandString)
    {
        string[] commandPair = commandString.Split(new[] { '=' }, 2);
        if (commandPair.Length == 1 && commandPair[0] != "")
        {
            Debug.Log($"Queuing {commandPair[0]}");
            m_RequestedCommandQueue.Enqueue(
                new KeyValuePair<string, string>(commandPair[0], "")
            );
        }
        else if (commandPair.Length == 2)
        {
            Debug.Log($"Queuing {commandPair[0]}={commandPair[1]}");
            m_RequestedCommandQueue.Enqueue(
                new KeyValuePair<string, string>(
                        commandPair[0],
                        UnityWebRequest.UnEscapeURL(commandPair[1]
                    )
                )
            );
        }
    }
    private void OnScriptsDirectoryChanged(object sender, FileSystemEventArgs e)
    {
        var fileinfo = new FileInfo(e.FullPath);
        RegisterUserScript(fileinfo);
    }

    private string InfoCallback(HttpListenerRequest request)
    {
        string html;
        StringBuilder builder;
        switch (request.Url.Segments.Last())
        {
            case "commands":

                if (request.Url.Query.Contains("raw"))
                {
                    html = String.Join("\n", endpoints.Keys);
                }
                else if (request.Url.Query.Contains("json"))
                {
                    html = JsonConvert.SerializeObject(ListApiCommands(), Formatting.Indented);
                }
                else
                {
                    var commandList = ListApiCommandsAsStrings();
                    builder = new StringBuilder("<h3>Open Brush API Commands</h3>");
                    builder.AppendLine("<p>To run commands a request to this url with http://localhost:40074/api/v1?</p>");
                    builder.AppendLine("<p>Commands are querystring parameters: commandname=parameters</p>");
                    builder.AppendLine("<p>Separate multiple commands with &</p>");
                    builder.AppendLine("<p>Example: <a href='http://localhost:40074/api/v1?brush.turn.y=45&brush.draw=1'>http://localhost:40074/api/v1?brush.turn.y=45&brush.draw=1</a></p>");
                    builder.AppendLine("<dl>");
                    foreach (var key in commandList.Keys)
                    {
                        string paramList = commandList[key].Item1;
                        if (paramList != "")
                        {
                            paramList = $"({paramList})";
                        }
                        builder.AppendLine($@"<dt><strong>{key}</strong> {paramList}
 <a href=""http://localhost:40074/api/v1?{getCommandExample(key)}"" target=""_blank"">Try it</a></dt>
<dd>{commandList[key].Item2}<br><br></dd>");
                    }
                    builder.AppendLine("</dl>");
                    html = String.Format(BASE_HTML, builder);
                }
                break;
            case "brushes":
                var brushes = BrushCatalog.m_Instance.AllBrushes.Where(x => x.DurableName != "");
                if (request.Url.Query.Contains("raw"))
                {
                    html = String.Join("\n", brushes.Select(x => x.DurableName));
                }
                else
                {
                    builder = new StringBuilder("<h3>Open Brush Brushes</h3>");
                    builder.AppendLine("<ul>");
                    foreach (var b in brushes)
                    {
                        builder.AppendLine($"<li>{b.DurableName}</li>");
                    }
                    builder.AppendLine("</ul>");
                    html = String.Format(BASE_HTML, builder);
                }
                break;
            case "help":
            default:
                html = $@"<h3>Open Brush API Help</h3>
<ul>
<li>List of API commands: <a href='/help/commands'>/help/commands</a></li>
<li>List of brushes: <a href='/help/brushes'>/help/brushes</a></li>
<li>User Scripts: <a href='{BASE_USER_SCRIPTS_URL}'>{BASE_USER_SCRIPTS_URL}</a></li>
<li>Example Scripts: <a href='{BASE_EXAMPLE_SCRIPTS_URL}'>{BASE_EXAMPLE_SCRIPTS_URL}</a></li>
</ul>";
                break;
        }
        return html;
    }

    private string getCommandExample(string key)
    {
        if (CommandExamples.ContainsKey(key))
        {
            return $"{key}={CommandExamples[key]}";
        }
        else
        {
            return key;
        }
    }

    private void PopulateExampleScripts()
    {
        App.HttpServer.AddHttpHandler(BASE_EXAMPLE_SCRIPTS_URL, ExampleScriptsCallback);
        var exampleScripts = Resources.LoadAll("ScriptExamples", typeof(TextAsset));
        foreach (TextAsset htmlFile in exampleScripts)
        {
            string filename = $"{BASE_EXAMPLE_SCRIPTS_URL}/{htmlFile.name}.html";
            m_ExampleScripts[filename] = htmlFile.ToString();
            App.HttpServer.AddHttpHandler(filename, ExampleScriptsCallback);
        }
    }

    private void PopulateUserScripts()
    {
        App.HttpServer.AddHttpHandler(BASE_USER_SCRIPTS_URL, UserScriptsCallback);
        if (!Directory.Exists(m_UserScriptsPath))
        {
            Directory.CreateDirectory(m_UserScriptsPath);
        }
        if (Directory.Exists(m_UserScriptsPath))
        {
            var dirInfo = new DirectoryInfo(m_UserScriptsPath);
            FileInfo[] AllFileInfo = dirInfo.GetFiles();
            foreach (FileInfo fileinfo in AllFileInfo)
            {
                RegisterUserScript(fileinfo);
            }
        }
    }

    private void RegisterUserScript(FileInfo file)
    {
        if (file.Extension == ".html" || file.Extension == ".htm")
        {
            var f = file.OpenText();
            string filename = $"{BASE_USER_SCRIPTS_URL}/{file.Name}";
            m_UserScripts[filename] = f.ReadToEnd();
            f.Close();
            if (!App.HttpServer.HttpHandlerExists(filename))
            {
                App.HttpServer.AddHttpHandler(filename, UserScriptsCallback);
            }
        }
    }

    private void PopulateApi()
    {
        endpoints = new Dictionary<string, ApiEndpoint>();
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(t => t.GetTypes())
            .Where(t => t.IsClass && t.Namespace == "TiltBrush");

        foreach (var type in types)
        {
            foreach (MethodInfo methodInfo in type.GetMethods())
            {
                var attrs = Attribute.GetCustomAttributes(methodInfo, typeof(ApiEndpoint));
                foreach (Attribute attr in attrs)
                {
                    ApiEndpoint apiEndpoint = (ApiEndpoint)attr;
                    bool valid = false;
                    if (type.IsAbstract && type.IsSealed) // therefore is static
                    {
                        apiEndpoint.instance = null;
                        valid = true;
                    }
                    else if (type.IsSubclassOf(typeof(MonoBehaviour)))
                    {
                        apiEndpoint.instance = FindObjectOfType(type);
                        if (apiEndpoint.instance != null)
                        {
                            valid = true;
                        }
                        else
                        {
                            Debug.LogWarning($"No instance found for ApiEndpoint on: {type}");
                        }
                    }

                    if (valid)
                    {
                        apiEndpoint.type = type;
                        apiEndpoint.methodInfo = methodInfo;
                        apiEndpoint.parameterInfo = methodInfo.GetParameters();
                        endpoints[apiEndpoint.Endpoint] = apiEndpoint;
                    }
                    else
                    {
                        Debug.LogWarning($"ApiEndpoint declared on invalid class: {type}");
                    }
                }
            }
        }
        App.HttpServer.AddHttpHandler(ROOT_API_URL, ApiCommandCallback);
    }

    public bool InvokeEndpoint(KeyValuePair<string, string> command)
    {
        if (endpoints.ContainsKey(command.Key))
        {
            var endpoint = endpoints[command.Key];
            var parameters = endpoint.DecodeParams(command.Value);
            endpoint.Invoke(parameters);
            return true;
        }
        else
        {
            Debug.LogError($"Invalid API command: {command.Key}");
        }
        return false;
    }
    [ContextMenu("Log Api Commands")]
    public void LogCommandsList()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Please run in play mode");
        }
        else
        {
            var builder = new StringBuilder();
            var commands = ListApiCommandsAsStrings();
            foreach (var k in commands.Keys)
            {
                builder.AppendLine($"{k} ({commands[k].Item2}): {commands[k].Item2}");
            }
        }
    }

    Dictionary<string, (string, string)> ListApiCommandsAsStrings()
    {
        var commandList = new Dictionary<string, (string, string)>();
        foreach (var endpoint in endpoints.Keys)
        {
            var paramInfoText = new List<string>();
            foreach (var param in endpoints[endpoint].parameterInfo)
            {
                string typeName = param.ParameterType.Name
                    .Replace("Single", "float")
                    .Replace("Int32", "int")
                    .Replace("String", "string");
                paramInfoText.Add($"{typeName} {param.Name}");
            }
            string paramInfo = String.Join(", ", paramInfoText);
            commandList[endpoint] = (paramInfo, endpoints[endpoint].Description);
        }
        return commandList;
    }

    Dictionary<string, object> ListApiCommands()
    {
        var commandList = new Dictionary<string, object>();
        foreach (var endpoint in endpoints.Keys)
        {
            commandList[endpoint] = new
            {
                parameters = endpoints[endpoint].ParamsAsDict(),
                description = endpoints[endpoint].Description
            };
        }
        return commandList;
    }

    private string UserScriptsCallback(HttpListenerRequest request)
    {
        string html;
        if (request.Url.Segments.Length == 2)
        {
            var builder = new StringBuilder("<h3>Open Brush User Scripts</h3>");
            builder.AppendLine("<ul>");
            foreach (var e in m_UserScripts)
            {
                builder.AppendLine($"<li><a href='{e.Key}'>{e.Key}</a></li>");
            }

            // Only show this button on Windows
            // TODO Update this is ApiMethods.OpenUserFolder is ever cross platform
            // (Also see similar global commands that will need updating)
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                builder.AppendLine($"<button onclick=\"fetch('{ROOT_API_URL}?showfolder.scripts');\">Open Scripts Folder</button>");
            }
            builder.AppendLine("</ul>");
            html = String.Format(BASE_HTML, builder);
        }
        else
        {
            html = m_UserScripts[Uri.UnescapeDataString(request.Url.AbsolutePath)];
        }
        return ScriptTemplateSubstitution(html);
    }

    private string ExampleScriptsCallback(HttpListenerRequest request)
    {
        string html;
        if (request.Url.Segments.Length == 2)
        {
            var builder = new StringBuilder("<h3>Open Brush Example Scripts</h3>");
            builder.AppendLine("<ul>");
            foreach (var e in m_ExampleScripts)
            {
                builder.AppendLine($"<li><a href='{e.Key}'>{e.Key}</a></li>");
            }
            builder.AppendLine("</ul>");
            html = String.Format(BASE_HTML, builder);
        }
        else
        {
            html = m_ExampleScripts[Uri.UnescapeDataString(request.Url.AbsolutePath)];
        }
        return ScriptTemplateSubstitution(html);
    }

    private string ScriptTemplateSubstitution(string html)
    {
        string[] brushNameList = BrushCatalog.m_Instance.AllBrushes
            .Where(x => x.m_Description != "")
            .Where(x => x.m_SupersededBy == null)
            .Select(x => x.m_Description.Replace(" ", "").Replace(".", "").Replace("(", "").Replace(")", ""))
            .ToArray();
        string brushesJson = JsonConvert.SerializeObject(brushNameList);
        html = html.Replace("{{brushesJson}}", brushesJson);

        string[] environmentNameList = EnvironmentCatalog.m_Instance.AllEnvironments
            .Select(x => x.m_Description.Replace(" ", ""))
            .ToArray();
        string environmentsJson = JsonConvert.SerializeObject(environmentNameList);
        html = html.Replace("{{environmentsJson}}", environmentsJson);

        string commandsJson = JsonConvert.SerializeObject(ListApiCommands());
        html = html.Replace("{{commandsJson}}", commandsJson);

        return html;
    }

    string ApiCommandCallback(HttpListenerRequest request)
    {

        KeyValuePair<string, string> command;

        // Handle GET
        foreach (string pair in request.Url.Query.TrimStart('?').Split('&'))
        {
            EnqueueCommandString(pair);
        }

        // Handle POST
        // TODO also accept JSON
        if (request.HasEntityBody)
        {
            using (Stream body = request.InputStream)
            {
                using (var reader = new StreamReader(body, request.ContentEncoding))
                {
                    var formdata = Uri.UnescapeDataString(reader.ReadToEnd());
                    var pairs = formdata.Replace("+", " ").Split('&');
                    foreach (var pair in pairs)
                    {
                        EnqueueCommandString(pair);
                    }
                }
            }
        }

        return "OK";
    }

    public bool HasOutgoingListeners => m_OutgoingApiListeners != null && m_OutgoingApiListeners.Count > 0;

    public void EnqueueOutgoingCommands(List<KeyValuePair<string, string>> commands)
    {
        if (!HasOutgoingListeners) return;
        foreach (var command in commands)
        {
            m_OutgoingCommandQueue.Enqueue(command);
        }
    }

    public void AddOutgoingCommandListener(Uri uri)
    {
        if (m_OutgoingApiListeners == null) m_OutgoingApiListeners = new List<Uri>();
        m_OutgoingApiListeners.Add(uri);
    }

    private void OutgoingApiCommand()
    {
        if (!HasOutgoingListeners) return;

        KeyValuePair<string, string> command;
        try
        {
            command = (KeyValuePair<string, string>)m_OutgoingCommandQueue.Dequeue();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        foreach (var listenerUrl in m_OutgoingApiListeners)
        {
            StartCoroutine(GetRequest($"{listenerUrl}?{command.Key}={command.Value}"));
        }
    }

    IEnumerator GetRequest(string uri)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            yield return webRequest.SendWebRequest();
        }
    }

    private bool HandleApiCommand()
    {
        KeyValuePair<string, string> command;
        try
        {
            command = (KeyValuePair<string, string>)m_RequestedCommandQueue.Dequeue();
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        Debug.Log($"Invoking {command.Key}={command.Value}");
        return Instance.InvokeEndpoint(command);
    }

    private void Update()
    {
        HandleApiCommand();
        OutgoingApiCommand();
        UpdateCameraView();
    }


    IEnumerator ScreenCap()
    {
        yield return new WaitForEndOfFrame();
        var rt = new RenderTexture(Screen.width, Screen.height, 0);
        ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
        var oldTex = RenderTexture.active;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        FlipTextureVertically(tex);
        tex.Apply();
        RenderTexture.active = oldTex;
        CameraViewPng = tex.EncodeToPNG();
        Destroy(tex);

        cameraViewRequested = false;
        cameraViewGenerated = true;
    }

    public static void FlipTextureVertically(Texture2D original)
    {
        // ScreenCap is upside down so flip it
        // Orientation might be platform specific so we might need some logic around this

        var originalPixels = original.GetPixels();

        Color[] newPixels = new Color[originalPixels.Length];

        int width = original.width;
        int rows = original.height;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                newPixels[x + y * width] = originalPixels[x + (rows - y - 1) * width];
            }
        }

        original.SetPixels(newPixels);
        original.Apply();
    }

    private void UpdateCameraView()
    {
        if (cameraViewRequested) StartCoroutine(ScreenCap());
    }

    private HttpListenerContext CameraViewCallback(HttpListenerContext ctx)
    {

        cameraViewRequested = true;
        while (cameraViewGenerated == false)
        {
            Thread.Sleep(5);
        }
        cameraViewGenerated = false;

        ctx.Response.AddHeader("Content-Type", "image/png");
        ctx.Response.ContentLength64 = CameraViewPng.Length;
        try
        {
            if (ctx.Response.OutputStream.CanWrite)
            {
                ctx.Response.OutputStream.Write(CameraViewPng, 0, CameraViewPng.Length);
            }
        }
        catch (SocketException e)
        {
            Debug.LogWarning(e.Message);
        }
        finally
        {
            ctx.Response.Close();
        }
        ctx = null;
        return ctx;
    }


}
