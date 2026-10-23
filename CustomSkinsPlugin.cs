using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Heathen.SteamworksIntegration;
using Steamworks;
using DG.Tweening;
using Doorstop;
using Logger = Doorstop.Logger;

namespace BongoCatCustomSkins {
	[Serializable]
	public class SkinConfig {
		public int id = 0;
		public string name = "";
		public string displayName = "";
		public string description = "";
		public string quality = "legendary";
		public bool disableClothing = false;
		public bool invertIdleAndBothPunch = false;
		public bool fullBody = false;
		public float hatOffsetX = 0f;
		public float hatOffsetY = 0f;
		public float hatScale = 1f;
		public float bongosOffsetX = 0f;
		public float bongosOffsetY = 0f;
		public float bongosScale = 1f;
		public List<string> incompatibleItems = new List<string>();
		public List<string> incompatibleItemNames = new List<string>();
		public List<int> incompatibleItemIds = new List<int>();

		public bool IsItemIncompatible(int itemId, string itemName, string spriteName) {
			if (incompatibleItemIds != null && incompatibleItemIds.Contains(itemId)) {
				return true;
			}
			if (incompatibleItems != null) {
				for (int i = 0; i < incompatibleItems.Count; i++) {
					string s = incompatibleItems[i];
					if (string.IsNullOrEmpty(s)) continue;
					int parsedId;
					if (int.TryParse(s, out parsedId) && parsedId == itemId) return true;
					if (!string.IsNullOrEmpty(itemName) && string.Equals(s, itemName, StringComparison.OrdinalIgnoreCase)) return true;
					if (!string.IsNullOrEmpty(itemName) && string.Equals(s.Replace(" ", ""), itemName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)) return true;
					if (!string.IsNullOrEmpty(spriteName) && string.Equals(s, spriteName, StringComparison.OrdinalIgnoreCase)) return true;
				}
			}
			if (incompatibleItemNames != null) {
				for (int i = 0; i < incompatibleItemNames.Count; i++) {
					string s = incompatibleItemNames[i];
					if (string.IsNullOrEmpty(s)) continue;
					if (!string.IsNullOrEmpty(itemName) && string.Equals(s, itemName, StringComparison.OrdinalIgnoreCase)) return true;
					if (!string.IsNullOrEmpty(itemName) && string.Equals(s.Replace(" ", ""), itemName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)) return true;
					if (!string.IsNullOrEmpty(spriteName) && string.Equals(s, spriteName, StringComparison.OrdinalIgnoreCase)) return true;
				}
			}
			return false;
		}
	}

	[Serializable]
	public class SkinOffsets {
		public float hatOffsetX = 0f;
		public float hatOffsetY = 0f;
		public float hatScale = 1f;
		public float bongosOffsetX = 0f;
		public float bongosOffsetY = 0f;
		public float bongosScale = 1f;
	}

	public class LoadedSkin {
		public SkinConfig Config;
		public string FolderPath;
		public Sprite SpriteIcon;
		public Sprite SpriteLeft;
		public Sprite SpriteLeftPunch;
		public Sprite SpriteRight;
		public Sprite SpriteRightPunch;
		public Sprite SpriteBothPunch;
		public Sprite SpritePetting;
		public Sprite SpriteSleeping;
		public BongoCat.SteamItemRuntime RuntimeItem;
	}

	public static class CustomSkinsBootstrap {
		public static void Initialize() {
			try {
				if (CustomSkinsPlugin.Instance == null) {
					GameObject host = new GameObject("BongoCatCustomSkinsHost");
					UnityEngine.Object.DontDestroyOnLoad(host);
					host.AddComponent<CustomSkinsPlugin>();
					Logger.LogInfo("CustomSkins", "CustomSkinsPlugin host GameObject created successfully on main thread!");
				}
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in CustomSkinsBootstrap.Initialize: " + ex.ToString());
			}
		}
	}

	public class CustomSkinsPlugin : MonoBehaviour {
		public static CustomSkinsPlugin Instance { get; private set; }

		public static KeyCode HatEditorKey = KeyCode.F9;
		public static KeyCode BongosEditorKey = KeyCode.F10;

		private const int DefaultSkinIdStart = 900000;
		private const float PettingDurationSeconds = 1.2f;
		private const float PatPunchThresholdSeconds = 0.25f;
		private const float SpritePivotCenter = 0.5f;
		private const float DefaultPixelsPerUnit = 100f;
		private const int PlaceholderTextureDimension = 2;
		private const ushort DefaultItemQuantity = 1;
		private const ushort DefaultItemFlags = 0;
		private const float DefaultScale = 1f;
		private const int IdPrefixLength = 5;

		private const string SuffixIcon = "Icon";
		private const string SuffixLeft = "Left";
		private const string SuffixLeftPunch = "LeftPunch";
		private const string SuffixRight = "Right";
		private const string SuffixRightPunch = "RightPunch";
		private const string SuffixBothPunch = "BothPunch";
		private const string SuffixPetting = "Petting";
		private const string SuffixSleeping = "Sleeping";
		private const string ImageExtension = ".png";
		private const string CustomSkinsDirName = "CustomSkins";
		private const string ModsDirName = "Mods";
		private const string OffsetsFileName = "__offsets__.json";
		private const string SavedCustomSkinPrefKey = "CUSTOM_SKINS_EQUIPPED_ID";
		private const string EquippedItemsPrefKey = "EQUIPPED_ITEMS_3";
		private const string InjectedToggleName = "HatDragToggle";
		private const float MinScaleLimit = 0.2f;
		private const float MaxScaleLimit = 4.0f;
		private const float ScaleStepPerScroll = 0.05f;
		private const float UiUpdateInterval = 0.25f;
		private const float ScrollThreshold = 0.01f;
		private const float EditorDragBodyMinAlpha = 0.70f;
		private const float EditorDragHatMinAlpha = 0.85f;
		private const float EditorDragMaxAlpha = 1f;
		private const float EditorPulseSpeed = 3f;
		private const float EditorPulseMidpoint = 0.5f;
		private const int SpriteBorderPadding = 1;
		private const int SpriteBorderPaddingDouble = 2;
		private const float SpriteBorderPaddingFloat = 1f;

		private readonly Dictionary<string, SkinOffsets> _offsetsMap = new Dictionary<string, SkinOffsets>(StringComparer.OrdinalIgnoreCase);

		private FieldInfo _fItemIndex;
		private FieldInfo _fDraggableIgnoring;
		private float _uiUpdateTimer = 0f;
		private bool _isHatDragModeEnabled = false;
		private bool _isBongosDragModeEnabled = false;
		private bool _isDragging = false;
		private Vector2 _dragStartMousePos;
		private Vector2 _dragStartOffsets;
		private Vector3 _frozenCatPosition;

		private readonly Dictionary<int, LoadedSkin> _skinsById = new Dictionary<int, LoadedSkin>();
		private readonly List<LoadedSkin> _loadedSkins = new List<LoadedSkin>();

		private FieldInfo _fRoutineLeft;
		private FieldInfo _fRoutineRight;
		private FieldInfo _fBodyImage;
		private FieldInfo _fRightPawImage;
		private FieldInfo _fHatImage;
		private FieldInfo _fHatObject;
		private FieldInfo _fBongosLeftBack;
		private FieldInfo _fBongosLeftFront;
		private FieldInfo _fBongosRight;
		private FieldInfo _fLocallyEquipped;
		private MethodInfo _mEquip;
		private MethodInfo _mUnequip;
		private FieldInfo _fDisabledOverlay;
		private readonly HashSet<int> _forceDisabledItemIds = new HashSet<int>();

		private bool _inventoryInjected = false;
		private bool _wasCustomActive = false;
		private string _skinsDirectory;

		private float _petTimer = 0f;
		private float _lastPatTime = -10f;
		private bool _pendingPetReaction = false;
		private bool _punchStartedForPat = false;
		private bool _eventsHooked = false;
		private bool _hasDefaultHatTransform = false;
		private Vector2 _defaultHatAnchoredPos;
		private Vector3 _defaultHatScale = Vector3.one;
		private bool _hasDefaultBongosTransform = false;
		private Vector2 _defBongosLBPos;
		private Vector2 _defBongosLFPos;
		private Vector2 _defBongosRPos;
		private Vector3 _defBongosLBScale = Vector3.one;
		private Vector3 _defBongosLFScale = Vector3.one;
		private Vector3 _defBongosRScale = Vector3.one;

		private BongoCat.Multiplayer.AfkDetector _afkDetector;
		private FieldInfo _fIsAfk;
		private FieldInfo _fCatPatterSeq;
		private FieldInfo _fUnowned;

