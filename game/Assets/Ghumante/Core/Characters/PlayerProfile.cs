using System;
using System.Collections.Generic;
using Ghumante.Core.Driving;
using Ghumante.Core.Save;

namespace Ghumante.Core.Characters
{
    /// <summary>One vehicle the player owns: a catalogue entry, its livery and its own plate number.</summary>
    public struct GarageVehicle
    {
        /// <summary>Index into <see cref="VehicleCatalog"/>.</summary>
        public int Variant;

        public byte Livery;

        /// <summary>Seeds the plate number (VehiclePlates), so the player's scooter keeps its plate.</summary>
        public uint PlateSeed;

        public GarageVehicle(int variant, byte livery, uint plateSeed)
        {
            Variant = variant;
            Livery = livery;
            PlateSeed = plateSeed;
        }
    }

    /// <summary>
    /// The player's own vehicles (W2-O5, W2_DESIGN 6.2): summoned with a whistle, they arrive from a road at least 60 m
    /// away within 20 s and never pop into view. The starter garage holds a scooter, a bicycle and a small hatchback
    /// (so every rig can be tried from the first ride). Saved as <c>player.garage</c>. No theft anywhere: the only other
    /// vehicles the player drives are the community fleet's.
    /// </summary>
    public sealed class Garage
    {
        public const int MaxVehicles = 24;

        /// <summary>A summoned vehicle starts this far away (on a road node), and arrives within this time.</summary>
        public const float SummonMinDistanceM = 60f, SummonMaxS = 20f;

        public readonly List<GarageVehicle> Vehicles = new List<GarageVehicle>();

        /// <summary>The vehicle the whistle calls (index into <see cref="Vehicles"/>).</summary>
        public int Selected;

        public static Garage Starter(uint seed)
        {
            var g = new Garage();
            g.Vehicles.Add(new GarageVehicle(VehicleCatalog.Scooter, 1, CharMath.Hash(seed, 1u)));
            g.Vehicles.Add(new GarageVehicle(VehicleCatalog.Bicycle, 2, CharMath.Hash(seed, 2u)));
            g.Vehicles.Add(new GarageVehicle(VehicleCatalog.Hatchback, 4, CharMath.Hash(seed, 3u)));
            return g;
        }

        public GarageVehicle Current
        {
            get { return Vehicles.Count == 0 ? new GarageVehicle(VehicleCatalog.Scooter, 0, 1u) : Vehicles[Clamp(Selected)]; }
        }

        /// <summary>True when an entry of this catalogue variant is in the garage.</summary>
        public bool Owns(int variant)
        {
            for (int i = 0; i < Vehicles.Count; i++)
                if (Vehicles[i].Variant == variant) return true;
            return false;
        }

        /// <summary>Adds a vehicle (unlocks); false when it is already there, not drivable or the garage is full.</summary>
        public bool Add(GarageVehicle v)
        {
            if (v.Variant < 0 || v.Variant >= VehicleCatalog.Count || !VehicleCatalog.At(v.Variant).PlayerDrivable) return false;
            if (Vehicles.Count >= MaxVehicles) return false;
            for (int i = 0; i < Vehicles.Count; i++)
                if (Vehicles[i].Variant == v.Variant && Vehicles[i].Livery == v.Livery) return false;
            Vehicles.Add(v);
            return true;
        }

        /// <summary>Selects the next vehicle (the garage button cycles).</summary>
        public GarageVehicle Next()
        {
            if (Vehicles.Count > 0) Selected = (Clamp(Selected) + 1) % Vehicles.Count;
            return Current;
        }

        private int Clamp(int i)
        {
            return Vehicles.Count == 0 ? 0 : ((i % Vehicles.Count) + Vehicles.Count) % Vehicles.Count;
        }

