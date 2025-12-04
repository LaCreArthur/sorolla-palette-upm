# Sorolla Debug Panel - Unity Implementation Plan

> Based on React mockup v2.3.0-beta  
> Last Updated: December 2, 2025

---

## 📋 Overview

A professional, mobile-optimized debug panel for testing Sorolla SDK features. Supports two modes:
- **Prototype Mode**: Facebook + GameAnalytics focused (no ads)
- **Full Mode**: Adjust + MAX Mediation + GameAnalytics (with ads testing)

---

## 🎯 Key Features

| Feature | Description |
|---------|-------------|
| **Mode Switching** | Toggle between Prototype/Full mode dynamically |
| **Minimizable** | Floating button when collapsed |
| **5 Tabs** | Dashboard, Ads*, Events, Tools, Logs (*Full mode only) |
| **Toast System** | Non-blocking notifications with auto-dismiss |
| **SDK Health** | Live status indicators for all integrated SDKs |
| **Device Info** | Copy IDFA/GAID, FB App ID, Adjust Token |
| **Ads Tester** | Load/Show interstitial, rewarded, banner |
| **Event Triggers** | Progression, Resources, Custom events |
| **Remote Config** | Fetch & view live config values |
| **Crashlytics** | Log exceptions, force crash |
| **Console Log** | Categorized, filterable, auto-scroll |

---

## 📁 Project Structure

```
Assets/PrototypeTest/
├── Prefabs/
│   ├── SorollaDebugPanel.prefab       # Main panel prefab
│   ├── Toast.prefab                    # Toast notification
│   └── LogEntry.prefab                 # Pooled log line
│
├── Scripts/
│   ├── Core/
│   │   ├── DebugPanelController.cs     # Main controller, singleton
│   │   ├── DebugPanelState.cs          # Runtime state (ScriptableObject)
│   │   ├── ToastManager.cs             # Toast queue & display
│   │   └── TabManager.cs               # Tab switching logic
│   │
│   ├── Tabs/
│   │   ├── IDebugTab.cs                # Tab interface
│   │   ├── DashboardTab.cs             # Device info, SDK health, toggles
│   │   ├── AdsTab.cs                   # Ad mediation tester
│   │   ├── EventsTab.cs                # Analytics event triggers
│   │   ├── ToolsTab.cs                 # Remote Config, CMP, Crashlytics
│   │   └── LogsTab.cs                  # Console log viewer
│   │
│   ├── Components/
│   │   ├── StatusDot.cs                # Animated SDK status indicator
│   │   ├── StatusRow.cs                # Label + StatusDot row
│   │   ├── ToggleSwitch.cs             # iOS-style toggle
│   │   ├── AdCard.cs                   # Ad type card with state
│   │   ├── CopyButton.cs               # Tap-to-copy with feedback
│   │   ├── TabButton.cs                # Bottom nav button
│   │   ├── ConfigValueRow.cs           # Key-value display for Remote Config
│   │   └── LogEntryView.cs             # Single log line (pooled)
│   │
│   └── Utils/
│       ├── DeviceInfo.cs               # Platform-specific device data
│       ├── FpsCounter.cs               # Performance monitor
│       ├── ObjectPool.cs               # Generic object pooling
│       └── ClipboardHelper.cs          # Cross-platform clipboard
│
├── ScriptableObjects/
│   ├── DebugPanelColors.asset          # Color palette
│   └── DebugPanelConfig.asset          # Default settings
│
└── Animations/
    ├── PanelSlideIn.anim
    ├── PanelSlideOut.anim
    ├── ToastFadeIn.anim
    └── StatusDotPulse.anim
```

---

## 🏗️ Architecture

### Core Principles

| Principle | Implementation |
|-----------|---------------|
| **KISS** | Each tab is self-contained; no complex inheritance |
| **DRY** | Reusable components (StatusRow, AdCard, CopyButton) |
| **SOLID** | Single responsibility; IDebugTab interface |
| **Mobile Perf** | Object pooling, event-driven, no Update() spam |

### State Management