		private void Awake() {
			Instance = this;
			Logger.LogInfo("CustomSkins", "CustomSkinsPlugin.Awake() running on main thread!");

			try {
				Type catType = typeof(BongoCat.Cat);
				_fRoutineLeft = catType.GetField("_routineLeft", BindingFlags.NonPublic | BindingFlags.Instance);
				_fRoutineRight = catType.GetField("_routineRight", BindingFlags.NonPublic | BindingFlags.Instance);
				_fBodyImage = catType.GetField("_bodyImage", BindingFlags.NonPublic | BindingFlags.Instance);
				_fRightPawImage = catType.GetField("_rightPawImage", BindingFlags.NonPublic | BindingFlags.Instance);
				_fHatImage = catType.GetField("_hatImage", BindingFlags.NonPublic | BindingFlags.Instance);
				_fHatObject = catType.GetField("_hatObject", BindingFlags.NonPublic | BindingFlags.Instance);
				_fBongosLeftBack = catType.GetField("_bongosLeftBackImage", BindingFlags.NonPublic | BindingFlags.Instance);
				_fBongosLeftFront = catType.GetField("_bongosLeftFrontImage", BindingFlags.NonPublic | BindingFlags.Instance);
				_fBongosRight = catType.GetField("_bongosRightImage", BindingFlags.NonPublic | BindingFlags.Instance);

				_fLocallyEquipped = typeof(BongoCat.CatCosmetics).GetField("_locallyEquippedItems", BindingFlags.NonPublic | BindingFlags.Instance);
				_mEquip = typeof(BongoCat.CatCosmetics).GetMethod("Equip", BindingFlags.NonPublic | BindingFlags.Instance);
				_mUnequip = typeof(BongoCat.CatCosmetics).GetMethod("Unequip", BindingFlags.NonPublic | BindingFlags.Instance);
				_fDisabledOverlay = typeof(BongoCat.InventoryItem).GetField("_disabledOverlay", BindingFlags.NonPublic | BindingFlags.Instance);
				_fUnowned = typeof(BongoCat.InventoryItem).GetField("_unownedOverlay", BindingFlags.NonPublic | BindingFlags.Instance);
				_fIsAfk = typeof(BongoCat.Multiplayer.AfkDetector).GetField("_isAfk", BindingFlags.NonPublic | BindingFlags.Instance);
				_fCatPatterSeq = typeof(BongoCat.Multiplayer.CatPatter).GetField("_seq", BindingFlags.NonPublic | BindingFlags.Instance);
				_fDraggableIgnoring = typeof(BongoCat.Draggable).GetField("_ignoring", BindingFlags.NonPublic | BindingFlags.Instance);

				BongoCat.CatCosmetics.OnEquippedItemsChanged += HandleEquippedItemsChanged;

				_skinsDirectory = Path.Combine(Path.Combine(Logger.GetGameDirectory(), ModsDirName), CustomSkinsDirName);
				CleanupLegacySettingsToggle();
				RepairEquippedItemsPref();
				LoadAllSkins();
				EnsureSavedSkinInEquippedPref();
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in Awake: " + ex.ToString());
			}
		}

		private void OnDestroy() {
			try {
				BongoCat.CatCosmetics.OnEquippedItemsChanged -= HandleEquippedItemsChanged;
			} catch {}
		}

		private void CleanupLegacySettingsToggle() {
			try {
				GameObject[] allObjs = Resources.FindObjectsOfTypeAll<GameObject>();
				for (int i = 0; i < allObjs.Length; i++) {
					if (allObjs[i] != null && string.Equals(allObjs[i].name, InjectedToggleName, StringComparison.Ordinal)) {
						UnityEngine.Object.Destroy(allObjs[i]);
					}
				}
			} catch {}
		}

		private void RepairEquippedItemsPref() {
			try {
				if (!PlayerPrefs.HasKey(EquippedItemsPrefKey)) return;
				string raw = PlayerPrefs.GetString(EquippedItemsPrefKey);
				if (string.IsNullOrEmpty(raw)) return;

				int[] validIds;
				if (!Helper.PlayerPrefsHelp.TryGetObject<int[]>(EquippedItemsPrefKey, out validIds) || validIds == null) {
					List<int> extracted = new List<int>();
					int cur = 0;
					while (cur < raw.Length) {
						int idIdx = raw.IndexOf("\"Id\":", cur, StringComparison.OrdinalIgnoreCase);
						if (idIdx == -1) break;
						int start = idIdx + IdPrefixLength;
						while (start < raw.Length && (raw[start] == ' ' || raw[start] == ':')) start++;
						int end = start;
						while (end < raw.Length && char.IsDigit(raw[end])) end++;
						if (end > start) {
							int parsed;
							if (int.TryParse(raw.Substring(start, end - start), out parsed) && !extracted.Contains(parsed)) {
								extracted.Add(parsed);
							}
						}
						cur = end;
					}
					if (extracted.Count > 0) {
						Helper.PlayerPrefsHelp.SetObject(EquippedItemsPrefKey, extracted.ToArray());
						Logger.LogInfo("CustomSkins", "Repaired corrupted EQUIPPED_ITEMS_3 with extracted IDs: " + string.Join(",", extracted));
					}
				}
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error repairing EQUIPPED_ITEMS_3: " + ex.ToString());
			}
		}

		private void EnsureSavedSkinInEquippedPref() {
			try {
				int savedSkinId = PlayerPrefs.GetInt(SavedCustomSkinPrefKey, 0);
				if (savedSkinId > 0 && _skinsById.ContainsKey(savedSkinId)) {
					int[] currentIds;
					if (Helper.PlayerPrefsHelp.TryGetObject<int[]>(EquippedItemsPrefKey, out currentIds) && currentIds != null) {
						bool hasSkin = false;
						for (int i = 0; i < currentIds.Length; i++) {
							if (currentIds[i] == savedSkinId) {
								hasSkin = true;
								break;
							}
						}
						if (!hasSkin) {
							int[] newIds = new int[currentIds.Length + 1];
							for (int i = 0; i < currentIds.Length; i++) {
								newIds[i] = currentIds[i];
							}
							newIds[currentIds.Length] = savedSkinId;
							Helper.PlayerPrefsHelp.SetObject(EquippedItemsPrefKey, newIds);
							Logger.LogInfo("CustomSkins", "Preserved custom skin ID " + savedSkinId.ToString() + " alongside equipped hats in EQUIPPED_ITEMS_3.");
						}
					}
				}
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in EnsureSavedSkinInEquippedPref: " + ex.ToString());
			}
		}

		private void HandleEquippedItemsChanged() {
			try {
				BongoCat.CatCosmetics cosmetics = BongoCat.CatCosmetics.Instance;
				if (cosmetics == null) return;

				BongoCat.SteamItemRuntime skin = cosmetics.Skin;
				if (skin != null && _skinsById.ContainsKey(skin.Id)) {
					if (PlayerPrefs.GetInt(SavedCustomSkinPrefKey, 0) != skin.Id) {
						PlayerPrefs.SetInt(SavedCustomSkinPrefKey, skin.Id);
						PlayerPrefs.Save();
						Logger.LogInfo("CustomSkins", "Persisted custom skin selection: " + skin.ItemName + " (ID=" + skin.Id.ToString() + ")");
					}
				} else if (skin != null && !_skinsById.ContainsKey(skin.Id)) {
					if (PlayerPrefs.GetInt(SavedCustomSkinPrefKey, 0) != 0) {
						PlayerPrefs.DeleteKey(SavedCustomSkinPrefKey);
						PlayerPrefs.Save();
						Logger.LogInfo("CustomSkins", "User selected native skin (" + skin.ItemName + "), cleared custom skin persistence.");
					}
				} else if (skin == null) {
					if (PlayerPrefs.GetInt(SavedCustomSkinPrefKey, 0) != 0) {
						PlayerPrefs.DeleteKey(SavedCustomSkinPrefKey);
						PlayerPrefs.Save();
						Logger.LogInfo("CustomSkins", "User unequipped skin to vanilla, cleared custom skin persistence.");
					}
				}
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in HandleEquippedItemsChanged: " + ex.ToString());
			}
		}

