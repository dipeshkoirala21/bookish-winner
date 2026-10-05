using System.Collections.Generic;
using System.IO;
using Ghumante.App.Explore;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Search;
using Ghumante.UI.Localization;

namespace Ghumante.Tests.EditMode
{
    // Engine-free helpers for the Explore tests (M1 track D). No UnityEngine here: these tests also run under plain
    // `dotnet test` when compiled with the engine-free sources.

    /// <summary>The sample region's search index, routing graph and a route planner, loaded once.</summary>
    internal static class ExploreSample
    {
        private static SearchEngine _search;
        private static RouteGraph _graph;
        private static RoutePlanner _planner;

        public static SearchEngine Search
        {
            get
            {
                if (_search == null) _search = new SearchEngine(SearchIndexReader.Read(File.ReadAllBytes(SampleRegion.FilePath(".search.ghsi"))));
                return _search;
            }
        }

        public static RouteGraph Graph
        {
            get
            {
                if (_graph == null) _graph = RouteGraphReader.Read(File.ReadAllBytes(SampleRegion.FilePath(".route.ghrg")));
                return _graph;
            }
        }

        public static RoutePlanner Planner
        {
            get
            {
                if (_planner == null) _planner = new RoutePlanner(Graph);
                return _planner;
            }
        }

        /// <summary>The repository root (above shared/sample-regions).</summary>
        public static string RepoRoot
        {
            get { return Path.GetDirectoryName(Path.GetDirectoryName(SampleRegion.SamplesFolder)); }
        }

        /// <summary>A string table (en / ne) as the game ships it.</summary>
        public static Dictionary<string, string> Strings(string locale)
        {
            string path = Path.Combine(RepoRoot, "game", "Assets", "Ghumante", "UI", "Localization", "strings." + locale + ".json");
            return FlatJson.Parse(File.ReadAllText(path));
        }
    }

    /// <summary>Flat ground at a fixed height everywhere (optionally only inside a square), one surface.</summary>
    internal sealed class FlatGround : IGroundQuery
    {
        public float Height = 1300f;
        public SurfaceGroup Surface = SurfaceGroup.Paved;
        public double HalfSize = double.PositiveInfinity;

        public bool TrySample(double x, double z, out GroundSample s)
        {
            s = default(GroundSample);
            if (System.Math.Abs(x) > HalfSize || System.Math.Abs(z) > HalfSize) return false;
            s.Height = Height;
            s.TerrainHeight = Height;
            s.Ny = 1f;
            s.Surface = Surface;
            return true;
        }
    }
}
