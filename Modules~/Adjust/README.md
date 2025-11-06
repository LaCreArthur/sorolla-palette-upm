# Adjust Module

This module provides Adjust SDK integration for Sorolla Palette.

## Purpose
- Required for **Full Mode** only
- Not used in Prototype Mode (Facebook handles UA tracking)
- Handles attribution tracking
- Receives ad revenue forwarding from MAX

## Activation
The module is activated when:
1. Adjust SDK is installed (.unitypackage)
2. `SOROLLA_ADJUST_ENABLED` scripting define is added
3. Configuration is set in Sorolla Palette Config Window

## Files
- `AdjustAdapter.cs` - Main adapter class
- `SorollaPalette.Adjust.asmdef` - Assembly definition

## Dependencies
- Adjust SDK for Unity (manual install)

## Note
Adjust requires a paid subscription (~$2000+/month). Only needed for production apps in Full Mode.
# Facebook Module

This module provides Facebook SDK integration for Sorolla Palette.

## Purpose
- Required for **Prototype Mode** only
- Handles Facebook Analytics events
- Provides UA tracking functionality

## Activation
The module is activated when:
1. Facebook SDK is installed (.unitypackage)
2. `SOROLLA_FACEBOOK_ENABLED` scripting define is added
3. Configuration is set in Sorolla Palette Config Window

## Files
- `FacebookAdapter.cs` - Main adapter class
- `SorollaPalette.Facebook.asmdef` - Assembly definition

## Dependencies
- Facebook SDK for Unity (manual install)