        public JsonObject ToJson()
        {
            var a = new JsonArray();
            foreach (GarageVehicle v in Vehicles)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v.Variant);
                // Saved by asset id and preset (stable across catalogue appends), plus the index as a hint.
                a.Add(new JsonObject().Set("asset", e.AssetId).Set("variant", e.Variant).Set("livery", (long)v.Livery).Set("plate", (long)v.PlateSeed));
            }
            return new JsonObject().Set("version", 1L).Set("selected", (long)Clamp(Selected)).Set("vehicles", a);
        }

        /// <summary>Reads <c>player.garage</c>; unknown or undrivable entries are dropped, and an empty garage becomes the
        /// starter garage.</summary>
        public static Garage FromJson(JsonObject o, uint seed)
        {
            if (o == null) return Starter(seed);
            var g = new Garage();
            JsonArray a = o.GetArray("vehicles");
            if (a != null)
            {
                foreach (JsonValue item in a)
                {
                    JsonObject v = item.AsObject();
                    if (v == null) continue;
                    int index = VehicleCatalog.IndexOf(v.GetString("asset", ""), v.GetString("variant", ""));
                    if (index < 0) continue;
                    long livery = v.GetLong("livery", 0), plate = v.GetLong("plate", 1);
                    var gv = new GarageVehicle(index, (byte)Math.Max(0, Math.Min(255, livery)), plate >= 0 && plate <= uint.MaxValue ? (uint)plate : 1u);
                    g.Add(gv);
                }
            }
            if (g.Vehicles.Count == 0) return Starter(seed);
            g.Selected = g.Clamp((int)o.GetLong("selected", 0));
            return g;
        }
    }

    /// <summary>
    /// A borrowed community-fleet vehicle (W2-O5): any parked vehicle with the green key tag can be borrowed with Hop on;
    /// once the player has left it more than 150 m behind, or 10 minutes after they last rode it, it returns home on its
    /// own (fading out at its next unseen moment and reappearing at its parking spot). Engine-free.
    /// </summary>
    public sealed class FleetLoan
    {
        public const float ReturnAfterS = 600f, ReturnBeyondM = 150f;

        public uint ParkedId;
        public int Variant;
        public byte Livery;
        public double HomeX, HomeZ;
        public float HomeY, HomeYawDeg;

        /// <summary>Seconds since the player last rode it.</summary>
        public float IdleS;

        /// <summary>
        /// Advances the loan. Returns true when the vehicle should go home now: it is not being ridden and either the
        /// player is more than 150 m away from it or it has stood unused for 10 minutes.
        /// </summary>
        public bool Update(float dt, bool aboard, double vehicleX, double vehicleZ, double playerX, double playerZ)
        {
            if (aboard)
            {
                IdleS = 0f;
                return false;
            }
            if (dt > 0f) IdleS += dt;
            double dx = vehicleX - playerX, dz = vehicleZ - playerZ;
            return IdleS >= ReturnAfterS || dx * dx + dz * dz > ReturnBeyondM * ReturnBeyondM;
        }
    }

    /// <summary>
    /// The player's part of the save (W2_DESIGN 10.3): <c>player.appearance</c> (<see cref="CharacterRecipe"/>) and
    /// <c>player.garage</c> (<see cref="Garage"/>), kept in the player section's extra keys so older builds round-trip
    /// them untouched.
    /// </summary>
    public static class PlayerProfile
    {
        public const string AppearanceKey = "appearance", GarageKey = "garage";

        /// <summary>The saved appearance, or a fresh one from <paramref name="newSeed"/> (random skin, never #1).</summary>
        public static CharacterRecipe LoadAppearance(SaveData save, uint newSeed)
        {
            JsonObject o = save != null ? save.Player.Extra.GetObject(AppearanceKey) : null;
            return o == null ? CharacterRecipe.NewPlayer(newSeed) : CharacterRecipe.FromJson(o);
        }

        public static void StoreAppearance(SaveData save, CharacterRecipe r)
        {
            if (save == null || r == null) return;
            save.Player.Extra.Set(AppearanceKey, r.Clone().Validate().ToJson());
        }

        public static Garage LoadGarage(SaveData save, uint seed)
        {
            JsonObject o = save != null ? save.Player.Extra.GetObject(GarageKey) : null;
            return Garage.FromJson(o, seed);
        }

        public static void StoreGarage(SaveData save, Garage g)
        {
            if (save == null || g == null) return;
            save.Player.Extra.Set(GarageKey, g.ToJson());
        }

        /// <summary>True when the save holds an appearance already (else the game shows it as new).</summary>
        public static bool HasAppearance(SaveData save)
        {
            return save != null && save.Player.Extra.GetObject(AppearanceKey) != null;
        }
    }
}
