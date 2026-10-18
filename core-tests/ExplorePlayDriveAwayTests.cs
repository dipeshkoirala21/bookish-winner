using System;
using System.Collections.Generic;
using System.Text;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.Core.Traffic;
using Ghumante.World.Instancing;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The owner's first Unity playtest (docs/W2_PLAYTEST_1.md #11): "Add Car, Bus, and truck driving ... Allow the user to
    /// grab and start riding the parked vehicles around". On the committed <c>kathmandu_core</c> sample every kind of
    /// parked vehicle the player may drive (cars, taxis, SUVs, pickups, vans and micros, tempos, the green city bus,
    /// minibuses, coaches, lorries, tippers, tankers and tractors) is found where the streamer parks it
    /// (<see cref="TileBuild.Execute"/>, Mid tier), mounted at its driver's door (<see cref="MountPlan"/> against the game's
    /// layered ground with the tile's solids, the dressing and the other parked vehicles registered as
    /// <c>WorldStreamer</c> and <c>ExploreSession</c> do), and driven off with its own handling
    /// (<see cref="VehicleCatalogEntry.Spec"/>, as <c>PlayerVehicle</c> builds it) onto the nearest lane its class may use
    /// (else the nearest car lane: the player drives where the body fits) and 20 m along it, by a plain rider that steers
    /// at the lane and backs up when it meets a wall. Each lands on the
    /// camera rig of its class (<see cref="VehicleRoles.RigOf"/>: bus, lorry, tractor, van, car).
    /// </summary>
    public class ExplorePlayDriveAwayTests
    {
        /// <summary>One parked vehicle driven away.</summary>
        private struct Outcome
        {
            public int Variant;
            public bool Mounted, Away;
            public double Along, Seconds;
            public int Reverses, Hits;
            public string Where, Why;
        }

        /// <summary>Most vehicles of one catalogue entry tried (spread over the sample).</summary>
        private const int PerVariant = 4;

        private static bool Drivable(int variant)
        {
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            return e.PlayerDrivable && !VehicleRoles.IsTwoWheeler(e.Shape);
        }

        /// <summary>The parked vehicles of a tile as the cheap placement pass sees them (to choose the tiles to build).</summary>
        private static List<ParkedVehicle> Quick(TileId id)
        {
            var parked = new List<ParkedVehicle>();
            TileData t = StreamingSampleRegion.Tile(id);
            if (t == null) return parked;
            MeshingSettings s = StreamingScene.Settings(1, 10, false);
            TileHeightSampler h = TileHeightSampler.ForArea(t, id, StreamingConfig.ForTier(1).TerrainStep(id, id, t.HeightsN));
            ParkedPlacement.Place(t, h, parked, s.Roads);
            return parked;
        }

        /// <summary>The fewest tiles (greedy) that hold every drivable four-wheeler the sample parks, most kinds first.</summary>
        private static List<TileId> TilesWithEveryKind(out HashSet<int> kinds)
        {
            var per = new List<KeyValuePair<TileId, HashSet<int>>>();
            kinds = new HashSet<int>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                var set = new HashSet<int>();
                foreach (ParkedVehicle p in Quick(id))
                    if (Drivable(p.Variant))
                        set.Add(p.Variant);
                if (set.Count == 0) continue;
                kinds.UnionWith(set);
                per.Add(new KeyValuePair<TileId, HashSet<int>>(id, set));
            }
            var chosen = new List<TileId>();
            var covered = new HashSet<int>();
            while (covered.Count < kinds.Count)
            {
                int best = -1, gain = 0;
                for (int i = 0; i < per.Count; i++)
                {
                    int g = 0;
                    foreach (int v in per[i].Value)
                        if (!covered.Contains(v))
                            g++;
                    if (g > gain)
                    {
                        gain = g;
                        best = i;
                    }
                }
                if (best < 0) break;
                chosen.Add(per[best].Key);
                covered.UnionWith(per[best].Value);
                per.RemoveAt(best);
            }
            return chosen;
        }

        [Test]
        public void EveryKindOfParkedVehicleCanBeMountedAndDrivenOntoTheRoad()
        {
            HashSet<int> kinds;
            List<TileId> tiles = TilesWithEveryKind(out kinds);
            Assert.That(tiles.Count, Is.GreaterThan(0), "drivable four-wheelers stand on the sample");
            LaneGraph lanes = TrafficData.Graph;
            MeshingSettings s = StreamingScene.Settings(1, 10);
            var outcomes = new List<Outcome>();
            var tried = new Dictionary<int, int>();
            foreach (TileId id in tiles)
            {
                TileGroundQuery ground = StreamingScene.Ground(s);
                TileBuild b = StreamingScene.Build(id, s, ground);
                ground.Add(id, b.Sampler, b.Roads);
                if (b.Colliders != null) ground.Register(id.Key | TileGroundQuery.StaticSetKeyBit, b.Colliders);
                List<ParkedVehicle> parked = b.Instances.Parked;
                for (int i = 0; i < parked.Count; i++)
                {
                    ParkedVehicle p = parked[i];
                    if (!Drivable(p.Variant)) continue;
                    int n;
                    tried.TryGetValue(p.Variant, out n);
                    if (n >= PerVariant) continue;
                    tried[p.Variant] = n + 1;
                    Assert.That(p.CommunityFleet, Is.True, VehicleCatalog.At(p.Variant).AssetId + " carries the borrow tag");
                    // The tile's own set as the streamer registers it (heroes) plus the other borrowable parked vehicles, solid
                    // round the player as ExploreSession registers them; the one borrowed is not.
                    var solids = new StructureColliders();
                    if (b.HeroColliders != null) solids.AddAll(b.HeroColliders);
                    for (int j = 0; j < parked.Count; j++)
                        if (j != i && parked[j].CommunityFleet)
                            solids.AddVehicle(parked[j].X, parked[j].Z, parked[j].Y, parked[j].YawDeg, parked[j].Variant);
                    if (solids.Count > 0) ground.Register(id.Key, solids);
                    else ground.Unregister(id.Key);
                    outcomes.Add(DriveAway(ground, lanes, id, p));
                }
            }

            var sb = new StringBuilder();
            var byKind = new Dictionary<int, int[]>();
            foreach (Outcome o in outcomes)
            {
                int[] c;
                if (!byKind.TryGetValue(o.Variant, out c)) byKind[o.Variant] = c = new int[3];
                c[0]++;
                if (o.Mounted) c[1]++;
                if (o.Away) c[2]++;
                sb.AppendLine(string.Format("{0,-28} {1} mount {2} away {3} along {4:0} m in {5:0} s, {6} reverses, {7} hits{8}", VehicleCatalog.At(o.Variant).AssetId +
                                            "/" + VehicleCatalog.At(o.Variant).Variant, o.Where, o.Mounted ? "yes" : "NO", o.Away ? "yes" : "NO", o.Along, o.Seconds,
                                            o.Reverses, o.Hits, o.Why != null ? " (" + o.Why + ")" : ""));
            }
            TestContext.WriteLine(tiles.Count + " tiles: " + string.Join(" ", tiles));
            TestContext.WriteLine(sb.ToString());
            int all = 0, away = 0, mounted = 0;
            foreach (KeyValuePair<int, int[]> kv in byKind)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(kv.Key);
                all += kv.Value[0];
                mounted += kv.Value[1];
                away += kv.Value[2];
                Assert.That(kv.Value[2], Is.GreaterThan(0), e.AssetId + "/" + e.Variant + ": at least one parked one drives off onto the road");
            }
            foreach (BodyShape shape in new[] { BodyShape.Bus, BodyShape.Truck, BodyShape.Tractor, BodyShape.Van, BodyShape.Hatchback })
            {
                bool seen = false;
                foreach (int v in byKind.Keys)
                    if (VehicleCatalog.At(v).Shape == shape)
                        seen = true;
                Assert.That(seen, shape + " parked and tried");
            }
            Assert.That(mounted, Is.EqualTo(all), "every one can be mounted at its driver's door (or the other side)");
            Assert.That(away, Is.GreaterThanOrEqualTo((int)Math.Ceiling(0.9 * all)), "nine in ten drive straight off");
        }

        [Test]
        public void EachDrivableClassHasItsOwnCameraRig()
        {
            // The camera follows the class of what is driven (ExplorerController.Rig: VehicleRoles.RigOf of the vehicle).
            var expect = new Dictionary<BodyShape, RigClass>
            {
                { BodyShape.Bus, RigClass.Bus }, { BodyShape.Truck, RigClass.Truck }, { BodyShape.Tipper, RigClass.Truck }, { BodyShape.Tanker, RigClass.Truck },
                { BodyShape.Tractor, RigClass.Tractor }, { BodyShape.Van, RigClass.Van }, { BodyShape.Tempo, RigClass.Van }, { BodyShape.Suv, RigClass.Van },
                { BodyShape.Hatchback, RigClass.Car }, { BodyShape.Pickup, RigClass.Car },
            };
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                if (!Drivable(v)) continue;
                RigClass want;
                Assert.That(expect.TryGetValue(e.Shape, out want), e.AssetId + " shape " + e.Shape + " has a rig");
                Assert.That(VehicleRoles.RigOf(e), Is.EqualTo(want), e.AssetId);
                RigParams r = CameraRigTable.For(want, false);
                Assert.That(CameraViews.Count(want), Is.GreaterThan(1), e.AssetId + " offers more than one camera view");
                if (want == RigClass.Bus || want == RigClass.Truck)
                    Assert.That(r.DistanceM, Is.GreaterThan(CameraRigTable.For(RigClass.Car, false).DistanceM), "a heavy rig sits farther back than a car's");
            }
        }

        /// <summary>Mounts the parked vehicle at its driver's door and drives it off onto the nearest lane its class may use.</summary>
        private static Outcome DriveAway(TileGroundQuery ground, LaneGraph lanes, TileId id, ParkedVehicle p)
        {
            VehicleCatalogEntry e = VehicleCatalog.At(p.Variant);
            double x = id.X0 + p.X, z = id.Z0 + p.Z;
            float yaw = (float)(p.YawDeg * Math.PI / 180.0);
            var o = new Outcome { Variant = p.Variant, Where = string.Format("({0:0},{1:0})", x, z) };
            var frame = new VehicleFrame(x, z, p.Y, yaw);
            SeatSocket[] seats = VehicleRoles.Seats(p.Variant);
            SeatSocket seat = seats[0];
            foreach (SeatSocket st in seats)
                if (st.Kind == SeatKind.Driver)
                {
                    seat = st;
                    break;
                }
            double ex, ez;
            frame.ToWorld(seat.EntryX, seat.EntryZ, out ex, out ez);
            MountPlan plan;
            o.Mounted = MountPlan.TryPlan(e.TrafficClass, seat, frame, ex, ez, ground, out plan) ||
                        // Standing a step outside the door (a wall right beside it sends the player round, MountPlan.Alternate).
                        MountPlan.TryPlan(e.TrafficClass, seat, frame, ex + Math.Cos(yaw) * Math.Sign(seat.EntryX), ez - Math.Sin(yaw) * Math.Sign(seat.EntryX), ground, out plan);

            if (!o.Mounted)
            {
                // Why: each door's ground and what blocks it (diagnostics for the report).
                var why = new StringBuilder();
                foreach (float lx in new[] { seat.EntryX, seat.HasAlternate ? seat.AltEntryX : seat.EntryX })
                    foreach (float outw in new[] { 0f, 0.5f })
                    {
                        double wx, wz;
                        frame.ToWorld(lx + Math.Sign(lx) * outw, seat.EntryZ, out wx, out wz);
                        GroundSample g;
                        bool ok = ground.TrySample(wx, wz, out g);
                        why.Append(string.Format("[{0:0.0},{1:0.0}] ground {2} dy {3:0.00} blocked {4}; ", lx, outw, ok, ok ? g.Height - p.Y : 0f,
                                                 ok && ground.IsBlocked(wx, wz, g.Height)));
                    }
                o.Why = why.ToString();
            }
            // The road: the nearest lane the class may use, from the nose, and on along its successors for 40 m.
            double fx = Math.Sin(yaw), fz = Math.Cos(yaw), front = e.LengthM - e.RearOverhangM;
            LaneId lane;
            float along;
            // The player drives where the vehicle fits, not where the traffic of its class is routed: a lane of its own
            // class, else a car lane (a bus park's own lanes, a farm lane for a tractor).
            if (!lanes.TryNearestLane(x + fx * front, z + fz * front, e.TrafficClass, 60.0, out lane, out along) &&
                !lanes.TryNearestLane(x + fx * front, z + fz * front, VehicleClass.Car, 60.0, out lane, out along))
            {
                o.Why = "no lane within 60 m";
                return o;
            }
            var px = new List<double>();
            var pz = new List<double>();
            int l = lane.Value;
            float from = along;
            for (int guard = 0; guard < 12 && l >= 0; guard++)
            {
                LaneInfo li = lanes.Info(new LaneId(l));
                for (int k = 0; k < li.PointCount; k++)
                    if (li.S[k] >= from)
                    {
                        px.Add(li.X[k]);
                        pz.Add(li.Z[k]);
                    }
                from = 0f;
                if (Length(px, pz) >= 40.0) break;
                int[] next = lanes.Lanes[l].Next;
                l = next.Length > 0 ? next[0] : -1;
            }
            if (px.Count < 2 || Length(px, pz) < 22.0)
            {
                o.Why = "lane too short";
                return o;
            }

            // Drive: a pure-pursuit rider at walking pace for heavy bodies, steering at a point 6 m on along the lane from
            // its own projection; when it makes no headway for 2 s it backs up 2 s with the wheel turned the other way.
            var v = new ArcadeVehicle(e.Spec());
            v.Teleport(x, z, yaw, ground);
            const float dt = 1f / 30f;
            double best = 0, bestAt = 0, t = 0, lastX = x, lastZ = z, movedAt = 0;
            double reverseUntil = -1;
            float reverseSteer = 0f;
            var ps = new List<double> { 0 };
            for (int k = 1; k < px.Count; k++) ps.Add(ps[k - 1] + Math.Sqrt((px[k] - px[k - 1]) * (px[k] - px[k - 1]) + (pz[k] - pz[k - 1]) * (pz[k] - pz[k - 1])));
            while (t < 60.0)
            {
                double lateral;
                double s = Project(px, pz, ps, v.X, v.Z, out lateral);
                if (lateral < 3.0 && s > best + 0.3)
                {
                    best = s;
                    bestAt = t;
                }
                if (best >= 20.0)
                {
                    o.Away = true;
                    break;
                }
                double mx = v.X - lastX, mz = v.Z - lastZ;
                if (mx * mx + mz * mz > 0.25)
                {
                    lastX = v.X;
                    lastZ = v.Z;
                    movedAt = t;
                }
                DriveInput input;
                if (t < reverseUntil) input = new DriveInput(-0.7f, 0f, reverseSteer);
                else
                {
                    double tx, tz;
                    At(px, pz, ps, Math.Min(ps[ps.Count - 1], (lateral < 3.0 ? best : s) + 6.0), out tx, out tz);
                    double err = Math.Atan2(tx - v.X, tz - v.Z) - v.HeadingRad;
                    while (err > Math.PI) err -= 2 * Math.PI;
                    while (err < -Math.PI) err += 2 * Math.PI;
                    float steer = (float)Math.Max(-1, Math.Min(1, err * 2.0));
                    if (t - movedAt > 2.0 && t - Math.Max(bestAt, reverseUntil) > 2.0)
                    {
                        o.Reverses++;
                        reverseUntil = t + 2.0;
                        reverseSteer = -steer;
                        movedAt = t;
                        input = new DriveInput(-0.7f, 0f, reverseSteer);
                    }
                    else
                    {
                        float target = Math.Abs(err) > 0.6 ? 2.5f : 5f;
                        input = new DriveInput((float)Math.Max(0, Math.Min(0.7, (target - v.SpeedMps) * 0.4)), v.SpeedMps > target + 1f ? 0.5f : 0f, steer);
                    }
                }
                if ((v.Step(input, dt, ground, 0f) & StepEvents.HitWall) != 0) o.Hits++;
                t += dt;
            }
            o.Along = best;
            o.Seconds = t;
            if (!o.Away && o.Why == null) o.Why = string.Format("stopped at ({0:0.0},{1:0.0})", v.X, v.Z);
            return o;
        }

        private static double Length(List<double> px, List<double> pz)
        {
            double len = 0;
            for (int k = 1; k < px.Count; k++) len += Math.Sqrt((px[k] - px[k - 1]) * (px[k] - px[k - 1]) + (pz[k] - pz[k - 1]) * (pz[k] - pz[k - 1]));
            return len;
        }

        private static double Project(List<double> px, List<double> pz, List<double> ps, double x, double z, out double lateral)
        {
            double best = double.MaxValue, at = 0;
            for (int k = 0; k + 1 < px.Count; k++)
            {
                double ax = px[k], az = pz[k], dx = px[k + 1] - ax, dz = pz[k + 1] - az, l2 = dx * dx + dz * dz;
                double u = l2 > 1e-9 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / l2)) : 0;
                double qx = ax + dx * u - x, qz = az + dz * u - z, d = qx * qx + qz * qz;
                if (d < best)
                {
                    best = d;
                    at = ps[k] + u * (ps[k + 1] - ps[k]);
                }
            }
            lateral = Math.Sqrt(best);
            return at;
        }

        private static void At(List<double> px, List<double> pz, List<double> ps, double s, out double x, out double z)
        {
            for (int k = 0; k + 1 < px.Count; k++)
            {
                if (s > ps[k + 1] && k + 2 < px.Count) continue;
                double len = ps[k + 1] - ps[k], f = len > 1e-9 ? Math.Max(0, Math.Min(1, (s - ps[k]) / len)) : 0;
                x = px[k] + (px[k + 1] - px[k]) * f;
                z = pz[k] + (pz[k + 1] - pz[k]) * f;
                return;
            }
            x = px[px.Count - 1];
            z = pz[pz.Count - 1];
        }
    }
}