		public void LoadAllSkins() {
			_skinsById.Clear();
			_loadedSkins.Clear();

			try {
				if (!Directory.Exists(_skinsDirectory)) {
					Directory.CreateDirectory(_skinsDirectory);
					Logger.LogInfo("CustomSkins", "Created CustomSkins directory at: " + _skinsDirectory);
				}

				string[] rootJsonFiles = Directory.GetFiles(_skinsDirectory, "*.json");
				for (int i = 0; i < rootJsonFiles.Length; i++) {
					if (string.Equals(Path.GetFileName(rootJsonFiles[i]), OffsetsFileName, StringComparison.OrdinalIgnoreCase)) continue;
					LoadSkinFromJsonFile(rootJsonFiles[i], _skinsDirectory);
				}

				string[] skinDirs = Directory.GetDirectories(_skinsDirectory);
				for (int i = 0; i < skinDirs.Length; i++) {
					string subDir = skinDirs[i];
					string[] subJsonFiles = Directory.GetFiles(subDir, "*.json");
					if (subJsonFiles.Length > 0) {
						for (int j = 0; j < subJsonFiles.Length; j++) {
							if (string.Equals(Path.GetFileName(subJsonFiles[j]), OffsetsFileName, StringComparison.OrdinalIgnoreCase)) continue;
							LoadSkinFromJsonFile(subJsonFiles[j], subDir);
						}
					} else {
						LoadSkinFromFolderWithoutJson(subDir);
					}
				}

				LoadOffsetsFile();
				ApplyOffsetsToAllLoadedSkins();

				RegisterAllSpritesInImageCache();
				Logger.LogInfo("CustomSkins", "Total loaded custom skins: " + _loadedSkins.Count.ToString());
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in LoadAllSkins: " + ex.ToString());
			}
		}

		public static int GenerateSkinId(string name) {
			if (string.IsNullOrEmpty(name)) return DefaultSkinIdStart;
			uint hash = 2166136261;
			for (int i = 0; i < name.Length; i++) {
				hash = (hash ^ name[i]) * 16777619;
			}
			return DefaultSkinIdStart + (int)(hash % 100000);
		}

		private void LoadSkinFromJsonFile(string jsonPath, string searchDir) {
			try {
				string json = File.ReadAllText(jsonPath);
				SkinConfig config = JsonUtility.FromJson<SkinConfig>(json);
				if (config == null) return;

				if (string.IsNullOrEmpty(config.name)) {
					config.name = Path.GetFileNameWithoutExtension(jsonPath);
				}
				if (string.IsNullOrEmpty(config.displayName)) {
					config.displayName = config.name;
				}
				if (config.description == null) {
					config.description = "";
				}
				if (config.id <= 0) {
					config.id = GenerateSkinId(config.name);
				}

				if (_skinsById.ContainsKey(config.id)) {
					Logger.LogInfo("CustomSkins", "Skin ID " + config.id.ToString() + " already loaded, skipping duplicate " + jsonPath);
					return;
				}

				LoadSkinWithConfig(config, searchDir);
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Failed to parse skin JSON at " + jsonPath + ": " + ex.ToString());
			}
		}

		private void LoadSkinFromFolderWithoutJson(string subDir) {
			try {
				string folderName = Path.GetFileName(subDir);
				SkinConfig config = new SkinConfig {
					name = folderName,
					displayName = folderName,
					description = "",
					quality = "legendary",
					disableClothing = true
				};
				config.id = GenerateSkinId(config.name);
				LoadSkinWithConfig(config, subDir);
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Failed to load skin without JSON from " + subDir + ": " + ex.ToString());
			}
		}

		private void LoadSkinWithConfig(SkinConfig config, string searchDir) {
			try {
				string prefix = !string.IsNullOrEmpty(config.name) ? config.name.Replace(" ", "") : "";
				string altPrefix = !string.IsNullOrEmpty(config.displayName) ? config.displayName.Replace(" ", "") : "";

				LoadedSkin skin = new LoadedSkin {
					Config = config,
					FolderPath = searchDir
				};

				skin.SpriteLeft = ResolveSprite(searchDir, prefix, altPrefix, SuffixLeft);
				skin.SpriteLeftPunch = ResolveSprite(searchDir, prefix, altPrefix, SuffixLeftPunch) ?? skin.SpriteLeft;
				skin.SpriteRight = ResolveSprite(searchDir, prefix, altPrefix, SuffixRight);
				skin.SpriteRightPunch = ResolveSprite(searchDir, prefix, altPrefix, SuffixRightPunch) ?? skin.SpriteLeft;
				skin.SpriteBothPunch = ResolveSprite(searchDir, prefix, altPrefix, SuffixBothPunch) ?? skin.SpriteLeft;
				skin.SpritePetting = ResolveSprite(searchDir, prefix, altPrefix, SuffixPetting) ?? skin.SpriteLeft;
				skin.SpriteSleeping = ResolveSprite(searchDir, prefix, altPrefix, SuffixSleeping) ?? skin.SpriteLeft;
				skin.SpriteIcon = ResolveSprite(searchDir, prefix, altPrefix, SuffixIcon) ?? skin.SpriteLeft;

				_skinsById[config.id] = skin;
				_loadedSkins.Add(skin);

				Logger.LogInfo("CustomSkins", "Loaded skin [" + config.name + "] (ID=" + config.id.ToString() + "): " +
					"Left=" + (skin.SpriteLeft != null ? "OK" : "NULL") +
					", LeftPunch=" + (skin.SpriteLeftPunch != null ? "OK" : "NULL") +
					", RightPunch=" + (skin.SpriteRightPunch != null ? "OK" : "NULL") +
					", BothPunch=" + (skin.SpriteBothPunch != null ? "OK" : "NULL") +
					", Petting=" + (skin.SpritePetting != null ? "OK" : "NULL") +
					", Sleeping=" + (skin.SpriteSleeping != null ? "OK" : "NULL") +
					", Icon=" + (skin.SpriteIcon != null ? "OK" : "NULL"));
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in LoadSkinWithConfig for " + config.name + ": " + ex.ToString());
			}
		}

		private void ApplyOffsetsToAllLoadedSkins() {
			for (int i = 0; i < _loadedSkins.Count; i++) {
				LoadedSkin skin = _loadedSkins[i];
				if (skin == null || skin.Config == null) continue;

				SkinOffsets offsets;
				if (_offsetsMap.TryGetValue(skin.Config.name, out offsets) || (!string.IsNullOrEmpty(skin.Config.displayName) && _offsetsMap.TryGetValue(skin.Config.displayName, out offsets))) {
					skin.Config.hatOffsetX = offsets.hatOffsetX;
					skin.Config.hatOffsetY = offsets.hatOffsetY;
					skin.Config.hatScale = offsets.hatScale;
					skin.Config.bongosOffsetX = offsets.bongosOffsetX;
					skin.Config.bongosOffsetY = offsets.bongosOffsetY;
					skin.Config.bongosScale = offsets.bongosScale;
				} else {
					SkinOffsets newEntry = new SkinOffsets {
						hatOffsetX = skin.Config.hatOffsetX,
						hatOffsetY = skin.Config.hatOffsetY,
						hatScale = skin.Config.hatScale > 0f ? skin.Config.hatScale : DefaultScale,
						bongosOffsetX = skin.Config.bongosOffsetX,
						bongosOffsetY = skin.Config.bongosOffsetY,
						bongosScale = skin.Config.bongosScale > 0f ? skin.Config.bongosScale : DefaultScale
					};
					_offsetsMap[skin.Config.name] = newEntry;
				}
			}
		}

		private void LoadOffsetsFile() {
			try {
				string filePath = Path.Combine(_skinsDirectory, OffsetsFileName);
				if (!File.Exists(filePath)) return;

				string text = File.ReadAllText(filePath);
				if (string.IsNullOrEmpty(text)) return;

				_offsetsMap.Clear();
				int cur = 0;
				while (cur < text.Length) {
					int quoteOpen = text.IndexOf('\"', cur);
					if (quoteOpen == -1) break;
					int quoteClose = text.IndexOf('\"', quoteOpen + 1);
					if (quoteClose == -1) break;

					string charName = text.Substring(quoteOpen + 1, quoteClose - quoteOpen - 1);
					int braceOpen = text.IndexOf('{', quoteClose);
					if (braceOpen == -1) break;

					int braceClose = text.IndexOf('}', braceOpen);
					if (braceClose == -1) break;

					string objJson = text.Substring(braceOpen, braceClose - braceOpen + 1);
					SkinOffsets offsets = JsonUtility.FromJson<SkinOffsets>(objJson);
					if (offsets != null) {
						_offsetsMap[charName] = offsets;
					}
					cur = braceClose + 1;
				}
				Logger.LogInfo("CustomSkins", "Loaded " + _offsetsMap.Count.ToString() + " character offset profiles from " + OffsetsFileName);
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Failed to load offsets file: " + ex.ToString());
			}
		}

