using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class ApprovedThemePixelReadbackTests
    {
        static readonly Type Theme = Type.GetType("Deviant.UI.Theme.ApprovedTheme, Deviant.UI.Theme", true);
        const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;

        [TearDown] public void ReleaseSources() => Theme.GetMethod("ReleaseWorkingSources").Invoke(null, null);

        [UnityTest]
        public IEnumerator PreparedCropSourcesRemainReleasable()
        {
            Theme.GetMethod("Load", Hidden).Invoke(null, null);
            Theme.GetMethod("ReleaseWorkingSources").Invoke(null, null);
            var records = (IDictionary)Theme.GetField("records", Hidden).GetValue(null);
            string id = null, file = null;
            foreach (DictionaryEntry entry in records)
            {
                if (!((string)entry.Key).StartsWith("ui-assets-v2/", StringComparison.Ordinal)) continue;
                id = (string)entry.Key;
                file = (string)entry.Value.GetType().GetField("file").GetValue(entry.Value);
                break;
            }
            Assert.That(id, Is.Not.Null);
            bool ready = false;
            for (int frame = 0; frame < 600 && !ready; frame++)
            {
                ready = (bool)Theme.GetMethod("Prepare").Invoke(null, new object[] { id });
                if (!ready) yield return null;
            }
            Assert.That(ready, Is.True, "asynchronous preparation completes");
            var textures = (IDictionary)Theme.GetField("textures", Hidden).GetValue(null);
            Assert.That(textures.Contains("source:" + file), Is.False, "crop atlas must not be pinned");
            var art = (Texture2D)Theme.GetMethod("Texture").Invoke(null, new object[] { id });
            Assert.That(art, Is.Not.Null);
            Assert.That(art.isReadable, Is.False);
            Theme.GetMethod("ReleaseWorkingSources").Invoke(null, null);
            Assert.That((int)Theme.GetProperty("WorkingSourceCount").GetValue(null), Is.Zero);
            Assert.That((int)Theme.GetProperty("SourcePixelCopyCount").GetValue(null), Is.Zero);
            Assert.That((bool)Theme.GetMethod("Prepare").Invoke(null, new object[] { id }), Is.True);
            Assert.That((int)Theme.GetProperty("WorkingSourceCount").GetValue(null), Is.Zero, "finished art needs no source reload");
            var args = new object[] { art, null };
            Assert.That((bool)Theme.GetMethod("TryReadPixels").Invoke(null, args), Is.True);
        }

        [Test]
        public void InventoryInkUsesTheDeployedPixelArtwork()
        {
            var sources = Resources.LoadAll<Texture2D>("ApprovedTheme/sources/inventory-ink-pixel");
            Assert.That(sources.Length, Is.EqualTo(39));
            foreach (var source in sources)
            {
                var id = source.name;
                Assert.That(source.width, Is.EqualTo(96), id);
                Assert.That(source.height, Is.EqualTo(96), id);
                var art = (Texture2D)Theme.GetMethod("Ink").Invoke(null, new object[] { id });
                Assert.That(art, Is.Not.Null, id);
                Assert.That(art.filterMode, Is.EqualTo(FilterMode.Point), id);
                Check(art, source.GetPixels(), source.width, source.height, true, id);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReconstructedPixelsMatchReadableRgba32AfterSourceRelease(bool releaseSources)
        {
            Theme.GetMethod("Load", Hidden).Invoke(null, null);
            var inks = (IDictionary)Theme.GetField("inks", Hidden).GetValue(null);
            Assert.That(inks.Count, Is.GreaterThan(0));
            foreach (DictionaryEntry entry in inks)
            {
                var id = (string)entry.Key;
                var color = (string)entry.Value.GetType().GetField("color").GetValue(entry.Value);
                var args = new object[] { id, 0, 0 };
                var inkPixels = (Color[])Theme.GetMethod("InkPixels", Hidden).Invoke(null, args);
                Check((Texture2D)Theme.GetMethod("Ink").Invoke(null, new object[] { id }), inkPixels,
                    (int)args[1], (int)args[2], releaseSources, "ink:" + id);
                var palettePixels = (Color[])Theme.GetMethod("PalettePixels", Hidden).Invoke(null, new object[] { color, id });
                Check((Texture2D)Theme.GetMethod("Palette").Invoke(null, new object[] { color, id }),
                    palettePixels, 220, 155, releaseSources, "palette:" + color + ":" + id);
            }
            Theme.GetMethod("ReleaseWorkingSources").Invoke(null, null);
        }

        static void Check(Texture2D art, Color[] composed, int width, int height, bool releaseSources, string label)
        {
            // This is the old read boundary: composition was uploaded to readable RGBA32 before shelf filtering.
            var reference = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                reference.SetPixels(composed); reference.Apply(false, false);
                var expected = reference.GetPixels();
                Assert.That(art.isReadable, Is.False, label + " keeps no CPU texture copy");
                if (releaseSources)
                {
                    Theme.GetMethod("ReleaseWorkingSources").Invoke(null, null);
                    Assert.That((int)Theme.GetProperty("WorkingSourceCount").GetValue(null), Is.Zero);
                    Assert.That((int)Theme.GetProperty("SourcePixelCopyCount").GetValue(null), Is.Zero);
                }
                var args = new object[] { art, null };
                Assert.That((bool)Theme.GetMethod("TryReadPixels").Invoke(null, args), Is.True, label);
                var actual = (Color[])args[1];
                Assert.That(actual.Length, Is.EqualTo(expected.Length), label);
                for (int i = 0; i < expected.Length; i++)
                    if (actual[i] != expected[i])
                        Assert.Fail(label + " pixel " + i + ": reconstructed " + actual[i].ToString("F6") +
                            " differs from readable RGBA32 " + expected[i].ToString("F6"));
            }
            finally { Object.DestroyImmediate(reference); }
        }
    }
}
