using Ghumante.Characters;
using Ghumante.Characters.Rides;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Services;
using Ghumante.Platform.Regions;
using Ghumante.UI.Hud;
using Ghumante.World.Life;
using Ghumante.World.Rendering;
using UnityEngine;

namespace Ghumante.App.Explore
{
    /// <summary>
    /// Flying from TIA in the Explore session (owner playtest #11, flight package): the <see cref="AircraftRide"/> (parked
    /// aircraft on the real stands, boarding with the Action button, the flight model, its cameras) and the
    /// <see cref="AircraftHud"/>. Kept in its own file so the session's main flow only carries a few one-line hooks:
    /// <see cref="FlightFrame"/> before the explorer's tick (which is skipped while flying), <see cref="ShowBoardPrompt"/>
    /// in the HUD update, <see cref="FlightLateUpdate"/> before the chase rig (which rests while flying) and
    /// <see cref="CloseFlight"/> on close. The ride is created once the world's life host is up and the region has an
    /// airport; without one nothing here does anything.
    /// </summary>
    public sealed partial class ExploreSession
    {
        private AircraftRide _aircraft;
        private AircraftHud _aircraftHud;
        private float _aircraftRetryS;
        private bool _flightUnavailable, _flyingShown;

        /// <summary>True while the explorer flies an aircraft (the session ticks the aircraft, not the explorer).</summary>
        public bool Flying
        {
            get { return _aircraft != null && _aircraft.Flying; }
        }

        /// <summary>The flyable aircraft of the region (null before the life host is up or without an airport).</summary>
        public AircraftRide Aircraft
        {
            get { return _aircraft; }
        }

        /// <summary>Creates the ride when the life host knows the airport (checked once a second until then).</summary>
        private void EnsureAircraft(float dt)
        {
            if (_aircraft != null || _flightUnavailable || _world == null || _toonMaterial == null || _screen == null) return;
            _aircraftRetryS -= dt;
            if (_aircraftRetryS > 0f) return;
            _aircraftRetryS = 1f;
            LifeHost life = _world.Life;
            if (life == null) return;
            if (!life.HasAirport || life.Aviation == null)
            {
                _flightUnavailable = true;
                return;
            }
            RegionManifest m = _world.Manifest;
            bool bounds = m != null && m.HasBounds;
            _aircraft = AircraftRide.Create(_toonMaterial, life.Aviation, bounds ? m.MinX : double.MaxValue, bounds ? m.MinZ : double.MaxValue,
                                            bounds ? m.MaxX : double.MinValue, bounds ? m.MaxZ : double.MinValue);
            if (_aircraft == null)
            {
                _flightUnavailable = true;
                return;
            }
            _aircraft.Notice += OnNotice;
            _aircraftHud = new AircraftHud(_screen.StyledRoot, _localizer, _haptics);
        }

        /// <summary>
        /// The aircraft's frame, before the explorer's tick: boards when the Action button is pressed by a parked aircraft
        /// (the press is consumed) and flies while boarded. Returns true while flying (the explorer is not ticked then).
        /// </summary>
        private bool FlightFrame(float dt, ref ControlFrame frame, bool blocked)
        {
            EnsureAircraft(dt);
            if (_aircraft == null || _explorer == null) return false;
            FlightTouch touch = _aircraftHud != null && _aircraftHud.Visible ? _aircraftHud.Read() : new FlightTouch { Throttle = -1f };
            if ((touch.Touched || touch.Active) && _touchVisibility != null) _touchVisibility.Update(TouchControlsVisibility.Source.Touch);
            bool flying = _aircraft.Frame(dt, ref frame, touch, _explorer, _world, _rig, blocked);
            if (flying != _flyingShown) OnFlyingChanged(flying);
            if (!flying) return false;
            _screen.SetTouchControlsVisible(false);
            if (_aircraftHud != null)
            {
                _aircraftHud.SetTouchVisible(_touchVisibility == null || _touchVisibility.Visible);
                _aircraftHud.UpdateHud(_aircraft.Model.Instruments(), _aircraft.Lever, dt);
            }
            FlightEvents e = _aircraft.LastEvents;
            if (!blocked && e != FlightEvents.None)
            {
                if ((e & (FlightEvents.Bounce | FlightEvents.Bump)) != 0) _haptics.Play(HapticKind.HeavyImpact);
                else if ((e & FlightEvents.Touchdown) != 0) _haptics.Play(HapticKind.MediumImpact);
                else if ((e & (FlightEvents.LiftOff | FlightEvents.ResetRunway | FlightEvents.ResetAir)) != 0) _haptics.Play(HapticKind.LightImpact);
                else if ((e & FlightEvents.Stall) != 0) _haptics.Play(HapticKind.Warning);
            }
            return true;
        }