		private void SaveOffsetsFile() {
			try {
				string filePath = Path.Combine(_skinsDirectory, OffsetsFileName);
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("{");
				int count = 0;
				foreach (KeyValuePair<string, SkinOffsets> kvp in _offsetsMap) {
					sb.Append("\t\"").Append(kvp.Key).AppendLine("\": {");
					sb.Append("\t\t\"hatOffsetX\": ").Append(kvp.Value.hatOffsetX.ToString("F1", CultureInfo.InvariantCulture)).AppendLine(",");
					sb.Append("\t\t\"hatOffsetY\": ").Append(kvp.Value.hatOffsetY.ToString("F1", CultureInfo.InvariantCulture)).AppendLine(",");
					sb.Append("\t\t\"hatScale\": ").Append(kvp.Value.hatScale.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(",");
					sb.Append("\t\t\"bongosOffsetX\": ").Append(kvp.Value.bongosOffsetX.ToString("F1", CultureInfo.InvariantCulture)).AppendLine(",");
					sb.Append("\t\t\"bongosOffsetY\": ").Append(kvp.Value.bongosOffsetY.ToString("F1", CultureInfo.InvariantCulture)).AppendLine(",");
					sb.Append("\t\t\"bongosScale\": ").Append(kvp.Value.bongosScale.ToString("F2", CultureInfo.InvariantCulture));
					sb.AppendLine();
					count++;
					if (count < _offsetsMap.Count) {
						sb.AppendLine("\t},");
					} else {
						sb.AppendLine("\t}");
					}
				}
				sb.AppendLine("}");
				File.WriteAllText(filePath, sb.ToString());
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Failed to save offsets file: " + ex.ToString());
			}
		}

		private void HandleEditorInteraction() {
			if (!_isHatDragModeEnabled && !_isBongosDragModeEnabled) return;

			BongoCat.Cat cat = BongoCat.Cat.Instance;
			if (cat == null) return;

			BongoCat.Draggable draggable = cat.GetComponentInParent<BongoCat.Draggable>();
			if (draggable != null && _fDraggableIgnoring != null) {
				_fDraggableIgnoring.SetValue(draggable, true);
			}

			LoadedSkin activeSkin = GetActiveSkin();
			if (activeSkin == null) return;
			string charName = activeSkin.Config.name;

			SkinOffsets curOffsets;
			if (!_offsetsMap.TryGetValue(charName, out curOffsets)) {
				curOffsets = new SkinOffsets {
					hatOffsetX = activeSkin.Config.hatOffsetX,
					hatOffsetY = activeSkin.Config.hatOffsetY,
					hatScale = activeSkin.Config.hatScale > 0f ? activeSkin.Config.hatScale : DefaultScale,
					bongosOffsetX = activeSkin.Config.bongosOffsetX,
					bongosOffsetY = activeSkin.Config.bongosOffsetY,
					bongosScale = activeSkin.Config.bongosScale > 0f ? activeSkin.Config.bongosScale : DefaultScale
				};
				_offsetsMap[charName] = curOffsets;
			}

			if (Input.GetMouseButtonDown(0)) {
				_isDragging = true;
				_dragStartMousePos = Input.mousePosition;
				if (_isHatDragModeEnabled) {
					_dragStartOffsets = new Vector2(curOffsets.hatOffsetX, curOffsets.hatOffsetY);
				} else if (_isBongosDragModeEnabled) {
					_dragStartOffsets = new Vector2(curOffsets.bongosOffsetX, curOffsets.bongosOffsetY);
				}
				if (draggable != null) {
					_frozenCatPosition = draggable.transform.position;
				}
			}

			if (Input.GetMouseButton(0) && _isDragging) {
				if (draggable != null) {
					draggable.transform.position = _frozenCatPosition;
				}

				Canvas rootCanvas = cat.GetComponentInParent<Canvas>();
				float factor = (rootCanvas != null && rootCanvas.scaleFactor > 0f) ? rootCanvas.scaleFactor : DefaultScale;
				Vector2 delta = ((Vector2)Input.mousePosition - _dragStartMousePos) / factor;

				if (_isHatDragModeEnabled) {
					curOffsets.hatOffsetX = Mathf.Round(_dragStartOffsets.x + delta.x);
					curOffsets.hatOffsetY = Mathf.Round(_dragStartOffsets.y + delta.y);
					if (activeSkin != null) {
						activeSkin.Config.hatOffsetX = curOffsets.hatOffsetX;
						activeSkin.Config.hatOffsetY = curOffsets.hatOffsetY;
					}
				} else if (_isBongosDragModeEnabled) {
					curOffsets.bongosOffsetX = Mathf.Round(_dragStartOffsets.x + delta.x);
					curOffsets.bongosOffsetY = Mathf.Round(_dragStartOffsets.y + delta.y);
					if (activeSkin != null) {
						activeSkin.Config.bongosOffsetX = curOffsets.bongosOffsetX;
						activeSkin.Config.bongosOffsetY = curOffsets.bongosOffsetY;
					}
				}
			}

			if (Input.GetMouseButtonUp(0) && _isDragging) {
				if (draggable != null) {
					draggable.transform.position = _frozenCatPosition;
				}
				SaveOffsetsFile();
				if (_isHatDragModeEnabled) {
					Logger.LogInfo("CustomSkins", string.Format("Hat offset updated for [{0}]: hatOffset=({1:F1}, {2:F1}), hatScale={3:F2}", charName, curOffsets.hatOffsetX, curOffsets.hatOffsetY, curOffsets.hatScale));
				} else if (_isBongosDragModeEnabled) {
					Logger.LogInfo("CustomSkins", string.Format("Bongos offset updated for [{0}]: bongosOffset=({1:F1}, {2:F1}), bongosScale={3:F2}", charName, curOffsets.bongosOffsetX, curOffsets.bongosOffsetY, curOffsets.bongosScale));
				}
				_isDragging = false;
			}

			float scroll = Input.mouseScrollDelta.y;
			if (Mathf.Abs(scroll) > ScrollThreshold) {
				if (_isHatDragModeEnabled) {
					curOffsets.hatScale = Mathf.Clamp(curOffsets.hatScale + scroll * ScaleStepPerScroll, MinScaleLimit, MaxScaleLimit);
					if (activeSkin != null) {
						activeSkin.Config.hatScale = curOffsets.hatScale;
					}
					SaveOffsetsFile();
					Logger.LogInfo("CustomSkins", string.Format("Hat scale updated for [{0}]: hatScale={1:F2}", charName, curOffsets.hatScale));
				} else if (_isBongosDragModeEnabled) {
					curOffsets.bongosScale = Mathf.Clamp(curOffsets.bongosScale + scroll * ScaleStepPerScroll, MinScaleLimit, MaxScaleLimit);
					if (activeSkin != null) {
						activeSkin.Config.bongosScale = curOffsets.bongosScale;
					}
					SaveOffsetsFile();
					Logger.LogInfo("CustomSkins", string.Format("Bongos scale updated for [{0}]: bongosScale={1:F2}", charName, curOffsets.bongosScale));
				}
			}
		}

		private Sprite ResolveSprite(string dirPath, string prefix, string altPrefix, string suffix) {
			try {
				if (!string.IsNullOrEmpty(prefix)) {
					string fullName = prefix + suffix + ImageExtension;
					Sprite s = LoadSpriteFromFile(Path.Combine(dirPath, fullName));
					if (s != null) return s;
				}

				if (!string.IsNullOrEmpty(altPrefix) && !string.Equals(altPrefix, prefix, StringComparison.OrdinalIgnoreCase)) {
					string altName = altPrefix + suffix + ImageExtension;
					Sprite s = LoadSpriteFromFile(Path.Combine(dirPath, altName));
					if (s != null) return s;
				}

				string standardName = suffix + ImageExtension;
				return LoadSpriteFromFile(Path.Combine(dirPath, standardName));
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in ResolveSprite: " + ex.ToString());
			}
			return null;
		}

		private Sprite LoadSpriteFromFile(string path) {
			try {
				if (!File.Exists(path)) return null;
				byte[] data = File.ReadAllBytes(path);
				Texture2D tex = new Texture2D(PlaceholderTextureDimension, PlaceholderTextureDimension, TextureFormat.RGBA32, false);
				if (ImageConversion.LoadImage(tex, data)) {
					int srcW = tex.width;
					int srcH = tex.height;
					Color32[] srcPixels = tex.GetPixels32();

					int dstW = srcW + SpriteBorderPaddingDouble;
					int dstH = srcH + SpriteBorderPaddingDouble;
					Color32[] dstPixels = new Color32[dstW * dstH];

					for (int y = 0; y < srcH; y++) {
						int srcRow = y * srcW;
						int dstRow = (y + SpriteBorderPadding) * dstW + SpriteBorderPadding;
						for (int x = 0; x < srcW; x++) {
							Color32 p = srcPixels[srcRow + x];
							if (p.a == 0) {
								p.r = 0;
								p.g = 0;
								p.b = 0;
							}
							dstPixels[dstRow + x] = p;
						}
					}

					Texture2D paddedTex = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false);
					paddedTex.filterMode = FilterMode.Bilinear;
					paddedTex.SetPixels32(dstPixels);
					paddedTex.Apply(false, false);
					UnityEngine.Object.Destroy(tex);

					return Sprite.Create(paddedTex, new Rect(SpriteBorderPaddingFloat, SpriteBorderPaddingFloat, (float)srcW, (float)srcH), new Vector2(SpritePivotCenter, SpritePivotCenter), DefaultPixelsPerUnit);
				}
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error loading sprite from " + path + ": " + ex.ToString());
			}
			return null;
		}

