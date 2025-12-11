namespace Sorolla.DebugUI
{
    /// <summary>
    ///     Base class for components that change based on SDK mode (Prototype vs Full).
    /// </summary>
    public abstract class ModeComponentBase : UIComponentBase
    {
        protected bool IsPrototype => Sorolla.Config == null || Sorolla.Config.isPrototypeMode;

        void Start() => ApplyTheme();

        void HandleModeChanged(SorollaMode mode) => ApplyTheme();

        protected override void UnsubscribeFromEvents() => SorollaDebugEvents.OnModeChanged -= HandleModeChanged;

        protected override void SubscribeToEvents() => SorollaDebugEvents.OnModeChanged += HandleModeChanged;
    }
}