```csharp
public enum SDKMode { Prototype, Full }
public enum AdState { Idle, Loading, Ready, Showing }

[CreateAssetMenu(fileName = "DebugPanelState", menuName = "Sorolla/Debug Panel State")]
public class DebugPanelState : ScriptableObject
{
    [Header("Mode")]
    public SDKMode CurrentMode;
    
    [Header("SDK Status")]
    public bool GameAnalyticsActive;
    public bool FacebookActive;      // Prototype only
    public bool AdjustActive;        // Full only
    public bool MaxMediationActive;  // Full only
    public bool CrashlyticsActive;
    public bool RemoteConfigActive;
    public bool IsInitialized;
    
    [Header("Ads")]
    public AdState InterstitialState;
    public AdState RewardedState;
    public bool BannerVisible;
    
    [Header("Toggles")]
    public bool DebugMode;
    public bool GodMode;
    
    [Header("Network")]
    public bool IsOnline;
    
    // Events
    public event Action OnStateChanged;
    public event Action<SDKMode> OnModeChanged;
    
    public void NotifyStateChanged() => OnStateChanged?.Invoke();
    
    public void SetMode(SDKMode mode)
    {
        CurrentMode = mode;
        // Auto-configure based on mode
        FacebookActive = mode == SDKMode.Prototype;
        AdjustActive = mode == SDKMode.Full;
        MaxMediationActive = mode == SDKMode.Full;
        OnModeChanged?.Invoke(mode);
        NotifyStateChanged();
    }
}
```

### Tab Interface

```csharp
public interface IDebugTab
{
    string TabId { get; }
    void OnTabActivated();
    void OnTabDeactivated();
    void RefreshData();
    bool IsAvailableInMode(SDKMode mode);
}
```

### Toast System

```csharp
public enum ToastType { Info, Success, Warning, Error }

public class ToastManager : MonoBehaviour
{
    [SerializeField] Transform toastContainer;
    [SerializeField] Toast toastPrefab;
    [SerializeField] float displayDuration = 3f;
    
    Queue<(string message, ToastType type)> toastQueue = new();
    Toast currentToast;
    
    public void Show(string message, ToastType type = ToastType.Info)
    {
        toastQueue.Enqueue((message, type));
        TryShowNext();
    }
    
    // ... implementation
}
```

---

## 🎨 UI Components

### Color System

```csharp
[CreateAssetMenu(fileName = "DebugPanelColors", menuName = "Sorolla/Debug Panel Colors")]
public class DebugPanelColors : ScriptableObject
{
    [Header("Backgrounds")]
    public Color Background = new Color(0.04f, 0.04f, 0.05f);  // zinc-950
    public Color Surface = new Color(0.09f, 0.09f, 0.10f);     // zinc-900
    public Color SurfaceAlt = new Color(0.15f, 0.15f, 0.16f);  // zinc-800
    
    [Header("Text")]
    public Color TextPrimary = Color.white;
    public Color TextSecondary = new Color(0.63f, 0.63f, 0.68f); // zinc-400
    public Color TextMuted = new Color(0.44f, 0.44f, 0.48f);     // zinc-500
    
    [Header("Accent")]
    public Color Indigo = new Color(0.39f, 0.45f, 0.95f);   // indigo-500
    public Color Amber = new Color(0.96f, 0.75f, 0.14f);    // amber-400
    
    [Header("Status")]
    public Color Success = new Color(0.20f, 0.83f, 0.60f);  // emerald-400
    public Color Warning = new Color(0.98f, 0.75f, 0.14f);  // amber-400
    public Color Error = new Color(0.94f, 0.30f, 0.30f);    // red-400
    
    [Header("Mode Colors")]
    public Color PrototypeAccent => Amber;
    public Color FullAccent => Indigo;
}
```

### Component Specifications

| Component | Description | Key Properties |
|-----------|-------------|----------------|
| **StatusDot** | Animated circle | `bool isActive`, pulse animation when active |
| **StatusRow** | Label + StatusDot | `string label`, dims when inactive |
| **ToggleSwitch** | iOS-style toggle | `bool value`, `Action<bool> onChanged` |
| **AdCard** | Ad type card | `AdType type`, `AdState state`, Load/Show buttons |
| **CopyButton** | Tap to copy | `string value`, `string label`, brief "Copied!" feedback |
| **TabButton** | Nav button | `Sprite icon`, `string label`, `bool isActive` |
| **ConfigValueRow** | Key-value pair | `string key`, `string value`, monospace font |
| **LogEntryView** | Log line | `LogEntry data`, category color tag |

---

## 📱 Tab Content

### Dashboard Tab
- **Device Identity Card**
  - IDFA/GAID (copy)
  - Mode-specific: FB App ID (Prototype) / Adjust Token (Full)
- **SDK Health Card**
  - 2x2 grid of StatusRows
  - Shows: GameAnalytics, Facebook/Adjust, MAX Ads, Crashlytics
  - Inactive SDKs are dimmed
- **Quick Toggles Card**
  - Debug Mode toggle
  - God Mode toggle

### Ads Tab (Full Mode Only)
- **Mediation Tester Header**
  - Shows "AppLovin MAX • Unity Ads • Ironsource"
- **Interstitial Card** (indigo accent)
  - State badge (Idle/Loading/Ready/Showing)
  - Load + Show buttons
- **Rewarded Card** (amber accent)
  - Same as interstitial
- **Banner Card** (gray accent)
  - Toggle switch to show/hide