		private void RegisterAllSpritesInImageCache() {
			try {
				if (BongoCat.MemoryImageCache.Instance == null) return;

				FieldInfo fCache = typeof(BongoCat.MemoryImageCache).GetField("_spriteCache", BindingFlags.NonPublic | BindingFlags.Instance);
				if (fCache == null) return;

				Dictionary<string, Sprite> cache = (Dictionary<string, Sprite>)fCache.GetValue(BongoCat.MemoryImageCache.Instance);
				if (cache == null) return;

				for (int i = 0; i < _loadedSkins.Count; i++) {
					LoadedSkin skin = _loadedSkins[i];
					string baseName = skin.Config.name.Replace(" ", "");

					if (skin.SpriteIcon != null) {
						cache[baseName] = skin.SpriteIcon;
						cache[baseName + SuffixIcon] = skin.SpriteIcon;
					}
					if (skin.SpriteLeft != null) {
						cache[baseName + SuffixLeft] = skin.SpriteLeft;
						cache[baseName + SuffixRight] = skin.SpriteRight != null ? skin.SpriteRight : skin.SpriteLeft;
					}
					if (skin.SpriteLeftPunch != null) {
						cache[baseName + SuffixLeftPunch] = skin.SpriteLeftPunch;
					}
					if (skin.SpriteRightPunch != null) {
						cache[baseName + SuffixRightPunch] = skin.SpriteRightPunch;
					}
					if (skin.SpriteBothPunch != null) {
						cache[baseName + SuffixBothPunch] = skin.SpriteBothPunch;
					}
					if (skin.SpritePetting != null) {
						cache[baseName + SuffixPetting] = skin.SpritePetting;
					}
					if (skin.SpriteSleeping != null) {
						cache[baseName + SuffixSleeping] = skin.SpriteSleeping;
					}
				}
				Logger.LogInfo("CustomSkins", "Registered all custom skin sprites in MemoryImageCache._spriteCache!");
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in RegisterAllSpritesInImageCache: " + ex.ToString());
			}
		}

		private bool InjectOwnership(int itemId) {
			try {
				ItemData key = new ItemData { id = itemId };
				SteamItemDetails_t steamDetails = new SteamItemDetails_t {
					m_itemId = new SteamItemInstanceID_t((ulong)itemId),
					m_iDefinition = new SteamItemDef_t(itemId),
					m_unQuantity = DefaultItemQuantity,
					m_unFlags = DefaultItemFlags
				};
				ItemDetail detail = new ItemDetail {
					ItemDetails = steamDetails
				};

				FieldInfo fIndex = typeof(Heathen.SteamworksIntegration.API.Inventory.Client).GetField("_itemIndex", BindingFlags.NonPublic | BindingFlags.Static);
				if (fIndex != null) {
					Dictionary<ItemData, List<ItemDetail>> dict = (Dictionary<ItemData, List<ItemDetail>>)fIndex.GetValue(null);
					if (dict == null) {
						dict = new Dictionary<ItemData, List<ItemDetail>>();
						fIndex.SetValue(null, dict);
					}

					List<ItemDetail> list = new List<ItemDetail>();
					list.Add(detail);
					dict[key] = list;
					Logger.LogInfo("CustomSkins", "Successfully injected ownership in Client._itemIndex for ID=" + itemId.ToString());
					return true;
				}
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in InjectOwnership: " + ex.ToString());
			}
			return false;
		}

		private void EnsureCustomOwnership() {
			try {
				if (_fItemIndex == null) {
					_fItemIndex = typeof(Heathen.SteamworksIntegration.API.Inventory.Client).GetField("_itemIndex", BindingFlags.NonPublic | BindingFlags.Static);
				}
				if (_fItemIndex == null) return;

				Dictionary<ItemData, List<ItemDetail>> dict = (Dictionary<ItemData, List<ItemDetail>>)_fItemIndex.GetValue(null);
				if (dict == null) {
					dict = new Dictionary<ItemData, List<ItemDetail>>();
					_fItemIndex.SetValue(null, dict);
				}

				for (int i = 0; i < _loadedSkins.Count; i++) {
					int itemId = _loadedSkins[i].Config.id;
					ItemData key = new ItemData { id = itemId };
					if (!dict.ContainsKey(key)) {
						SteamItemDetails_t steamDetails = new SteamItemDetails_t {
							m_itemId = new SteamItemInstanceID_t((ulong)itemId),
							m_iDefinition = new SteamItemDef_t(itemId),
							m_unQuantity = DefaultItemQuantity,
							m_unFlags = DefaultItemFlags
						};
						ItemDetail detail = new ItemDetail {
							ItemDetails = steamDetails
						};
						List<ItemDetail> list = new List<ItemDetail>();
						list.Add(detail);
						dict[key] = list;
					}
				}
			} catch {}
		}

		private void UpdateCustomUiItems() {
			if (BongoCat.CatInventory.Instance == null || !BongoCat.CatInventory.Instance.gameObject.activeInHierarchy) {
				return;
			}

			_uiUpdateTimer += Time.deltaTime;
			if (_uiUpdateTimer < UiUpdateInterval) return;
			_uiUpdateTimer = 0f;

			try {
				if (_fUnowned == null) {
					_fUnowned = typeof(BongoCat.InventoryItem).GetField("_unownedOverlay", BindingFlags.NonPublic | BindingFlags.Instance);
				}
				if (_fDisabledOverlay == null) {
					_fDisabledOverlay = typeof(BongoCat.InventoryItem).GetField("_disabledOverlay", BindingFlags.NonPublic | BindingFlags.Instance);
				}

				LoadedSkin activeSkin = GetActiveSkin();
				HashSet<int> nextDisabled = new HashSet<int>();

#pragma warning disable 0618
				BongoCat.InventoryItem[] allItems = UnityEngine.Object.FindObjectsOfType<BongoCat.InventoryItem>();
#pragma warning restore 0618
				for (int i = 0; i < allItems.Length; i++) {
					BongoCat.InventoryItem item = allItems[i];
					if (item == null || item.SteamItem == null) continue;

					if (_skinsById.ContainsKey(item.SteamItem.Id)) {
						item.SteamItem.IsNew = false;
						if (_fUnowned != null) {
							Image unownedImg = (Image)_fUnowned.GetValue(item);
							if (unownedImg != null && unownedImg.gameObject.activeSelf) {
								unownedImg.gameObject.SetActive(false);
							}
						}
					}

					if (_fDisabledOverlay != null) {
						Image disabledImg = (Image)_fDisabledOverlay.GetValue(item);
						if (disabledImg != null) {
							bool isIncompatible = activeSkin != null && activeSkin.Config.IsItemIncompatible(item.SteamItem.Id, item.SteamItem.ItemName, item.SteamItem.FullImage != null ? item.SteamItem.FullImage.name : null);
							if (isIncompatible) {
								nextDisabled.Add(item.SteamItem.Id);
								if (!disabledImg.gameObject.activeSelf) {
									disabledImg.gameObject.SetActive(true);
								}
								disabledImg.enabled = true;
							} else if (_forceDisabledItemIds.Contains(item.SteamItem.Id)) {
								bool normalDisabled = (BongoCat.ItemExchange.Instance != null && !BongoCat.ItemExchange.Instance.IsItemSlottable(item.SteamItem)) || (Steam.TrashCan.Instance != null && Steam.TrashCan.Instance.IsItemInTrash(item.SteamItem));
								disabledImg.enabled = normalDisabled;
							}
						}
					}
				}

				_forceDisabledItemIds.Clear();
				foreach (int id in nextDisabled) {
					_forceDisabledItemIds.Add(id);
				}
			} catch {}
		}

