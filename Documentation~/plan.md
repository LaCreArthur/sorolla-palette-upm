# Sorolla Palette - Development Plan

**Publisher SDK Package** - Like Voodoo Sauce or Homa Belly for mobile games.

---

## 🎯 **Goal**

Create a UPM package providing a complete mobile publisher stack with:
- **Prototype Mode**: GA + Facebook SDK + optional MAX (CPI testing)
- **Full Mode**: GA + MAX + Adjust (production)

---

## 📦 **Architecture Overview**

### UPM Embedded Package Development
- Package code lives in `Packages/com.sorolla.palette/` (writeable during development)
- Users install via Git URL → Unity auto-installs dependencies
- Test in `Sorolla_Testbed` project, distribute via GitHub

### Auto-Dependencies (package.json)
- **GameAnalytics** - Always included (analytics + remote config) - both modes
- **AppLovin MAX** - Auto-installed, optional in Prototype Mode, required in Full Mode
- **External Dependency Manager** - Android/iOS dependency resolution

### Manual SDKs (guided by Configuration Window)
- **Facebook SDK** - .unitypackage, required for Prototype Mode only (handles UA tracking)
- **Adjust SDK** - .unitypackage, required for Full Mode only (not used in Prototype)

### Module System
- SDK adapters in `Modules~/` (hidden from users)
- Enabled via scripting defines: `SOROLLA_FACEBOOK_ENABLED`, `SOROLLA_MAX_ENABLED`, `SOROLLA_ADJUST_ENABLED`
- No file copying - modules compile when defines are present

---

## 📋 **Development Phases**

### Phase 0: Testbed Setup ✅ COMPLETE
- [x] Create Unity project: `Sorolla_Testbed` (Unity 2022.3+)
- [x] Create `Packages/com.sorolla.palette/` folder structure:
  - `Runtime/` - Core code (SorollaPalette.cs, config)
  - `Editor/` - Configuration window, mode selector
  - `Modules~/Facebook/` - Facebook adapter
  - `Modules~/MAX/` - MAX adapter
  - `Modules~/Adjust/` - Adjust adapter
  - `Documentation~/` - These docs
