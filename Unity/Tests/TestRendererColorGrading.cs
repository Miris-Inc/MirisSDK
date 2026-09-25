// Copyright © 2026 Miris, Inc. All rights reserved.

// Standard library
using System.Collections;

// Unity engine
using UnityEngine;
using UnityEngine.TestTools;

// Unity packages
using NUnit.Framework;

using Miris.Runtime;

namespace Miris.Tests
{
    // Covers MirisColorGradeState and the LUT maths in MirisColorGradeState.hlsl.
    //
    // Deliberately does NOT derive from RendererTestBase and never streams an asset: everything
    // here is either pure C# or a blit over a synthetic texture, so it runs offline and
    // deterministically. Named TestRenderer* so the `gtf` test category picks it up - that filter
    // is a plain name match on `TestRenderer`, not an NUnit [Category].
    public class TestRendererColorGrading
    {
        // Cube size of the LUTs built here. 32 is the size the docs recommend, giving a 1024x32
        // strip.
        private const int c_lutSize = 32;

        [TearDown]
        public void Teardown()
        {
            // MirisColorGradeState is static, so a LUT left set would leak into the next test - and
            // into the graphics tests, whose reference images assume grading is off. Reset rather
            // than Set, so an owner left behind cannot refuse the next test's component.
            MirisColorGradeState.Reset();
        }

        // ---------------------------------------------------------
        // LUT parameter maths
        // ---------------------------------------------------------

        [Test]
        public void LutParamsMatchStripLayout([Values(8, 16, 32, 64)] int size)
        {
            Texture2D lut = CreateIdentityLut(size);

            Vector4 lutParams = MirisColorGradeState.CalculateLutParams(lut);

            Assert.AreEqual(1.0f / (size * size), lutParams.x, 1e-6f, "x should be one texel of width");
            Assert.AreEqual(1.0f / size, lutParams.y, 1e-6f, "y should be one texel of height");
            Assert.AreEqual(size - 1.0f, lutParams.z, 1e-6f, "z should scale a channel onto the cell range");

            Object.DestroyImmediate(lut);
        }

        [Test]
        public void LutParamsAreZeroWithoutALut()
        {
            Assert.AreEqual(Vector4.zero, MirisColorGradeState.CalculateLutParams(null));
        }

        // ---------------------------------------------------------
        // Keyword and material state
        // ---------------------------------------------------------

        [Test]
        public void GradingIsInactiveUntilALutIsSet()
        {
            MirisColorGradeState.Set(null, 1.0f);

            Assert.IsFalse(MirisColorGradeState.IsActive, "no LUT should mean no grading");
        }

        // The neutral-default guarantee: with no LUT the keyword must be off, which is what makes
        // both composites compile down to their pre-grading form and keeps the existing graphics
        // reference images valid.
        [Test]
        public void CompositeMaterialHasNoGradingKeywordWithoutALut()
        {
            Material material = CreateGradeMaterial();
            material.EnableKeyword("MIRIS_COLOR_GRADING_LUT");

            MirisColorGradeState.Set(null, 1.0f);
            MirisColorGradeState.ApplyTo(material);

            Assert.IsFalse(material.IsKeywordEnabled("MIRIS_COLOR_GRADING_LUT"),
                           "clearing the LUT must switch the shader variant back off");

            Object.DestroyImmediate(material);
        }