		private void HookGameEvents() {
			if (_eventsHooked) return;

			try {
				BongoCat.Cat cat = BongoCat.Cat.Instance;
				if (cat != null) {
					BongoCat.MouseDownEvent[] mdes = cat.GetComponentsInChildren<BongoCat.MouseDownEvent>(true);
					for (int i = 0; i < mdes.Length; i++) {
						if (mdes[i] != null) {
							if (mdes[i].onMouseDown == null) {
								mdes[i].onMouseDown = new UnityEngine.Events.UnityEvent();
							}
							mdes[i].onMouseDown.AddListener(OnCatPatted);
						}
					}

					Image bodyImg = _fBodyImage != null ? (Image)_fBodyImage.GetValue(cat) : null;
					if (bodyImg != null) {
						bodyImg.raycastTarget = true;
						BongoCat.MouseDownEvent bodyMde = bodyImg.GetComponent<BongoCat.MouseDownEvent>();
						if (bodyMde == null) {
							bodyMde = bodyImg.gameObject.AddComponent<BongoCat.MouseDownEvent>();
						}
						if (bodyMde.onMouseDown == null) {
							bodyMde.onMouseDown = new UnityEngine.Events.UnityEvent();
						}
						bodyMde.onMouseDown.AddListener(OnCatPatted);
					}

					Image rightPaw = _fRightPawImage != null ? (Image)_fRightPawImage.GetValue(cat) : null;
					if (rightPaw != null) {
						BongoCat.MouseDownEvent pawMde = rightPaw.GetComponent<BongoCat.MouseDownEvent>();
						if (pawMde == null) {
							pawMde = rightPaw.gameObject.AddComponent<BongoCat.MouseDownEvent>();
						}
						if (pawMde.onMouseDown == null) {
							pawMde.onMouseDown = new UnityEngine.Events.UnityEvent();
						}
						pawMde.onMouseDown.AddListener(OnCatPatted);
					}

					_eventsHooked = true;
					Logger.LogInfo("CustomSkins", "Hooked into Cat MouseDownEvents for joy reactions!");
				}
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in HookGameEvents: " + ex.ToString());
			}
		}

		private void OnCatPatted() {
			if (_isHatDragModeEnabled || _isBongosDragModeEnabled) return;

			BongoCat.Cat cat = BongoCat.Cat.Instance;
			if (cat == null) return;

			if (_afkDetector != null && _fIsAfk != null && (bool)_fIsAfk.GetValue(_afkDetector)) {
				return;
			}

			_lastPatTime = Time.realtimeSinceStartup;
			_pendingPetReaction = true;
			_punchStartedForPat = false;
			_petTimer = 0f;

			if (cat.catPatter != null) {
				cat.catPatter.Pat();
			}
		}

		private void TryInjectInventory() {
			try {
				if (BongoCat.SteamItemIdReference.Inst == null || BongoCat.CatInventory.Instance == null) {
					return;
				}

				BongoCat.SteamItemIdReference idRef = BongoCat.SteamItemIdReference.Inst;
				BongoCat.CatInventory inventory = BongoCat.CatInventory.Instance;

				RegisterAllSpritesInImageCache();

				FieldInfo fRuntime = typeof(BongoCat.SteamItemIdReference).GetField("RuntimeItems", BindingFlags.NonPublic | BindingFlags.Instance);
				Dictionary<int, BongoCat.SteamItemRuntime> runtimeMap = null;
				if (fRuntime != null) {
					runtimeMap = (Dictionary<int, BongoCat.SteamItemRuntime>)fRuntime.GetValue(idRef);
				}

				FieldInfo fSkins = typeof(BongoCat.CatInventory).GetField("_skinsCollection", BindingFlags.NonPublic | BindingFlags.Instance);
				BongoCat.ItemCollection skinsColl = fSkins != null ? (BongoCat.ItemCollection)fSkins.GetValue(inventory) : null;
				MethodInfo mAdd = typeof(BongoCat.ItemCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance);

				for (int i = 0; i < _loadedSkins.Count; i++) {
					LoadedSkin skin = _loadedSkins[i];
					int itemId = skin.Config.id;

					InjectOwnership(itemId);

					BongoCat.SteamJsonParser.SteamItemBasic basic = null;
					if (idRef.AllItemData != null) {
						for (int j = 0; j < idRef.AllItemData.Count; j++) {
							if (idRef.AllItemData[j].Id == itemId) {
								basic = idRef.AllItemData[j];
								break;
							}
						}
					}

					if (basic == null && idRef.AllItemData != null) {
						basic = new BongoCat.SteamJsonParser.SteamItemBasic();
						basic.Id = itemId;
						basic.Name = !string.IsNullOrEmpty(skin.Config.displayName) ? skin.Config.displayName : skin.Config.name;
						basic.Description = skin.Config.description;
						basic.Tags = new List<string>(new string[] { "itemslot:skin", "slot:skin", "type:cosmetic", "quality:" + skin.Config.quality.ToLowerInvariant() });
						basic.Hidden = false;
						basic.GameOnly = false;
						idRef.AllItemData.Add(basic);
					}

					BongoCat.SteamItemRuntime runtime = null;
					if (runtimeMap != null && runtimeMap.ContainsKey(itemId)) {
						runtime = runtimeMap[itemId];
					}

					if (runtime == null && basic != null) {
						runtime = new BongoCat.SteamItemRuntime(basic);
						if (runtimeMap != null) {
							runtimeMap[itemId] = runtime;
						}
					}

					if (runtime != null) {
						skin.RuntimeItem = runtime;
						runtime.ItemSlot = "skin";
						runtime.IsNew = false;
						if (basic != null) {
							basic.GameOnly = false;
						}
						if (skin.SpriteIcon != null) {
							runtime.Icon = skin.SpriteIcon;
							runtime.FullImage = skin.SpriteIcon;
						}

						if (inventory.Items != null && !inventory.Items.Contains(runtime)) {
							inventory.Items.Add(runtime);
						}

						if (skinsColl != null) {
							bool inColl = false;
							if (skinsColl.CollectionItems != null) {
								for (int k = 0; k < skinsColl.CollectionItems.Count; k++) {
									BongoCat.InventoryItem ii = skinsColl.CollectionItems[k];
									if (ii != null && ii.SteamItem != null && ii.SteamItem.Id == itemId) {
										inColl = true;
										break;
									}
								}
							}

							if (!inColl && mAdd != null) {
								mAdd.Invoke(skinsColl, new object[] { runtime, true });
							}
						}
					}
				}

				if (skinsColl != null) {
					skinsColl.SortCollection();
				}

				MethodInfo mItemUpdated = typeof(BongoCat.InventoryItem).GetMethod("OnItemUpdated", BindingFlags.NonPublic | BindingFlags.Instance);
				FieldInfo fUnowned = typeof(BongoCat.InventoryItem).GetField("_unownedOverlay", BindingFlags.NonPublic | BindingFlags.Instance);
#pragma warning disable 0618
				BongoCat.InventoryItem[] allItems = UnityEngine.Object.FindObjectsOfType<BongoCat.InventoryItem>();
#pragma warning restore 0618
				for (int i = 0; i < allItems.Length; i++) {
					BongoCat.InventoryItem item = allItems[i];
					if (item != null && item.SteamItem != null && _skinsById.ContainsKey(item.SteamItem.Id)) {
						LoadedSkin s = _skinsById[item.SteamItem.Id];
						if (s.SpriteIcon != null) {
							item.SteamItem.Icon = s.SpriteIcon;
							item.SteamItem.FullImage = s.SpriteIcon;
						}

						if (fUnowned != null) {
							Image unownedImg = (Image)fUnowned.GetValue(item);
							if (unownedImg != null && unownedImg.gameObject.activeSelf) {
								unownedImg.gameObject.SetActive(false);
							}
						}

						if (mItemUpdated != null) {
							mItemUpdated.Invoke(item, null);
						}
					}
				}

				_inventoryInjected = true;
				Logger.LogInfo("CustomSkins", "All custom skins fully injected with ownership and collection entries!");
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in TryInjectInventory: " + ex.ToString());
			}
		}

