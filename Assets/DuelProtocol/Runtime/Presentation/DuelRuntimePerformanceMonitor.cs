using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace DuelProtocol.Presentation
{
    [Serializable]
    public sealed class DuelPerformanceReport
    {
        public string schemaVersion = "1.0";
        public string capturedUtc;
        public string platform;
        public string unityVersion;
        public string graphicsDevice;
        public int screenWidth;
        public int screenHeight;
        public float durationSeconds;
        public int sampleCount;
        public float averageFps;
        public float p95FrameMilliseconds;
        public float maximumFrameMilliseconds;
        public float averageGpuMilliseconds;
        public int gpuSampleCount;
        public long maximumAllocatedMemoryBytes;
        public long maximumReservedMemoryBytes;
        public float batteryLevelAtStart;
        public float batteryLevelAtEnd;
        public string batteryStatusAtEnd;
        public float targetFps;
        public float minimumAcceptableFps;
        public bool targetMet;
        public bool headless;
        public string[] screenshotPaths;
    }

    public static class DuelPerformanceSummary
    {
        public static float Percentile95Milliseconds(IReadOnlyList<float> frameSeconds)
        {
            if (frameSeconds == null || frameSeconds.Count == 0) return 0f;
            var ordered = frameSeconds.OrderBy(value => value).ToArray();
            var index = Mathf.Clamp(
                Mathf.CeilToInt(ordered.Length * 0.95f) - 1,
                0,
                ordered.Length - 1);
            return ordered[index] * 1000f;
        }
    }

    [DisallowMultipleComponent]
    public sealed class DuelRuntimePerformanceMonitor : MonoBehaviour
    {
        private const string EnableArgument = "--duel-performance-seconds";
        private const string OutputArgument = "--duel-performance-output";
        private const string ScreenshotArgument = "--duel-screenshot-directory";
        private const float WarmupSeconds = 3f;
        private readonly List<float> _frameSeconds = new List<float>(3600);
        private readonly List<double> _gpuMilliseconds = new List<double>(3600);
        private float _requestedDuration;
        private float _startedAt;
        private long _maximumAllocated;
        private long _maximumReserved;
        private float _batteryAtStart;
        private string _outputPath;
        private string _screenshotDirectory;
        private readonly List<string> _screenshotPaths = new List<string>(6);
        private int _screenshotIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWhenRequested()
        {
            var args = Environment.GetCommandLineArgs();
            if (!TryGetArgument(args, EnableArgument, out var durationText) ||
                !float.TryParse(durationText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var duration) ||
                duration <= 0f)
            {
                return;
            }
            if (FindAnyObjectByType<DuelRuntimePerformanceMonitor>() != null) return;
            var instance = new GameObject("Duel Runtime Performance Monitor");
            DontDestroyOnLoad(instance);
            var monitor = instance.AddComponent<DuelRuntimePerformanceMonitor>();
            monitor._requestedDuration = Mathf.Max(5f, duration);
            monitor._outputPath = TryGetArgument(args, OutputArgument, out var path)
                ? path
                : Path.Combine(Application.persistentDataPath, "duel-performance.json");
            monitor._screenshotDirectory = TryGetArgument(args, ScreenshotArgument, out var screenshotPath)
                ? screenshotPath
                : string.Empty;
        }

        private void Start()
        {
            _startedAt = Time.realtimeSinceStartup;
            _batteryAtStart = SystemInfo.batteryLevel;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Application.platform == RuntimePlatform.Android ? 30 : 60;
            if (!string.IsNullOrWhiteSpace(_screenshotDirectory))
            {
                Directory.CreateDirectory(_screenshotDirectory);
            }
            FrameTimingManager.CaptureFrameTimings();
        }

        private void LateUpdate()
        {
            if (_requestedDuration <= 0f) return;
            var elapsed = Time.realtimeSinceStartup - _startedAt;
            if (elapsed >= WarmupSeconds)
            {
                _frameSeconds.Add(Mathf.Max(0.000001f, Time.unscaledDeltaTime));
                _maximumAllocated = Math.Max(
                    _maximumAllocated,
                    Profiler.GetTotalAllocatedMemoryLong());
                _maximumReserved = Math.Max(
                    _maximumReserved,
                    Profiler.GetTotalReservedMemoryLong());
                var timings = new FrameTiming[1];
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 &&
                    timings[0].gpuFrameTime > 0d)
                {
                    _gpuMilliseconds.Add(timings[0].gpuFrameTime);
                }
                FrameTimingManager.CaptureFrameTimings();
                if (_screenshotIndex < 6 &&
                    !string.IsNullOrWhiteSpace(_screenshotDirectory) &&
                    elapsed >= WarmupSeconds + _screenshotIndex * 2.5f)
                {
                    _screenshotIndex++;
                    var screenshot = Path.Combine(
                        _screenshotDirectory,
                        $"duel-ai-{_screenshotIndex:00}.png");
                    if (CaptureOffscreen(screenshot))
                    {
                        _screenshotPaths.Add(screenshot);
                        Debug.Log($"DUEL_SCREENSHOT_CAPTURED path={screenshot}");
                    }
                }
            }
            if (elapsed >= WarmupSeconds + _requestedDuration)
            {
                Complete();
            }
        }

        private void Complete()
        {
            _requestedDuration = 0f;
            var averageFrame = _frameSeconds.Count == 0 ? 0f : _frameSeconds.Average();
            var target = Application.platform == RuntimePlatform.Android ? 30f : 60f;
            var report = new DuelPerformanceReport
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                durationSeconds = _frameSeconds.Sum(),
                sampleCount = _frameSeconds.Count,
                averageFps = averageFrame <= 0f ? 0f : 1f / averageFrame,
                p95FrameMilliseconds = DuelPerformanceSummary.Percentile95Milliseconds(_frameSeconds),
                maximumFrameMilliseconds = _frameSeconds.Count == 0 ? 0f : _frameSeconds.Max() * 1000f,
                averageGpuMilliseconds = _gpuMilliseconds.Count == 0
                    ? 0f
                    : (float)_gpuMilliseconds.Average(),
                gpuSampleCount = _gpuMilliseconds.Count,
                maximumAllocatedMemoryBytes = _maximumAllocated,
                maximumReservedMemoryBytes = _maximumReserved,
                batteryLevelAtStart = _batteryAtStart,
                batteryLevelAtEnd = SystemInfo.batteryLevel,
                batteryStatusAtEnd = SystemInfo.batteryStatus.ToString(),
                targetFps = target,
                minimumAcceptableFps = target * 0.98f,
                headless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null,
                screenshotPaths = _screenshotPaths.ToArray()
            };
            report.targetMet = !report.headless &&
                               report.averageFps >= report.minimumAcceptableFps &&
                               report.p95FrameMilliseconds <= (1000f / target) * 1.25f;
            try
            {
                var directory = Path.GetDirectoryName(_outputPath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(_outputPath, JsonUtility.ToJson(report, true));
                Debug.Log(
                    $"DUEL_PERFORMANCE_REPORT path={_outputPath} fps={report.averageFps:0.00} " +
                    $"p95Ms={report.p95FrameMilliseconds:0.00} gpuMs={report.averageGpuMilliseconds:0.00} " +
                    $"allocated={report.maximumAllocatedMemoryBytes} targetMet={report.targetMet}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"DUEL_PERFORMANCE_REPORT_FAILED {exception.GetType().Name}");
                Application.Quit(3);
                return;
            }
            Application.Quit(report.targetMet ? 0 : 2);
        }

        private static bool TryGetArgument(string[] args, string name, out string value)
        {
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) continue;
                value = args[index + 1];
                return true;
            }
            value = string.Empty;
            return false;
        }

        private static bool CaptureOffscreen(string path)
        {
            var camera = Camera.main;
            if (camera == null) return false;
            var width = Mathf.Max(320, Screen.width);
            var height = Mathf.Max(180, Screen.height);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return File.Exists(path) && new FileInfo(path).Length > 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"DUEL_SCREENSHOT_FAILED path={path} error={exception.GetType().Name}");
                return false;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Destroy(target);
                Destroy(texture);
            }
        }
    }
}
