# MAX Module

This module provides AppLovin MAX integration for Sorolla Palette.

## Purpose
- Optional in **Prototype Mode**
- Required in **Full Mode**
- Handles ad mediation (rewarded, interstitial, banner)
- Revenue tracking and forwarding to Adjust (Full Mode)

## Activation
The module is activated when:
1. AppLovin MAX SDK is installed (auto-installed via package.json)
2. `SOROLLA_MAX_ENABLED` scripting define is added
3. Configuration is set in Sorolla Palette Config Window

## Files
- `MaxAdapter.cs` - Main adapter class
- `SorollaPalette.MAX.asmdef` - Assembly definition

## Dependencies
- AppLovin MAX SDK (auto-installed)

