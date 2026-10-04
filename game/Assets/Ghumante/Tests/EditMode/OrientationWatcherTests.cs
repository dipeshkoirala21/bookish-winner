using Ghumante.UI;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    public class OrientationWatcherTests
    {
        [TestCase(2400f, 1080f, LayoutOrientation.Landscape)]
        [TestCase(1080f, 2400f, LayoutOrientation.Portrait)]
        [TestCase(1536f, 2048f, LayoutOrientation.Portrait)]   // iPad upright
        [TestCase(1000f, 1000f, LayoutOrientation.Landscape)]  // square (unfolded foldable): landscape layout
        [TestCase(2208f, 1768f, LayoutOrientation.Landscape)]  // unfolded foldable, held wide
        [TestCase(694f, 2048f, LayoutOrientation.Portrait)]    // iPad split view, narrow pane in a landscape iPad
        public void ClassifiesBySafeAreaAspect(float width, float height, LayoutOrientation expected)
        {
            Assert.AreEqual(expected, OrientationWatcher.Classify(width, height));
        }
    }
}