### Events Tab
- **Progression Card**
  - Start / Win / Fail buttons (3-column grid)
- **Resources Card**
  - Add Coins / Spend Coins buttons
- **Custom Events Card**
  - Track 'Jump' button
  - Track 'NPC Talk' button

### Tools Tab
- **Remote Config Card** (amber accent)
  - Fetch & Activate button
  - Live values display (key-value rows)
- **Privacy & CMP Card**
  - Reset Consent / Show ATT buttons
- **Crashlytics Card** (red accent)
  - Log Exception button
  - Force Crash button

### Logs Tab
- **Filter Buttons** (All/Info/Success/Warning/Error)
- **Scrollable Log List**
  - Timestamp + Category tag + Message
  - Color-coded by type
  - Object pooled
  - Auto-scroll to bottom
- **Clear Button**

---

## ⚡ Performance Optimizations

1. **Object Pooling**
   - Log entries recycled (pool size: 100)
   - Toast instances reused

2. **Canvas Optimization**
   - Separate canvas for toast overlay
   - CanvasGroup for fade animations (avoid rebuilds)
   - Disable raycast on non-interactive elements

3. **Event-Driven Updates**
   - No Update() polling
   - State changes trigger UI refreshes
   - Use UnityEvents for button wiring

4. **Lazy Tab Loading**
   - Tab content only initialized on first activation
   - Inactive tabs are disabled, not destroyed

5. **String Optimization**
   - StringBuilder for log formatting
   - Cached timestamp format

6. **Layout Rebuilds**
   - Batch UI updates
   - Use ContentSizeFitter sparingly

---

## 🎬 Animation Strategy

Using Unity Animator or DOTween/LeanTween:

| Animation | Duration | Easing |
|-----------|----------|--------|
| Panel slide in | 300ms | EaseOutCubic |
| Panel slide out | 200ms | EaseInCubic |
| Toast fade in | 200ms | EaseOutQuad |
| Toast fade out | 300ms | EaseInQuad |
| Status dot pulse | 1500ms | Sine, loop |
| Tab content fade | 150ms | Linear |
| Button press scale | 100ms | EaseOutBack |

---

## 📋 Implementation Phases

### Phase 1: Core Framework ✅
- [ ] DebugPanelController (singleton, minimize/restore)
- [ ] DebugPanelState (ScriptableObject)
- [ ] TabManager (tab switching, mode-aware)
- [ ] Basic UI shell (header, content area, nav bar)
- [ ] Mode toggle in header

### Phase 2: Toast System
- [ ] ToastManager
- [ ] Toast prefab with animations
- [ ] Integration with logging

### Phase 3: Dashboard Tab
- [ ] CopyButton component
- [ ] StatusDot with animation
- [ ] StatusRow component
- [ ] ToggleSwitch component
- [ ] Device Identity card
- [ ] SDK Health card
- [ ] Quick Toggles card

### Phase 4: Logs Tab
- [ ] LogEntryView component
- [ ] ObjectPool for log entries
- [ ] LogsTab with filtering
- [ ] Auto-scroll behavior
- [ ] Clear functionality

### Phase 5: Events Tab
- [ ] Progression card (Start/Win/Fail)
- [ ] Resources card (Add/Spend)
- [ ] Custom Events card

### Phase 6: Ads Tab
- [ ] AdCard component with state machine
- [ ] Interstitial/Rewarded cards
- [ ] Banner toggle card
- [ ] Mode-conditional visibility

### Phase 7: Tools Tab
- [ ] Remote Config card with live values
- [ ] ConfigValueRow component
- [ ] Privacy & CMP card
- [ ] Crashlytics card

### Phase 8: Polish
- [ ] All animations implemented
- [ ] Safe area handling (notch, home indicator)
- [ ] Haptic feedback (optional)
- [ ] FPS counter in header
- [ ] Network status indicator
- [ ] Final color/spacing pass

---

## 🔗 Integration Points

```csharp
// How game code will use the debug panel

// Show toast from anywhere
SorollaDebug.Toast("Level Complete!", ToastType.Success);

// Log with category
SorollaDebug.Log("Player jumped", LogType.Info, "Game");

// Check if debug mode is on
if (SorollaDebug.IsDebugMode) { /* ... */ }

// Check if god mode is on
if (SorollaDebug.IsGodMode) { /* ... */ }

// Get current mode
if (SorollaDebug.CurrentMode == SDKMode.Prototype) { /* ... */ }
```

---

## 📝 Notes

- Panel designed for 1080x1920 reference resolution (scale with screen size)
- Supports both portrait and landscape (responsive layout)
- Dark theme only (matches modern debug tools aesthetic)
- Follows Unity UI best practices for mobile performance
- Consider adding unit tests for state management