- [x] Create `package.json` (no dependencies - setup script handles them)
- [x] Auto-setup script (SorollaPaletteSetup.cs):
  - Adds Google registry (https://unityregistry-pa.googleapis.com/) for com.google packages
  - Adds OpenUPM scoped registry to project manifest.json
  - Adds GameAnalytics SDK dependency (7.10.6)
  - Adds External Dependency Manager dependency (Git URL)
- [x] Create assembly definition files (.asmdef) for Runtime, Editor, each Module
- [x] Unity auto-installs GameAnalytics & EDM

### Phase 1: Core Runtime
- [ ] Create `Runtime/SorollaPalette.cs` - Static API singleton
- [ ] Create `Runtime/SorollaPaletteConfig.cs` - ScriptableObject config
- [ ] Initialize GameAnalytics (always - both modes)
- [ ] Conditional initialization for MAX/Facebook/Adjust (based on enabled modules)
- [ ] Implement wrapper functions:
  - Analytics: `TrackProgressionEvent()`, `TrackDesignEvent()`, `TrackResourceEvent()`
  - Remote Config: `GetRemoteConfigValue()`, `GetRemoteConfigInt()`, `IsRemoteConfigReady()`
  - Ads: `ShowRewardedAd()`, `ShowInterstitialAd()` (Full Mode only)

### Phase 2: SDK Adapters
- [ ] **Modules~/MAX/MaxAdapter.cs**:
```csharp
#if SOROLLA_MAX_ENABLED
public static class MaxAdapter {
    public static void Initialize(string sdkKey) {
        MaxSdk.SetSdkKey(sdkKey);
        MaxSdk.InitializeSdk();
    }
}
#endif
```
- [ ] **Modules~/Facebook/FacebookAdapter.cs**:
```csharp
#if SOROLLA_FACEBOOK_ENABLED
public static class FacebookAdapter {
    public static void Initialize() {
        FB.Init();
    }
}
#endif
```
- [ ] **Modules~/Adjust/AdjustAdapter.cs** (optional for Full Mode)

### Phase 3: Editor Tools
- [ ] **Mode Selection Wizard** (`SorollaPaletteModeSelector.cs`):
  - Auto-show on first import (`[InitializeOnLoadMethod]`)
  - Two buttons: [Prototype Mode] [Full Mode]
  - Save selection: `EditorPrefs.SetString("SorollaPalette_Mode", "Prototype"/"Full")`
  
- [ ] **Configuration Window** (`SorollaPaletteWindow.cs`):
  - Show current mode at top
  - SDK status indicators (✅ Installed / ❌ Not Found)
  - Enable/disable module buttons (adds scripting defines)
  - Context-aware fields (show only enabled module configs)
  - Create/save SorollaPaletteConfig.asset

- [ ] **SDK Detection**:
```csharp
bool IsFacebookInstalled() => Type.GetType("Facebook.Unity.FB, Facebook.Unity") != null;
bool IsAdjustInstalled() => Type.GetType("com.adjust.sdk.Adjust, Assembly-CSharp") != null;
```

- [ ] **Scripting Define Management**:
```csharp
void AddScriptingDefine(string define) {
    string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(...);
    defines += ";" + define;
    PlayerSettings.SetScriptingDefineSymbolsForGroup(..., defines);
}
```

### Phase 4: Testing
- [ ] Create test scene in `Sorolla_Testbed/Assets/Scenes/`
- [ ] Test GameAnalytics init (should work immediately)
- [ ] Test Mode Selection Wizard
- [ ] Import Facebook SDK manually, enable module, test
- [ ] Test Configuration Window
- [ ] Build APK, test on device

### Phase 5: Distribution
- [ ] Initialize Git in `Packages/com.sorolla.palette/`
- [ ] Push to GitHub: `https://github.com/yourusername/sorolla-palette.git`
- [ ] Create tag: `v1.0.0`
- [ ] Create GitHub Release
- [ ] **Integration Test**: Fresh Unity project → Install via Git URL → Test full workflow

---

## 📊 **Current Progress**

```
Phase 0 (Testbed Setup):        ██████████ 100% (5/5) ✅ COMPLETE
Phase 1 (Core Runtime):         ░░░░░░░░░░   0% (0/5)
Phase 2 (SDK Adapters):         ░░░░░░░░░░   0% (0/3)
Phase 3 (Editor Tools):         ░░░░░░░░░░   0% (0/5)
Phase 4 (Testing):              ░░░░░░░░░░   0% (0/5)
Phase 5 (Distribution):         ░░░░░░░░░░   0% (0/5)

TOTAL: ████░░░░░░ 20% (5/25 tasks) - Ready for Phase 1!
```

---

## 🎯 **Next Steps**

1. **Create Testbed Project** 
   - New Unity project: `Sorolla_Testbed`
   - Set up `Packages/com.sorolla.palette/` structure

2. **Create package.json** 
   - Define dependencies (GA, MAX)
   - Unity auto-installs them

3. **Move Existing Code** 
   - `Assets/SorollaPalette/` → `Packages/com.sorolla.palette/Runtime/`
   - Create Modules~/ folders

4. **Create SDK Adapters** 
   - MaxAdapter, FacebookAdapter, AdjustAdapter
   - Wrap with `#if SOROLLA_*_ENABLED`

5. **Create Mode Wizard** 
   - Auto-show editor window
   - Mode selection UI
   - SDK detection

6. **Update Config Window** 
   - Add mode display
   - Add module enable buttons
   - Add scripting define management

7. **Test & Release** 
   - Test in testbed
   - Push to GitHub
   - Integration test
