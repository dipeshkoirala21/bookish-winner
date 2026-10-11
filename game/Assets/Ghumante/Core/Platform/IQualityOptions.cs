using System;

namespace Ghumante.Core.Platform
{
    /// <summary>
    /// The graphics settings as the Settings sheet sees them (UI/Screens/SettingsSheet "Graphics quality" and "Frame
    /// rate"). App implements it (<c>Ghumante.App.QualityDirector</c>), applies every change at once and persists it;
    /// tests use a fake. Main thread.
    /// </summary>
    public interface IQualityOptions
    {
        /// <summary>The player's choice (Auto by default).</summary>
        GraphicsChoice Choice { get; }

        /// <summary>The tier Auto uses now: what the device check picked, or lower when the adaptive governor stepped it
        /// down. Shown on the Auto option ("Auto (Medium)").</summary>
        QualityTier AutoTier { get; }

        /// <summary>The tier the device check picked at launch.</summary>
        QualityTier DetectedTier { get; }

        /// <summary>The tier in use (the choice resolved).</summary>
        QualityTier ActiveTier { get; }

        /// <summary>The frame rate in use (30 or 60).</summary>
        int FrameRate { get; }

        /// <summary>The highest frame rate <see cref="ActiveTier"/> allows (60 needs Medium or High).</summary>
        int MaxFrameRate { get; }

        /// <summary>True while the world still draws with an older tier: its streets redraw when the Settings sheet
        /// closes (the look, frame rate and resolution change at once).</summary>
        bool WorldRedrawPending { get; }

        /// <summary>Raised after any of the above changed (a pick, the governor, the world redraw finishing).</summary>
        event Action Changed;

        /// <summary>Picks a graphics choice; applied at once and saved.</summary>
        void SetChoice(GraphicsChoice choice);

        /// <summary>Picks 30 or 60 fps (clamped to <see cref="MaxFrameRate"/> while the tier is lower); saved.</summary>
        void SetFrameRate(int fps);
    }
}