		public LoadedSkin GetActiveSkin() {
			if (_loadedSkins.Count == 0) return null;

			try {
				BongoCat.CatCosmetics cosmetics = BongoCat.CatCosmetics.Instance;
				if (cosmetics != null) {
					if (cosmetics.EquippedItems != null) {
						for (int i = 0; i < cosmetics.EquippedItems.Count; i++) {
							BongoCat.SteamItemRuntime item = cosmetics.EquippedItems[i];
							if (item != null && _skinsById.ContainsKey(item.Id)) {
								return _skinsById[item.Id];
							}
						}
					}

					if (_fLocallyEquipped != null) {
						List<BongoCat.SteamItemRuntime> local = (List<BongoCat.SteamItemRuntime>)_fLocallyEquipped.GetValue(cosmetics);
						if (local != null) {
							for (int i = local.Count - 1; i >= 0; i--) {
								BongoCat.SteamItemRuntime item = local[i];
								if (item != null && _skinsById.ContainsKey(item.Id)) {
									LoadedSkin skin = _skinsById[item.Id];
									local.RemoveAt(i);
									if (_mEquip != null) {
										_mEquip.Invoke(cosmetics, new object[] { item, true, false });
									}
									return skin;
								}
							}
						}
					}

					if (cosmetics.Skin != null && _skinsById.ContainsKey(cosmetics.Skin.Id)) {
						return _skinsById[cosmetics.Skin.Id];
					}
				}

				int savedSkinId = PlayerPrefs.GetInt(SavedCustomSkinPrefKey, 0);
				if (savedSkinId > 0 && _skinsById.ContainsKey(savedSkinId)) {
					return _skinsById[savedSkinId];
				}
			} catch {}

			return null;
		}

		private void Update() {
			EnsureCustomOwnership();

			if (!_inventoryInjected) {
				TryInjectInventory();
			}

			if (!_eventsHooked) {
				HookGameEvents();
			}

			HandleEditorInteraction();

			if (Input.GetKeyDown(HatEditorKey)) {
				_isHatDragModeEnabled = !_isHatDragModeEnabled;
				if (_isHatDragModeEnabled) {
					_isBongosDragModeEnabled = false;
				}
				_isDragging = false;
				Logger.LogInfo("CustomSkins", "Toggled Hat Drag via F9! Enabled=" + _isHatDragModeEnabled.ToString());
			}

			if (Input.GetKeyDown(BongosEditorKey)) {
				_isBongosDragModeEnabled = !_isBongosDragModeEnabled;
				if (_isBongosDragModeEnabled) {
					_isHatDragModeEnabled = false;
				}
				_isDragging = false;
				Logger.LogInfo("CustomSkins", "Toggled Bongos Drag via F10! Enabled=" + _isBongosDragModeEnabled.ToString());
			}

			if (_petTimer > 0f) {
				_petTimer -= Time.deltaTime;
			}
		}