        [Test]
        public void ApplyToBindsTheLutAndItsParameters()
        {
            Material material = CreateGradeMaterial();
            Texture2D lut = CreateIdentityLut(c_lutSize);

            MirisColorGradeState.Set(lut, 0.25f);
            MirisColorGradeState.ApplyTo(material);

            Assert.IsTrue(material.IsKeywordEnabled("MIRIS_COLOR_GRADING_LUT"));
            Assert.AreSame(lut, material.GetTexture("_MirisLutTex"));
            Assert.AreEqual(0.25f, material.GetFloat("_MirisLutStrength"), 1e-6f);
            Vector4 expectedParams = MirisColorGradeState.CalculateLutParams(lut);
            Vector4 boundParams = material.GetVector("_MirisLutParams");
            Assert.AreEqual(expectedParams.x, boundParams.x, 1e-6f);
            Assert.AreEqual(expectedParams.y, boundParams.y, 1e-6f);
            Assert.AreEqual(expectedParams.z, boundParams.z, 1e-6f);

            Object.DestroyImmediate(lut);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void StrengthIsClamped()
        {
            Texture2D lut = CreateIdentityLut(c_lutSize);

            MirisColorGradeState.Set(lut, 4.0f);
            Assert.AreEqual(1.0f, MirisColorGradeState.Strength, 1e-6f);

            MirisColorGradeState.Set(lut, -1.0f);
            Assert.AreEqual(0.0f, MirisColorGradeState.Strength, 1e-6f);

            Object.DestroyImmediate(lut);
        }

        // ---------------------------------------------------------
        // Import-setting validation
        // ---------------------------------------------------------

        [Test]
        public void ValidateWarnsAboutANonStripLayout()
        {
            // Square rather than size*size wide, so the slices would be sampled at wrong offsets.
            // Filter and wrap set explicitly so the layout warning is the only one raised.
            Texture2D lut = new Texture2D(c_lutSize, c_lutSize, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not a 2D strip"));
            MirisColorGradeState.Validate(lut);

            Object.DestroyImmediate(lut);
        }

        [Test]
        public void ValidateWarnsAboutAnSRGBLut()
        {
            // linear:false is what makes Unity view the texture as sRGB, which would decode the
            // LUT's stored output values as if they were a colour to be linearised.
            Texture2D lut = new Texture2D(c_lutSize * c_lutSize, c_lutSize, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("sRGB"));
            MirisColorGradeState.Validate(lut);

            Object.DestroyImmediate(lut);
        }

        [Test]
        public void ValidateWarnsAboutFilterAndWrapModes()
        {
            Texture2D lut = CreateIdentityLut(c_lutSize);
            lut.filterMode = FilterMode.Point;
            lut.wrapMode = TextureWrapMode.Repeat;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Bilinear"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Clamp"));
            MirisColorGradeState.Validate(lut);

            Object.DestroyImmediate(lut);
        }

        [Test]
        public void ValidateAcceptsACorrectlyImportedLut()
        {
            Texture2D lut = CreateIdentityLut(c_lutSize);

            MirisColorGradeState.Validate(lut);

            // Explicit: an unexpected warning does not fail a test on its own, only an error does.
            LogAssert.NoUnexpectedReceived();

            Object.DestroyImmediate(lut);
        }

        // ---------------------------------------------------------
        // Shader round trip
        // ---------------------------------------------------------

        // The regression that matters: an identity LUT must leave the image alone. This exercises
        // the real shader maths end to end - the linear->sRGB conversion, the unpremultiply, the
        // strip sample and slice interpolation, the re-premultiply, and the sRGB->linear
        // conversion back - and fails if any of them drifts.
        //
        // Driven through the real MirisColorGrade shader, which is the single place grading now
        // happens - so this covers every path at once rather than one renderer's composite.
        [UnityTest]
        public IEnumerator IdentityLutLeavesTheImageUnchanged()
        {
            const int size = 64;

            Material material = CreateGradeMaterial();
            Texture2D source = CreatePremultipliedSource(size);
            Texture2D lut = CreateIdentityLut(c_lutSize);

            MirisColorGradeState.Set(null, 1.0f);
            MirisColorGradeState.ApplyTo(material);
            Texture2D ungraded = RenderToTexture(material, source, size);

            yield return null;

            MirisColorGradeState.Set(lut, 1.0f);
            MirisColorGradeState.ApplyTo(material);
            Texture2D graded = RenderToTexture(material, source, size);

            yield return null;

            AssertTexturesMatch(ungraded, graded, tolerance: 2);

            Object.DestroyImmediate(ungraded);
            Object.DestroyImmediate(graded);
            Object.DestroyImmediate(lut);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(material);
        }

        // Strength 0 must also be the identity, and by a different route - the LUT is bound and the
        // keyword is on, so this is the shader's lerp rather than the compiled-out path.
        [UnityTest]
        public IEnumerator ZeroStrengthLeavesTheImageUnchanged()
        {
            const int size = 64;

            Material material = CreateGradeMaterial();
            Texture2D source = CreatePremultipliedSource(size);
            // A LUT that inverts, so anything other than a true zero-strength blend shows up loudly.
            Texture2D lut = CreateInvertingLut(c_lutSize);

            MirisColorGradeState.Set(null, 1.0f);
            MirisColorGradeState.ApplyTo(material);
            Texture2D ungraded = RenderToTexture(material, source, size);

            yield return null;

            MirisColorGradeState.Set(lut, 0.0f);
            MirisColorGradeState.ApplyTo(material);
            Texture2D graded = RenderToTexture(material, source, size);

            yield return null;

            AssertTexturesMatch(ungraded, graded, tolerance: 2);

            Object.DestroyImmediate(ungraded);
            Object.DestroyImmediate(graded);
            Object.DestroyImmediate(lut);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(material);
        }

