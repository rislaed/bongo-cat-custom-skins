using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Doorstop {
	public static class Logger {
		public const string LogFileName = "Doorstop.log";
		private const string InfoLevel = "INFO";
		private const string WarnLevel = "WARN";
		private const string ErrorLevel = "ERROR";
		private const string DefaultTag = "DoorstopLoader";
		private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
		private static readonly object _syncRoot = new object();

		public static string GetGameDirectory() {
			return Entrypoint.GetBaseDirectory();
		}

		public static void LogInfo(string message) {
			LogInfo(DefaultTag, message);
		}

		public static void LogWarning(string message) {
			LogWarning(DefaultTag, message);
		}

		public static void LogError(string message) {
			LogError(DefaultTag, message);
		}

		public static void LogInfo(string tag, string message) {
			Write(InfoLevel, tag, message);
		}

		public static void LogWarning(string tag, string message) {
			Write(WarnLevel, tag, message);
		}

		public static void LogError(string tag, string message) {
			Write(ErrorLevel, tag, message);
		}

		private static void Write(string level, string tag, string message) {
			try {
				string baseDir = Entrypoint.GetBaseDirectory();
				string path = Path.Combine(baseDir, LogFileName);
				string timestamp = DateTime.Now.ToString(TimeFormat);
				string line = "[" + timestamp + "] [" + level + "] [" + tag + "] " + message + "\r\n";
				lock (_syncRoot) {
					using (FileStream fs = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) {
						byte[] bytes = Encoding.UTF8.GetBytes(line);
						fs.Write(bytes, 0, bytes.Length);
					}
				}
			} catch {}
		}
	}

	public static class Entrypoint {
		private const uint TimerElapseMs = 100;
		private const int MaxWaitTicks = 300;
		private const string DoorstopProcessPathEnv = "DOORSTOP_PROCESS_PATH";
		private const string DoorstopInvokeDllPathEnv = "DOORSTOP_INVOKE_DLL_PATH";
		private const string BongoCatExe = "BongoCat.exe";
		private const string AssemblyCSharpName = "Assembly-CSharp";
		private const string DllExtension = ".dll";
		private const string DllSearchPattern = "*.dll";
		private const string ModsDirectoryName = "Mods";
		private const string ManagedDirectoryName = "BongoCat_Data";
		private const string ManagedSubdirectoryName = "Managed";
		private const string LoaderDllName = "DoorstopLoader.dll";
		private const string BepInExDirectoryName = "BepInEx";
		private const string CoreDirectoryName = "core";
		private const string BepInExPreloaderDll = "BepInEx.Preloader.dll";
		private const string DoorstopEntrypointType = "Doorstop.Entrypoint";
		private const string StartMethodName = "Start";
		private const string MainMethodName = "Main";

		private static readonly string[] EntrypointMethodNames = new string[] {
			"Initialize",
			"Init",
			"Load",
			"Main"
		};

		private static readonly string[] PreferredTypeKeywords = new string[] {
			"Bootstrap",
			"Plugin",
			"Entrypoint",
			"Mod"
		};

		private static string _baseDir;
		private static bool _initialized = false;

		private delegate void TimerProc(IntPtr hWnd, uint uMsg, IntPtr nIDEvent, uint dwTime);

		[DllImport("user32.dll")]
		private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, TimerProc lpTimerFunc);

		[DllImport("user32.dll")]
		private static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);

		private static TimerProc _timerCallback;
		private static IntPtr _timerId;
		private static int _ticks = 0;

		public static string GetBaseDirectory() {
			if (_baseDir != null) return _baseDir;

			try {
				string processPath = Environment.GetEnvironmentVariable(DoorstopProcessPathEnv);
				if (!string.IsNullOrEmpty(processPath)) {
					string dir = Path.GetDirectoryName(processPath);
					if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) {
						_baseDir = dir;
						return _baseDir;
					}
				}
			} catch {}

			try {
				string appBase = AppDomain.CurrentDomain.BaseDirectory;
				if (!string.IsNullOrEmpty(appBase) && Directory.Exists(appBase) && File.Exists(Path.Combine(appBase, BongoCatExe))) {
					_baseDir = appBase;
					return _baseDir;
				}
			} catch {}

			_baseDir = Directory.GetCurrentDirectory();
			return _baseDir;
		}

		private const string LoaderPrefix = "[DoorstopLoader] ";

		public static void Log(string text) {
			string cleanText = text.StartsWith(LoaderPrefix) ? text.Substring(LoaderPrefix.Length) : text;
			Logger.LogInfo(cleanText);
		}

		public static void Start() {
			try {
				string logFile = Path.Combine(GetBaseDirectory(), Logger.LogFileName);
				if (File.Exists(logFile)) {
					File.Delete(logFile);
				}
			} catch {}

			Log("[DoorstopLoader] Start() called at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			Log("[DoorstopLoader] Base directory: " + GetBaseDirectory());

			try {
				AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
				Log("[DoorstopLoader] AssemblyResolve registered.");

				TryChainBepInEx();

				_timerCallback = new TimerProc(OnTimerTick);
				_timerId = SetTimer(IntPtr.Zero, IntPtr.Zero, TimerElapseMs, _timerCallback);
				Log("[DoorstopLoader] Main thread timer initialized. Waiting for Assembly-CSharp...");
			} catch (Exception ex) {
				Log("[DoorstopLoader] Exception in Start: " + ex.ToString());
			}
		}

		private static void TryChainBepInEx() {
			try {
				string baseDir = GetBaseDirectory();
				string preloader = Path.Combine(baseDir, BepInExDirectoryName, CoreDirectoryName, BepInExPreloaderDll);
				if (!File.Exists(preloader)) return;

				Log("[DoorstopLoader] Detected BepInEx preloader at " + preloader + ". Chaining invocation...");
				try {
					Environment.SetEnvironmentVariable(DoorstopInvokeDllPathEnv, preloader);
				} catch {}
				Assembly asm = Assembly.LoadFrom(preloader);
				if (asm == null) return;

				Type entryType = asm.GetType(DoorstopEntrypointType);
				if (entryType == null) return;

				MethodInfo startMethod = entryType.GetMethod(StartMethodName, BindingFlags.Public | BindingFlags.Static);
				if (startMethod == null) {
					startMethod = entryType.GetMethod(MainMethodName, BindingFlags.Public | BindingFlags.Static);
				}

				if (startMethod != null) {
					startMethod.Invoke(null, null);
					Log("[DoorstopLoader] BepInEx preloader chained successfully!");
				}
			} catch (Exception ex) {
				Log("[DoorstopLoader] Notice: BepInEx chaining skipped or failed: " + ex.Message);
			}
		}

		private static void OnTimerTick(IntPtr hWnd, uint uMsg, IntPtr nIDEvent, uint dwTime) {
			try {
				_ticks++;
				if (_initialized) return;

				Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
				bool gameFound = false;
				for (int i = 0; i < loaded.Length; i++) {
					if (string.Equals(loaded[i].GetName().Name, AssemblyCSharpName, StringComparison.OrdinalIgnoreCase)) {
						gameFound = true;
						break;
					}
				}

				if (!gameFound) {
					if (_ticks >= MaxWaitTicks) {
						KillTimer(IntPtr.Zero, _timerId);
						Log("[DoorstopLoader] TIMEOUT waiting for Assembly-CSharp.");
					}
					return;
				}

				KillTimer(IntPtr.Zero, _timerId);
				_initialized = true;

				Log("[DoorstopLoader] Assembly-CSharp detected on main thread after " + (_ticks * TimerElapseMs).ToString() + "ms!");
				LoadModsMainThread();
			} catch (Exception ex) {
				Log("[DoorstopLoader] Exception in OnTimerTick: " + ex.ToString());
			}
		}

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
			try {
				string asmName = new AssemblyName(args.Name).Name;
				string dllName = asmName + DllExtension;
				string baseDir = GetBaseDirectory();

				string modsDir = Path.Combine(baseDir, ModsDirectoryName);
				if (Directory.Exists(modsDir)) {
					string direct = Path.Combine(modsDir, dllName);
					if (File.Exists(direct)) {
						return Assembly.LoadFrom(direct);
					}
					string found = FindFileRecursive(modsDir, dllName);
					if (found != null) {
						return Assembly.LoadFrom(found);
					}
				}

				string managed = Path.Combine(baseDir, ManagedDirectoryName, ManagedSubdirectoryName, dllName);
				if (File.Exists(managed)) {
					return Assembly.LoadFrom(managed);
				}

				string bepCore = Path.Combine(baseDir, BepInExDirectoryName, CoreDirectoryName, dllName);
				if (File.Exists(bepCore)) {
					return Assembly.LoadFrom(bepCore);
				}

				string root = Path.Combine(baseDir, dllName);
				if (File.Exists(root)) {
					return Assembly.LoadFrom(root);
				}
			} catch {}
			return null;
		}

		private static string FindFileRecursive(string dir, string targetFileName) {
			try {
				if (!Directory.Exists(dir)) return null;
				string direct = Path.Combine(dir, targetFileName);
				if (File.Exists(direct)) return direct;

				string[] subDirs = Directory.GetDirectories(dir);
				if (subDirs != null) {
					for (int i = 0; i < subDirs.Length; i++) {
						string found = FindFileRecursive(subDirs[i], targetFileName);
						if (found != null) return found;
					}
				}
			} catch {}
			return null;
		}

		private static void CollectDlls(string dir, List<string> list) {
			try {
				if (!Directory.Exists(dir)) return;
				string[] files = Directory.GetFiles(dir, DllSearchPattern);
				if (files != null) {
					for (int i = 0; i < files.Length; i++) {
						list.Add(files[i]);
					}
				}
				string[] subDirs = Directory.GetDirectories(dir);
				if (subDirs != null) {
					for (int i = 0; i < subDirs.Length; i++) {
						CollectDlls(subDirs[i], list);
					}
				}
			} catch {}
		}

		private static void LoadModsMainThread() {
			try {
				string baseDir = GetBaseDirectory();
				string modsDir = Path.Combine(baseDir, ModsDirectoryName);
				if (!Directory.Exists(modsDir)) {
					Log("[DoorstopLoader] Mods directory not found: " + modsDir);
					return;
				}

				List<string> dllFiles = new List<string>();
				CollectDlls(modsDir, dllFiles);

				if (dllFiles.Count == 0) {
					Log("[DoorstopLoader] No DLL files found in " + modsDir);
					return;
				}

				Log("[DoorstopLoader] Discovered " + dllFiles.Count.ToString() + " DLL files in " + modsDir + ". Resolving mod entrypoints...");

				for (int i = 0; i < dllFiles.Count; i++) {
					string dllPath = dllFiles[i];
					string fileName = Path.GetFileName(dllPath);

					if (string.Equals(fileName, LoaderDllName, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}

					TryLoadModAssembly(dllPath);
				}
			} catch (Exception ex) {
				Log("[DoorstopLoader] FATAL during mod scanning: " + ex.ToString());
			}
		}

		private static void TryLoadModAssembly(string dllPath) {
			string fileName = Path.GetFileName(dllPath);
			try {
				Assembly asm = Assembly.LoadFrom(dllPath);
				MethodInfo targetMethod = FindModEntrypoint(asm);
				if (targetMethod == null) {
					Log("[DoorstopLoader] Skipped " + fileName + " (no parameterless static Initialize/Init/Load/Main entrypoint found).");
					return;
				}

				Log("[DoorstopLoader] [" + fileName + "] Invoking entrypoint " + targetMethod.DeclaringType.FullName + "." + targetMethod.Name + "()...");
				targetMethod.Invoke(null, null);
				Log("[DoorstopLoader] [" + fileName + "] Initialized successfully!");
			} catch (Exception ex) {
				Log("[DoorstopLoader] ERROR loading mod " + fileName + ": " + ex.ToString());
			}
		}

		private static MethodInfo FindModEntrypoint(Assembly asm) {
			try {
				string asmName = asm.GetName().Name;
				string[] commonTypeNames = new string[] {
					asmName + "." + "CustomSkinsBootstrap",
					"BongoCatCustomSkins.CustomSkinsBootstrap",
					asmName + "." + "Plugin",
					asmName + "." + "Mod",
					asmName + "." + "Bootstrap",
					asmName + "." + "Entrypoint",
					"Plugin",
					"Mod",
					"Bootstrap",
					"Entrypoint"
				};

				for (int i = 0; i < commonTypeNames.Length; i++) {
					try {
						Type directType = asm.GetType(commonTypeNames[i], false, true);
						if (directType != null) {
							MethodInfo m = FindCandidateMethod(directType);
							if (m != null) return m;
						}
					} catch {}
				}

				Type[] types = null;
				try {
					types = asm.GetTypes();
				} catch {}

				if (types == null) return null;

				for (int p = 0; p < PreferredTypeKeywords.Length; p++) {
					string keyword = PreferredTypeKeywords[p];
					for (int i = 0; i < types.Length; i++) {
						Type t = types[i];
						if (t != null && t.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) {
							MethodInfo m = FindCandidateMethod(t);
							if (m != null) return m;
						}
					}
				}

				for (int i = 0; i < types.Length; i++) {
					Type t = types[i];
					if (t != null) {
						MethodInfo m = FindCandidateMethod(t);
						if (m != null) return m;
					}
				}
			} catch {}
			return null;
		}

		private static MethodInfo FindCandidateMethod(Type type) {
			if (type == null) return null;
			for (int m = 0; m < EntrypointMethodNames.Length; m++) {
				string methodName = EntrypointMethodNames[m];
				MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
				if (method != null && method.GetParameters().Length == 0) {
					return method;
				}
			}
			return null;
		}
	}
}