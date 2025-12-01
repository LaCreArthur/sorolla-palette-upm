# Prototype Mode Test Sample

Beautiful test UI for Sorolla SDK in **Prototype mode** (GameAnalytics + Facebook).

## Features

- 🎨 Modern dark theme UI
- 📦 Dynamic SDK detection - only shows relevant sections
- 🔄 Self-wiring buttons - no manual setup required
- 📱 Mobile-friendly with touch support

## Usage

### Option 1: Create via Menu
1. Open any scene
2. Go to **Sorolla > Create Prototype Test UI**
3. Press Play!

### Option 2: Use the Sample Scene
1. Open `Scenes/PrototypeTestScene.unity`
2. Press Play!

## Test Sections

| Section | Requires | Description |
|---------|----------|-------------|
| **📈 Analytics** | GameAnalytics | Design, Progression, Resource events |
| **⚙️ Remote Config** | GA or Firebase RC | Fetch and read config values |
| **📘 Facebook** | Facebook SDK | Check FB status (events via TrackDesign) |
| **🔥 Crashlytics** | Firebase Crashlytics | Log messages, exceptions, test crashes |

## Toggle Panel

- Click the 🧪 button in the top-right corner
- Or triple-tap the button area on mobile

## SDK Detection

The UI automatically detects which SDKs are installed and hides/shows sections accordingly:

```csharp
// Sections are shown/hidden based on:
- GameAnalytics: GAMEANALYTICS_INSTALLED
- Facebook: SOROLLA_FACEBOOK_ENABLED  
- Firebase Analytics: FIREBASE_ANALYTICS_INSTALLED
- Firebase Crashlytics: FIREBASE_CRASHLYTICS_INSTALLED
- Firebase Remote Config: FIREBASE_REMOTE_CONFIG_INSTALLED
```

## Customization

The `PrototypeTestUI` component is self-wiring. Button names follow the convention:
```
ButtonName_MethodName
```

For example, `DesignBtn_TestTrackDesign` will automatically call `TestTrackDesign()`.