        // Guards the test itself: if an inverting LUT at full strength did NOT change the image,
        // every assertion above would be passing vacuously.
        [UnityTest]
        public IEnumerator InvertingLutChangesTheImage()
        {
            const int size = 64;

            Material material = CreateGradeMaterial();
            Texture2D source = CreatePremultipliedSource(size);
            Texture2D lut = CreateInvertingLut(c_lutSize);

            MirisColorGradeState.Set(null, 1.0f);
            MirisColorGradeState.ApplyTo(material);
            Texture2D ungraded = RenderToTexture(material, source, size);

            yield return null;

            MirisColorGradeState.Set(lut, 1.0f);
            MirisColorGradeState.ApplyTo(material);
            Texture2D graded = RenderToTexture(material, source, size);

            yield return null;

            Assert.IsFalse(TexturesMatch(ungraded, graded, tolerance: 2),
                           "an inverting LUT at full strength must visibly change the image");

            Object.DestroyImmediate(ungraded);
            Object.DestroyImmediate(graded);
            Object.DestroyImmediate(lut);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(material);
        }

        // The grade must leave alpha exactly as it found it. On visionOS the system compositor
        // blends passthrough underneath the app using the rendered image's alpha as its mask, so a
        // grade that altered alpha would not tint passthrough - it would turn it opaque.
        [UnityTest]
        public IEnumerator GradeNeverWritesAlpha()
        {
            const int size = 64;

            Material material = CreateGradeMaterial();
            Texture2D source = CreatePremultipliedSource(size);
            // Fully opaque in the source, so if alpha were written at all it would land on 255 and
            // be unmistakable against the target's cleared value.
            Texture2D lut = CreateInvertingLut(c_lutSize);

            MirisColorGradeState.Set(lut, 1.0f);
            MirisColorGradeState.ApplyTo(material);
            Texture2D graded = RenderToTexture(material, source, size);

            yield return null;

            Color32[] sourcePixels = source.GetPixels32();
            Color32[] gradedPixels = graded.GetPixels32();
            for (int i = 0; i < gradedPixels.Length; ++i)
            {
                if (Mathf.Abs(gradedPixels[i].a - sourcePixels[i].a) > 2)
                {
                    Assert.Fail($"pixel {i % size},{i / size} had alpha {gradedPixels[i].a}, expected "
                                + $"the source's {sourcePixels[i].a} - the grade altered alpha, which "
                                + "would turn visionOS passthrough opaque");
                }
            }

            Object.DestroyImmediate(graded);
            Object.DestroyImmediate(lut);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(material);
        }

        // Orientation. Every other image test here compares one render against another, so a flip
        // is common to both and invisible to them - which is exactly how an inherited
        // _ProjectionParams.x flip once turned the whole screen upside down without failing a
        // single test. This one checks the output against the SOURCE's own layout instead.
        //
        // CreatePremultipliedSource ramps red along +x and green along +y, and GetPixels32 returns
        // rows bottom-up, so a correctly oriented grade preserves both gradients.
        [UnityTest]
        public IEnumerator GradePreservesOrientation()
        {
            const int size = 64;

            Material material = CreateGradeMaterial();
            Texture2D source = CreatePremultipliedSource(size);
            Texture2D lut = CreateIdentityLut(c_lutSize);

            MirisColorGradeState.Set(lut, 1.0f);
            MirisColorGradeState.ApplyTo(material);
            Texture2D graded = RenderToTexture(material, source, size);

            yield return null;

            Color32[] pixels = graded.GetPixels32();

            float bottomGreen = MeanChannel(pixels, size, 0, size / 4, channel: 1);
            float topGreen = MeanChannel(pixels, size, size - size / 4, size, channel: 1);
            Assert.Greater(topGreen, bottomGreen,
                           "green ramps upward in the source, so the graded image is vertically "
                           + "flipped - check the UV flip in MirisColorGrade.hlsl");

            float leftRed = MeanColumnChannel(pixels, size, 0, size / 4, channel: 0);
            float rightRed = MeanColumnChannel(pixels, size, size - size / 4, size, channel: 0);
            Assert.Greater(rightRed, leftRed,
                           "red ramps rightward in the source, so the graded image is horizontally "
                           + "flipped");

            Object.DestroyImmediate(graded);
            Object.DestroyImmediate(lut);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(material);
        }