        private void OnFlyingChanged(bool flying)
        {
            _flyingShown = flying;
            if (flying)
            {
                if (_aircraftHud != null) _aircraftHud.Show(true, _aircraft.Model.Spec.Craft);
                _screen.SetPrompt(null);
                _screen.SetPassengerOffered(false);
                _screen.SetTouchControlsVisible(false);
                _haptics.Play(HapticKind.MediumImpact);
                return;
            }
            if (_aircraftHud != null) _aircraftHud.Show(false);
            _screen.SetTouchControlsVisible(_touchVisibility == null || _touchVisibility.Visible);
            if (_explorer != null) _screen.SetLayout(_explorer.Layout);
        }

        /// <summary>On foot by a parked aircraft: the Action button reads "Board" and the prompt names the aircraft.
        /// Returns true when it did (the explorer's own prompt waits).</summary>
        private bool ShowBoardPrompt()
        {
            if (_aircraft == null || !_aircraft.Offered) return false;
            _screen.SetAction("hud.action.board", "hop", false);
            _screen.SetPrompt("hud.prompt.board", CraftNameKey(_aircraft.OfferedCraft));
            _screen.SetPassengerOffered(false);
            return true;
        }

        /// <summary>Localisation key of a craft's generic name (never a type or airline name).</summary>
        public static string CraftNameKey(FlightCraft craft)
        {
            switch (craft)
            {
                case FlightCraft.TurbopropStretch: return "flight.craft.turboprop_stretch";
                case FlightCraft.Commuter: return "flight.craft.commuter";
                case FlightCraft.Stol: return "flight.craft.stol";
                case FlightCraft.Helicopter: return "flight.craft.helicopter";
                default: return "flight.craft.turboprop";
            }
        }

        /// <summary>
        /// LateUpdate's flight part: draws the parked (and flown) aircraft and, while flying, places the camera from the
        /// aircraft's chase or cockpit view instead of the chase rig. Returns true while flying (the rig then rests).
        /// </summary>
        private bool FlightLateUpdate()
        {
            if (_aircraft == null) return false;
            Camera cam = _rig.Camera;
            bool flying = _aircraft.Flying;
            if (flying)
            {
                _aircraft.UpdateCamera(cam, Time.deltaTime, _frame, _world.Origin);
                WorldPos p = _aircraft.Position, o = _world.Origin;
                ToonLook.SetOccluderTarget(new Vector3((float)(p.X - o.X), p.Y - o.Y, (float)(p.Z - o.Z)));
                if (cam != null) _screen.SetHeading(cam.transform.eulerAngles.y * Mathf.Deg2Rad);
            }
            if (cam != null) _aircraft.Draw(_world.Origin, cam.transform.position);
            return flying;
        }

        private void CloseFlight()
        {
            if (_aircraft != null)
            {
                _aircraft.Notice -= OnNotice;
                _aircraft.Dispose();
                _aircraft = null;
            }
            if (_aircraftHud != null)
            {
                _aircraftHud.Dispose();
                _aircraftHud = null;
            }
            _flyingShown = false;
            _flightUnavailable = false;
            _aircraftRetryS = 0f;
        }
    }
}
