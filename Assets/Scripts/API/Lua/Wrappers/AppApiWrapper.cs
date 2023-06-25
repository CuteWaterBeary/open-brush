﻿using System.IO;
using MoonSharp.Interpreter;
using ODS;
using UnityAsyncAwaitUtil;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharpUserData]
    public static class AppApiWrapper
    {
        [LuaDocsDescription("The time in seconds since Open Brush was launched")]
        public static float time => UnityEngine.Time.realtimeSinceStartup;

        [LuaDocsDescription("The number of frames that have been rendered since Open Brush was launched")]
        public static float frames => UnityEngine.Time.frameCount;

        [LuaDocsDescription("Determines if physics simulation is active")]
        public static bool Physics(bool active) => UnityEngine.Physics.autoSimulation = active;

        [LuaDocsDescription("The current scale of the scene")]
        public static float currentScale => App.Scene.Pose.scale;

        [LuaDocsDescription("Undo the last action")]
        public static void Undo() => ApiMethods.Undo();

        [LuaDocsDescription("Redo the previously undone action")]
        public static void Redo() => ApiMethods.Redo();

        [LuaDocsDescription("Add a listener")]
        public static void AddListener(string a) => ApiMethods.AddListener(a);

        [LuaDocsDescription("Reset all panels")]
        public static void ResetPanels() => ApiMethods.ResetAllPanels();

        [LuaDocsDescription("Show the user scripts folder")]
        public static void ShowScriptsFolder() => ApiMethods.OpenUserScriptsFolder();

        [LuaDocsDescription("Show the export folder")]
        public static void ShowExportFolder() => ApiMethods.OpenExportFolder();

        [LuaDocsDescription("Show the sketches folder")]
        public static void ShowSketchesFolder(int a) => ApiMethods.ShowSketchFolder(a);

        [LuaDocsDescription("Activate or deactivate straight edge mode")]
        public static void StraightEdge(bool active) => LuaApiMethods.StraightEdge(active);

        [LuaDocsDescription("Activate or deactivate auto orientation mode")]
        public static void AutoOrient(bool active) => LuaApiMethods.AutoOrient(active);

        [LuaDocsDescription("Activate or deactivate view only mode")]
        public static void ViewOnly(bool active) => LuaApiMethods.ViewOnly(active);

        [LuaDocsDescription("Activate or deactivate auto simplification mode")]
        public static void AutoSimplify(bool active) => LuaApiMethods.AutoSimplify(active);

        [LuaDocsDescription("Activate or deactivate disco mode")]
        public static void Disco(bool active) => LuaApiMethods.Disco(active);

        [LuaDocsDescription("Activate or deactivate profiling mode")]
        public static void Profiling(bool active) => LuaApiMethods.Profiling(active);

        [LuaDocsDescription("Activate or deactivate post-processing")]
        public static void PostProcessing(bool active) => LuaApiMethods.PostProcessing(active);

        [LuaDocsDescription("Set the drafting mode to visible")]
        public static void DraftingVisible() => ApiMethods.DraftingVisible();

        [LuaDocsDescription("Set the drafting mode to transparent")]
        public static void DraftingTransparent() => ApiMethods.DraftingTransparent();

        [LuaDocsDescription("Set the drafting mode to hidden")]
        public static void DraftingHidden() => ApiMethods.DraftingHidden();

        [LuaDocsDescription("Get or set the current environment")]
        public static string environment
        {
            get => SceneSettings.m_Instance.CurrentEnvironment.Description;
            set => ApiMethods.SetEnvironment(value);
        }

        [LuaDocsDescription("Activate or deactivate the watermark")]
        public static void Watermark(bool active) => LuaApiMethods.Watermark(active);

        // TODO Unified API for tools and panels
        // public static void SettingsPanel(bool active) => )LuaApiMethods.SettingsPanel)(active);
        // public static void SketchOrigin(bool active) => )LuaApiMethods.SketchOrigin)(active);

        [LuaDocsDescription("Get or set the clipboard text")]
        public static string clipboardText
        {
            get => SystemClipboard.GetClipboardText();
            set => SystemClipboard.SetClipboardText(value);
        }
        
        // public static Texture2D clipboardImage {
        //     get => SystemClipboard.GetClipboardImage();
        //     // set => SystemClipboard.SetClipboardImage(value);
        // }

        [LuaDocsDescription("Read the contents of a file")]
        public static string ReadFile(string path)
        {
            bool valid = false;
            // Disallow absolute paths
            valid = !Path.IsPathRooted(path);
            if (valid)
            {
                path = Path.Join(ApiManager.Instance.UserScriptsPath(), path);
                // Check path is a subdirectory of User folder
                valid = _IsSubdirectory(path, App.UserPath());
            }
            if (!valid)
            {
                // TODO think long and hard about security
                Debug.LogWarning($"Path is not a subdirectory of User folder: {path}");
                return null;
            }

            Stream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            string contents;
            using (var sr = new StreamReader(fileStream)) contents = sr.ReadToEnd();
            fileStream.Close();

            return contents;
        }

        [LuaDocsDescription("Log a Lua error message")]
        public static void Error(string message) => LuaManager.Instance.LogLuaErrorRaisedByScript(message);

        [LuaDocsDescription("Set the font for the text")]
        public static void SetFont(string fontData) => ApiManager.Instance.SetTextFont(fontData);

        [LuaDocsDescription("Take a snapshot of your scene and save it to your Snapshots folder")]
        [LuaDocsExample(@"App:TakeSnapshop(Transform:New(0, 12, 3), ""mysnapshot.png"", 1024, 768, true)")]
        [LuaDocsParameter("tr", "Determines the position and orientation of the camera used to take the snapshot")]
        [LuaDocsParameter("filename", "The filename to use for the saved snapshot")]
        [LuaDocsParameter("width", "Image width")]
        [LuaDocsParameter("height", "Image height")]
        public static void TakeSnapshot(TrTransform tr, string filename, int width, int height, float superSampling = 1f)
        {
            bool saveAsPng;
            if (filename.ToLower().EndsWith(".jpg") || filename.ToLower().EndsWith(".jpeg"))
            {
                saveAsPng = false;
            }
            else if (filename.ToLower().EndsWith(".png"))
            {
                saveAsPng = true;
            }
            else
            {
                saveAsPng = false;
                filename += ".jpg";
            }
            string path = Path.Join(App.SnapshotPath(), filename);
            MultiCamTool cam = SketchSurfacePanel.m_Instance.GetToolOfType(BaseTool.ToolType.MultiCamTool) as MultiCamTool;

            if (cam != null)
            {
                var rig = SketchControlsScript.m_Instance.MultiCamCaptureRig;
                App.Scene.AsScene[rig.gameObject.transform] = tr;
                var rMgr = rig.ManagerFromStyle(
                    MultiCamStyle.Snapshot
                );
                var initialState = rig.gameObject.activeSelf;
                rig.gameObject.SetActive(true);
                RenderTexture tmp = rMgr.CreateTemporaryTargetForSave(width, height);
                RenderWrapper wrapper = rMgr.gameObject.GetComponent<RenderWrapper>();
                float ssaaRestore = wrapper.SuperSampling;
                wrapper.SuperSampling = superSampling;
                rMgr.RenderToTexture(tmp);
                wrapper.SuperSampling = ssaaRestore;
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    ScreenshotManager.Save(fs, tmp, bSaveAsPng: saveAsPng);
                }
                rig.gameObject.SetActive(initialState);
            }
        }

        [LuaDocsDescription("Take a 360-degree snapshot of the scene and save it")]
        [LuaDocsParameter("tr", "Determines the position and orientation of the camera used to take the snapshot")]
        [LuaDocsParameter("filename", "The filename to use for the saved snapshot")]
        [LuaDocsParameter("width", "The width of the image")]
        public static void Take360Snapshot(TrTransform tr, string filename, int width = 4096)
        {
            var odsDriver = App.Instance.InitOds();
            App.Scene.AsScene[odsDriver.gameObject.transform] = tr;
            odsDriver.FramesToCapture = 1;
            odsDriver.OdsCamera.basename = filename;
            odsDriver.OdsCamera.outputFolder = App.SnapshotPath();
            odsDriver.OdsCamera.imageWidth = width;
            odsDriver.OdsCamera.outputFolder = App.SnapshotPath();
            odsDriver.OdsCamera.SetOdsRendererType(HybridCamera.OdsRendererType.Slice);
            odsDriver.OdsCamera.gameObject.SetActive(true);
            odsDriver.OdsCamera.enabled = true;
            AsyncCoroutineRunner.Instance.StartCoroutine(odsDriver.OdsCamera.Render(odsDriver.transform));
        }

        private static bool _IsSubdirectory(string path, string basePath)
        {
            var relPath = Path.GetRelativePath(
                basePath.Replace('\\', '/'),
                path.Replace('\\', '/')
            );
            return relPath != "." && relPath != ".."
                && !relPath.StartsWith("../")
                && !Path.IsPathRooted(relPath);
        }
    }
}