        // End to end through a real camera, which is the only way to exercise the copy FROM the
        // camera target. Every other image test here drives the material directly, so a flip
        // introduced by that copy is invisible to them - which is exactly how an upside-down screen
        // survived a fully green suite twice.
        //
        // An identity LUT must leave the camera's image untouched. If the pass flips, the graded
        // render is the ungraded one upside down and this fails.
        [UnityTest]
        public IEnumerator GradeThroughCameraPreservesOrientation()
        {
            const int size = 128;

            RenderTexture target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32,
                                                     RenderTextureReadWrite.Linear);
            GameObject cameraObject = new GameObject("GradeTestCamera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 1.0f;
            camera.transform.position = new Vector3(0.0f, 0.0f, -5.0f);
            MirisColorGrade grade = cameraObject.AddComponent<MirisColorGrade>();

            // A bright quad in the UPPER half only, so the image is strongly asymmetric top to
            // bottom and a vertical flip cannot hide.
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.position = new Vector3(0.0f, 0.5f, 0.0f);
            quad.transform.localScale = new Vector3(2.0f, 0.8f, 1.0f);
            Material unlit = new Material(Shader.Find("Unlit/Color"));
            unlit.color = Color.white;
            quad.GetComponent<MeshRenderer>().sharedMaterial = unlit;

            Texture2D lut = CreateIdentityLut(c_lutSize);

            grade.SetGrade(null, 1.0f);
            yield return null;
            Texture2D ungraded = RenderCamera(camera, target, size);

            grade.SetGrade(lut, 1.0f);
            yield return null;
            Texture2D graded = RenderCamera(camera, target, size);

            // Guards the test: if the scene rendered flat, an orientation check proves nothing.
            Color32[] ungradedPixels = ungraded.GetPixels32();
            float bottom = MeanChannel(ungradedPixels, size, 0, size / 4, channel: 1);
            float top = MeanChannel(ungradedPixels, size, size - size / 4, size, channel: 1);
            Assert.Greater(Mathf.Abs(top - bottom), 32.0f,
                           "the test scene is not asymmetric enough to detect a flip");

            AssertTexturesMatch(ungraded, graded, tolerance: 3);

            Object.DestroyImmediate(ungraded);
            Object.DestroyImmediate(graded);
            Object.DestroyImmediate(lut);
            Object.DestroyImmediate(unlit);
            Object.DestroyImmediate(quad);
            Object.DestroyImmediate(cameraObject);
            target.Release();
            Object.DestroyImmediate(target);
        }

        // The bug this guards: MirisApplyColorGrade unpremultiplied by dividing by alpha, which is
        // correct for a splat composite but not for a full-screen camera image, where alpha carries
        // no coverage meaning of its own. A transparent SolidColor background (alpha 0) collapsed to
        // black as soon as any LUT was assigned, even the identity one.
        [UnityTest]
        public IEnumerator GradeIgnoresTransparentBackgroundAlpha()
        {
            const int size = 64;

            RenderTexture target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32,
                                                     RenderTextureReadWrite.Linear);
            GameObject cameraObject = new GameObject("GradeAlphaTestCamera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.3f, 0.5f, 0.0f);
            camera.orthographic = true;
            camera.orthographicSize = 1.0f;
            camera.transform.position = new Vector3(0.0f, 0.0f, -5.0f);
            MirisColorGrade grade = cameraObject.AddComponent<MirisColorGrade>();

            Texture2D lut = CreateIdentityLut(c_lutSize);

            grade.SetGrade(null, 1.0f);
            yield return null;
            Texture2D ungraded = RenderCamera(camera, target, size);

            grade.SetGrade(lut, 1.0f);
            yield return null;
            Texture2D graded = RenderCamera(camera, target, size);

            AssertTexturesMatch(ungraded, graded, tolerance: 2);

            Object.DestroyImmediate(ungraded);
            Object.DestroyImmediate(graded);
            Object.DestroyImmediate(lut);
            Object.DestroyImmediate(cameraObject);
            target.Release();
            Object.DestroyImmediate(target);
        }