		private void LateUpdate() {
			try {
				UpdateCustomUiItems();

				BongoCat.Cat cat = BongoCat.Cat.Instance;
				if (cat == null) return;

				if (_isDragging) {
					BongoCat.Draggable draggable = cat.GetComponentInParent<BongoCat.Draggable>();
					if (draggable != null) {
						draggable.transform.position = _frozenCatPosition;
					}
				}

				LoadedSkin activeSkin = GetActiveSkin();
				if (activeSkin != null) {
					_wasCustomActive = true;
					string charName = activeSkin.Config.name;
					SkinOffsets curOffsets;
					if (!_offsetsMap.TryGetValue(charName, out curOffsets)) {
						curOffsets = new SkinOffsets {
							hatOffsetX = activeSkin.Config.hatOffsetX,
							hatOffsetY = activeSkin.Config.hatOffsetY,
							hatScale = activeSkin.Config.hatScale > 0f ? activeSkin.Config.hatScale : DefaultScale,
							bongosOffsetX = activeSkin.Config.bongosOffsetX,
							bongosOffsetY = activeSkin.Config.bongosOffsetY,
							bongosScale = activeSkin.Config.bongosScale > 0f ? activeSkin.Config.bongosScale : DefaultScale
						};
						_offsetsMap[charName] = curOffsets;
					}

					BongoCat.CatCosmetics cosmetics = BongoCat.CatCosmetics.Instance;
					if (cosmetics != null && cosmetics.EquippedItems != null) {
						for (int i = cosmetics.EquippedItems.Count - 1; i >= 0; i--) {
							BongoCat.SteamItemRuntime eq = cosmetics.EquippedItems[i];
							if (eq != null && activeSkin.Config.IsItemIncompatible(eq.Id, eq.ItemName, eq.FullImage != null ? eq.FullImage.name : null)) {
								Logger.LogInfo("CustomSkins", "Unequipping incompatible item: " + eq.ItemName);
								if (_mUnequip != null) {
									_mUnequip.Invoke(cosmetics, new object[] { eq, false });
								} else {
									cosmetics.EquippedItems.RemoveAt(i);
									eq.IsEquipped = false;
									if (eq.OnItemUpdated != null) {
										eq.OnItemUpdated();
									}
									cat.ClearItemSlot(eq.ItemSlot);
								}
							}
						}
					}

					if (cosmetics != null && _fLocallyEquipped != null) {
						List<BongoCat.SteamItemRuntime> local = (List<BongoCat.SteamItemRuntime>)_fLocallyEquipped.GetValue(cosmetics);
						if (local != null) {
							for (int i = local.Count - 1; i >= 0; i--) {
								BongoCat.SteamItemRuntime eq = local[i];
								if (eq != null && activeSkin.Config.IsItemIncompatible(eq.Id, eq.ItemName, eq.FullImage != null ? eq.FullImage.name : null)) {
									local.RemoveAt(i);
									if (_mUnequip != null) {
										_mUnequip.Invoke(cosmetics, new object[] { eq, true });
									}
								}
							}
						}
					}

					float hatPulseAlpha = Mathf.Lerp(EditorDragHatMinAlpha, EditorDragMaxAlpha, (Mathf.Sin(Time.unscaledTime * EditorPulseSpeed) + 1f) * EditorPulseMidpoint);
					float bodyPulseAlpha = Mathf.Lerp(EditorDragBodyMinAlpha, EditorDragMaxAlpha, (Mathf.Sin(Time.unscaledTime * EditorPulseSpeed) + 1f) * EditorPulseMidpoint);

					Image rightPaw = _fRightPawImage != null ? (Image)_fRightPawImage.GetValue(cat) : null;
					if (rightPaw != null) {
						if (!rightPaw.gameObject.activeSelf) {
							rightPaw.gameObject.SetActive(true);
						}
						rightPaw.raycastTarget = true;
						if (activeSkin.Config.fullBody) {
							rightPaw.color = Color.clear;
						} else {
							rightPaw.color = _isBongosDragModeEnabled ? new Color(1f, 1f, 1f, bodyPulseAlpha) : Color.white;
						}
					}

					Image bodyImage = _fBodyImage != null ? (Image)_fBodyImage.GetValue(cat) : null;
					if (bodyImage != null) {
						bodyImage.raycastTarget = true;
					}

					GameObject hatObj = _fHatObject != null ? (GameObject)_fHatObject.GetValue(cat) : null;
					if (hatObj != null) {
						if (activeSkin.Config.disableClothing) {
							if (hatObj.activeSelf) hatObj.SetActive(false);
						} else {
							if (!hatObj.activeSelf) hatObj.SetActive(true);

							RectTransform hatRect = hatObj.transform as RectTransform;
							if (hatRect != null) {
								if (!_hasDefaultHatTransform) {
									_defaultHatAnchoredPos = hatRect.anchoredPosition;
									_defaultHatScale = hatRect.localScale;
									_hasDefaultHatTransform = true;
								}

								float scaleFactor = curOffsets.hatScale > 0f ? curOffsets.hatScale : DefaultScale;
								hatRect.anchoredPosition = _defaultHatAnchoredPos + new Vector2(curOffsets.hatOffsetX, curOffsets.hatOffsetY);
								hatRect.localScale = new Vector3(_defaultHatScale.x * scaleFactor, _defaultHatScale.y * scaleFactor, _defaultHatScale.z);

								Image hatImg = _fHatImage != null ? (Image)_fHatImage.GetValue(cat) : null;
								if (hatImg != null) {
									float hAlpha = _isHatDragModeEnabled ? hatPulseAlpha : 1f;
									hatImg.color = new Color(1f, 1f, 1f, hAlpha);
								}
							}
						}
					}

					Image bongosLB = _fBongosLeftBack != null ? (Image)_fBongosLeftBack.GetValue(cat) : null;
					Image bongosLF = _fBongosLeftFront != null ? (Image)_fBongosLeftFront.GetValue(cat) : null;
					Image bongosR = _fBongosRight != null ? (Image)_fBongosRight.GetValue(cat) : null;

					float bAlpha = _isBongosDragModeEnabled ? bodyPulseAlpha : 1f;
					if (bongosLB != null) {
						bongosLB.color = new Color(1f, 1f, 1f, bAlpha);
					}
					if (bongosLF != null) {
						bongosLF.color = new Color(1f, 1f, 1f, bAlpha);
					}
					if (bongosR != null) {
						bongosR.color = new Color(1f, 1f, 1f, bAlpha);
					}

					if (!_hasDefaultBongosTransform) {
						if (bongosLB != null) {
							_defBongosLBPos = bongosLB.rectTransform.anchoredPosition;
							_defBongosLBScale = bongosLB.rectTransform.localScale;
						}
						if (bongosLF != null) {
							_defBongosLFPos = bongosLF.rectTransform.anchoredPosition;
							_defBongosLFScale = bongosLF.rectTransform.localScale;
						}
						if (bongosR != null) {
							_defBongosRPos = bongosR.rectTransform.anchoredPosition;
							_defBongosRScale = bongosR.rectTransform.localScale;
						}
						_hasDefaultBongosTransform = true;
					}

					Vector2 bOffset = new Vector2(curOffsets.bongosOffsetX, curOffsets.bongosOffsetY);
					float bScale = curOffsets.bongosScale > 0f ? curOffsets.bongosScale : DefaultScale;

					if (bongosLB != null) {
						bongosLB.rectTransform.anchoredPosition = _defBongosLBPos + bOffset;
						bongosLB.rectTransform.localScale = new Vector3(_defBongosLBScale.x * bScale, _defBongosLBScale.y * bScale, _defBongosLBScale.z);
					}
					if (bongosLF != null) {
						bongosLF.rectTransform.anchoredPosition = _defBongosLFPos + bOffset;
						bongosLF.rectTransform.localScale = new Vector3(_defBongosLFScale.x * bScale, _defBongosLFScale.y * bScale, _defBongosLFScale.z);
					}
					if (bongosR != null) {
						bongosR.rectTransform.anchoredPosition = _defBongosRPos + bOffset;
						bongosR.rectTransform.localScale = new Vector3(_defBongosRScale.x * bScale, _defBongosRScale.y * bScale, _defBongosRScale.z);
					}

					bool isSleeping = false;
					if (_afkDetector == null) {
						_afkDetector = cat.GetComponentInChildren<BongoCat.Multiplayer.AfkDetector>(true);
						if (_afkDetector == null) {
							_afkDetector = UnityEngine.Object.FindAnyObjectByType<BongoCat.Multiplayer.AfkDetector>();
						}
					}
					if (_afkDetector != null && _fIsAfk != null) {
						isSleeping = (bool)_fIsAfk.GetValue(_afkDetector);
					}

					Sprite idleSprite;
					Sprite bothPunchSprite;
					if (activeSkin.Config.invertIdleAndBothPunch) {
						idleSprite = activeSkin.SpriteBothPunch != null ? activeSkin.SpriteBothPunch : activeSkin.SpriteLeft;
						bothPunchSprite = activeSkin.SpriteLeft;
					} else {
						idleSprite = activeSkin.SpriteLeft;
						bothPunchSprite = activeSkin.SpriteBothPunch != null ? activeSkin.SpriteBothPunch : activeSkin.SpriteLeft;
					}

					bool leftPunch = false;
					bool rightPunch = false;

					if (_fRoutineLeft != null && _fRoutineLeft.GetValue(cat) != null) {
						leftPunch = true;
					}
					if (_fRoutineRight != null && _fRoutineRight.GetValue(cat) != null) {
						rightPunch = true;
					}

					bool isPunching = (leftPunch || rightPunch);

					if (_pendingPetReaction) {
						if (isPunching) {
							_punchStartedForPat = true;
						} else if (_punchStartedForPat) {
							_punchStartedForPat = false;
							_pendingPetReaction = false;
							_petTimer = PettingDurationSeconds;
						} else if (Time.realtimeSinceStartup - _lastPatTime > PatPunchThresholdSeconds) {
							_pendingPetReaction = false;
							_petTimer = PettingDurationSeconds;
						}
					} else if (isPunching) {
						_petTimer = 0f;
					}

					if (!activeSkin.Config.fullBody && rightPaw != null) {
						if (rightPunch && activeSkin.SpriteRightPunch != null) {
							rightPaw.sprite = activeSkin.SpriteRightPunch;
						} else if (activeSkin.SpriteRight != null) {
							rightPaw.sprite = activeSkin.SpriteRight;
						}
					}

					Sprite targetSprite = idleSprite;

					if (leftPunch && rightPunch && bothPunchSprite != null) {
						targetSprite = bothPunchSprite;
					} else if (leftPunch && activeSkin.SpriteLeftPunch != null) {
						targetSprite = activeSkin.SpriteLeftPunch;
					} else if (rightPunch && activeSkin.Config.fullBody && activeSkin.SpriteRightPunch != null) {
						targetSprite = activeSkin.SpriteRightPunch;
					} else if (_petTimer > 0f) {
						targetSprite = activeSkin.SpritePetting != null ? activeSkin.SpritePetting : idleSprite;
					} else if (isSleeping) {
						targetSprite = activeSkin.SpriteSleeping != null ? activeSkin.SpriteSleeping : idleSprite;
					} else {
						targetSprite = idleSprite;
					}

					if (targetSprite != null && bodyImage != null) {
						bodyImage.sprite = targetSprite;
						bool dimBody = _isBongosDragModeEnabled && (activeSkin.Config.fullBody || bongosLF == null || !bongosLF.gameObject.activeInHierarchy);
						bodyImage.color = dimBody ? new Color(1f, 1f, 1f, bodyPulseAlpha) : Color.white;
						bodyImage.enabled = true;
					}
				} else {
					if (_wasCustomActive) {
						Image rightPaw = _fRightPawImage != null ? (Image)_fRightPawImage.GetValue(cat) : null;
						if (rightPaw != null) {
							rightPaw.color = Color.white;
							rightPaw.gameObject.SetActive(true);
						}
						_wasCustomActive = false;
					}

					GameObject hatObj = _fHatObject != null ? (GameObject)_fHatObject.GetValue(cat) : null;
					if (hatObj != null) {
						if (!hatObj.activeSelf) hatObj.SetActive(true);
						RectTransform hatRect = hatObj.transform as RectTransform;
						if (hatRect != null && _hasDefaultHatTransform) {
							hatRect.anchoredPosition = _defaultHatAnchoredPos;
							hatRect.localScale = _defaultHatScale;
						}
						Image hatImg = _fHatImage != null ? (Image)_fHatImage.GetValue(cat) : null;
						if (hatImg != null) hatImg.color = Color.white;
					}

					Image bongosLB = _fBongosLeftBack != null ? (Image)_fBongosLeftBack.GetValue(cat) : null;
					Image bongosLF = _fBongosLeftFront != null ? (Image)_fBongosLeftFront.GetValue(cat) : null;
					Image bongosR = _fBongosRight != null ? (Image)_fBongosRight.GetValue(cat) : null;

					if (bongosLB != null) {
						if (!bongosLB.gameObject.activeSelf) bongosLB.gameObject.SetActive(true);
						bongosLB.color = Color.white;
						if (_hasDefaultBongosTransform) {
							bongosLB.rectTransform.anchoredPosition = _defBongosLBPos;
							bongosLB.rectTransform.localScale = _defBongosLBScale;
						}
					}

					if (bongosLF != null) {
						if (!bongosLF.gameObject.activeSelf) bongosLF.gameObject.SetActive(true);
						bongosLF.color = Color.white;
						if (_hasDefaultBongosTransform) {
							bongosLF.rectTransform.anchoredPosition = _defBongosLFPos;
							bongosLF.rectTransform.localScale = _defBongosLFScale;
						}
					}

					if (bongosR != null) {
						if (!bongosR.gameObject.activeSelf) bongosR.gameObject.SetActive(true);
						bongosR.color = Color.white;
						if (_hasDefaultBongosTransform) {
							bongosR.rectTransform.anchoredPosition = _defBongosRPos;
							bongosR.rectTransform.localScale = _defBongosRScale;
						}
					}
				}
			} catch (Exception ex) {
				Logger.LogError("CustomSkins", "Error in LateUpdate: " + ex.ToString());
			}
		}
	}
}