        // The bug this guards: the grade is one process-wide state behind a per-camera component,
        // so a second enabled component used to overwrite the first's LUT, and disabling either one
        // cleared the grade for both.
        [Test]
        public void ASecondComponentCannotTakeOrClearTheGrade()
        {
            Texture2D ownerLut = CreateIdentityLut(c_lutSize);
            Texture2D intruderLut = CreateIdentityLut(c_lutSize);

            GameObject ownerObject = new GameObject("GradeOwner", typeof(Camera));
            MirisColorGrade owner = ownerObject.AddComponent<MirisColorGrade>();
            owner.SetGrade(ownerLut, 1.0f);

            GameObject intruderObject = new GameObject("GradeIntruder", typeof(Camera));
            MirisColorGrade intruder = intruderObject.AddComponent<MirisColorGrade>();
            intruder.SetGrade(intruderLut, 1.0f);

            Assert.AreSame(ownerLut, MirisColorGradeState.Lut,
                           "the second component took the grade over");

            intruder.enabled = false;
            Assert.AreSame(ownerLut, MirisColorGradeState.Lut,
                           "disabling the second component cleared the owner's grade");

            owner.enabled = false;
            Assert.IsFalse(MirisColorGradeState.IsActive,
                           "disabling the owner should clear the grade");

            Object.DestroyImmediate(intruderObject);
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(intruderLut);
            Object.DestroyImmediate(ownerLut);
        }

        // ---------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------

        private static Texture2D RenderCamera(Camera camera, RenderTexture target, int size)
        {
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            return result;
        }

        // Mean of one channel over a band of rows. GetPixels32 is bottom-up, so row 0 is the
        // bottom of the image.
        private static float MeanChannel(Color32[] pixels, int size, int firstRow, int lastRow, int channel)
        {
            double total = 0.0;
            int count = 0;
            for (int y = firstRow; y < lastRow; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    Color32 pixel = pixels[y * size + x];
                    total += channel == 0 ? pixel.r : (channel == 1 ? pixel.g : pixel.b);
                    ++count;
                }
            }
            return (float)(total / count);
        }

        // Mean of one channel over a band of columns.
        private static float MeanColumnChannel(Color32[] pixels, int size, int firstColumn, int lastColumn, int channel)
        {
            double total = 0.0;
            int count = 0;
            for (int y = 0; y < size; ++y)
            {
                for (int x = firstColumn; x < lastColumn; ++x)
                {
                    Color32 pixel = pixels[y * size + x];
                    total += channel == 0 ? pixel.r : (channel == 1 ? pixel.g : pixel.b);
                    ++count;
                }
            }
            return (float)(total / count);
        }

        private static Material CreateGradeMaterial()
        {
            Shader shader = Resources.Load<Shader>(MirisColorGradeState.c_shaderResourcePath);
            Assert.IsNotNull(shader, $"{MirisColorGradeState.c_shaderResourcePath} is missing from the package Resources");
            return new Material(shader);
        }

        // A horizontal strip LUT that maps every colour to itself: `size` square slices, red across
        // x within a slice, green up y, blue selected by the slice. linear:true because a LUT
        // stores output values rather than a colour to be decoded.
        private static Texture2D CreateIdentityLut(int size)
        {
            Texture2D lut = new Texture2D(size * size, size, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "IdentityLut"
            };

            Color[] pixels = new Color[size * size * size];
            float scale = 1.0f / (size - 1.0f);

            for (int y = 0; y < size; ++y)
            {
                for (int slice = 0; slice < size; ++slice)
                {
                    for (int x = 0; x < size; ++x)
                    {
                        int index = y * size * size + slice * size + x;
                        pixels[index] = new Color(x * scale, y * scale, slice * scale, 1.0f);
                    }
                }
            }

            lut.SetPixels(pixels);
            lut.Apply();
            return lut;
        }

        // The identity LUT with every channel inverted, used to prove the tests above are not
        // passing vacuously.
        private static Texture2D CreateInvertingLut(int size)
        {
            Texture2D lut = CreateIdentityLut(size);
            lut.name = "InvertingLut";

            Color[] pixels = lut.GetPixels();
            for (int i = 0; i < pixels.Length; ++i)
            {
                pixels[i] = new Color(1.0f - pixels[i].r, 1.0f - pixels[i].g, 1.0f - pixels[i].b, 1.0f);
            }

            lut.SetPixels(pixels);
            lut.Apply();
            return lut;
        }

        // A premultiplied test image standing in for the camera buffer the grade samples: rgb is
        // always colour times alpha, with BOTH already sRGB-encoded.
        //
        // linear:false is load-bearing, not incidental. The camera target is an sRGB surface, so
        // the sampler decodes on the way in and the shader's LinearToSRGB recovers the stored
        // bytes. Building this linear instead would break the premultiplied invariant the grade
        // relies on: encoding is non-linear, so sRGB(C*a) > sRGB(C)*a, and the unpremultiply would
        // divide by an alpha that is now too small and clamp - which is a bug in the test, not in
        // the shader. Compositing premultiplies in the encoded domain, so rgb <= alpha holds.
        //
        // Alpha is kept at or above 0.25 on purpose. The grade unpremultiplies by dividing by
        // alpha, so at very low coverage the 8-bit quantisation of rgb is amplified into a large
        // LUT input error - real, but self-limiting, since the re-premultiply scales it straight
        // back down. Including near-transparent pixels here would test quantisation, not grading.
        private static Texture2D CreatePremultipliedSource(int size)
        {
            Texture2D source = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PremultipliedSource"
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    float r = x / (size - 1.0f);
                    float g = y / (size - 1.0f);
                    float b = ((x + y) % size) / (size - 1.0f);
                    float alpha = Mathf.Lerp(0.25f, 1.0f, (x * y) / ((size - 1.0f) * (size - 1.0f)));

                    pixels[y * size + x] = new Color(r * alpha, g * alpha, b * alpha, alpha);
                }
            }

            source.SetPixels(pixels);
            source.Apply();
            return source;
        }

        // The alpha every render target below is cleared to. Deliberately not 0 or 1: the grade
        // pass declares ColorMask RGB, and this is what proves it never wrote alpha.
        private const float c_clearAlpha = 0.5f;

        // Drives the grade exactly as the render passes do - Graphics.Blit through the material -
        // rather than issuing a quad of the test's own. That matters: the flip bug this suite
        // missed lived in a hand-rolled quad, so a harness that draws its own geometry would not
        // reproduce the real pass at all.
        private static Texture2D RenderToTexture(Material material, Texture2D source, int size)
        {
            RenderTexture target = RenderTexture.GetTemporary(
                size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;

            // Cleared before the blit so ColorMask RGB has a known alpha to leave behind.
            RenderTexture.active = target;
            GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, c_clearAlpha));
            RenderTexture.active = previous;

            Graphics.Blit(source, target, material);

            RenderTexture.active = target;
            Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            result.Apply();
            RenderTexture.active = previous;

            RenderTexture.ReleaseTemporary(target);
            return result;
        }

        // Tolerance is in 8-bit levels.
        private static bool TexturesMatch(Texture2D expected, Texture2D actual, int tolerance)
        {
            Color32[] expectedPixels = expected.GetPixels32();
            Color32[] actualPixels = actual.GetPixels32();

            if (expectedPixels.Length != actualPixels.Length)
            {
                return false;
            }

            for (int i = 0; i < expectedPixels.Length; ++i)
            {
                if (Mathf.Abs(expectedPixels[i].r - actualPixels[i].r) > tolerance
                    || Mathf.Abs(expectedPixels[i].g - actualPixels[i].g) > tolerance
                    || Mathf.Abs(expectedPixels[i].b - actualPixels[i].b) > tolerance
                    || Mathf.Abs(expectedPixels[i].a - actualPixels[i].a) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private static void AssertTexturesMatch(Texture2D expected, Texture2D actual, int tolerance)
        {
            Color32[] expectedPixels = expected.GetPixels32();
            Color32[] actualPixels = actual.GetPixels32();

            Assert.AreEqual(expectedPixels.Length, actualPixels.Length, "images differ in size");

            for (int i = 0; i < expectedPixels.Length; ++i)
            {
                Color32 expectedPixel = expectedPixels[i];
                Color32 actualPixel = actualPixels[i];

                if (Mathf.Abs(expectedPixel.r - actualPixel.r) > tolerance
                    || Mathf.Abs(expectedPixel.g - actualPixel.g) > tolerance
                    || Mathf.Abs(expectedPixel.b - actualPixel.b) > tolerance
                    || Mathf.Abs(expectedPixel.a - actualPixel.a) > tolerance)
                {
                    Assert.Fail($"pixel {i % expected.width},{i / expected.width} differs by more than "
                                + $"{tolerance}/255: expected {expectedPixel}, got {actualPixel}");
                }
            }
        }
    }
}
