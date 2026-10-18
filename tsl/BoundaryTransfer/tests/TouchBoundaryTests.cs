namespace BoundaryTransfer.Tests;

/// <summary>仅测试纯碰边几何、朝外意图与重新武装算法；不启动 Terraria。</summary>
internal static class TouchBoundaryTests
{
    // 独立固定真实 BordersMovement 边距，避免从被测常量派生期望而掩盖回归。
    private const int Padding = 640;
    private const int Rearm = 128;
    private const float WorldRight = 4200 * 16f;

    public static void Add(List<AlgorithmCase> cases)
    {
        AddContact(cases);
        AddOutward(cases);
        AddValidation(cases);
        AddInterior(cases);
        AddProperties(cases);
    }

    private static void AddContact(List<AlgorithmCase> cases)
    {
        cases.Add("contact.body.native-limits.exact", "body-contact", () =>
        {
            foreach (int width in new[] { 1, 20, 128 })
            {
                Check.Equal<BoundarySide?>(BoundarySide.Left, BoundaryContact.Side(Padding, width, 0, WorldRight, 0));
                Check.Equal<BoundarySide?>(BoundarySide.Right, BoundaryContact.Side(WorldRight - Padding - width, width, 0, WorldRight, 0));
            }
        });
        cases.Add("contact.body-not-center.left-and-right", "body-contact", () =>
        {
            const int width = 128;
            float leftX = Padding, rightX = WorldRight - Padding - width;
            Check.True(leftX + width / 2f > Padding + 8);
            Check.True(rightX + width / 2f < WorldRight - Padding - 8);
            Check.Equal<BoundarySide?>(BoundarySide.Left, BoundaryContact.Side(leftX, width, 0, WorldRight, 0));
            Check.Equal<BoundarySide?>(BoundarySide.Right, BoundaryContact.Side(rightX, width, 0, WorldRight, 0));
        });
        cases.Add("contact.no-legacy-48-tile-distance", "body-contact", () =>
        {
            foreach (int width in new[] { 1, 20, 128 })
                foreach (int tolerance in new[] { 0, 2, 8 })
                {
                    // 旧算法按中心距边48格触发；现在身体还没碰640px原生边界就不能切图。
                    float x = 48 * 16 - width / 2f;
                    Check.Equal<BoundarySide?>(null, BoundaryContact.Side(x, width, 0, WorldRight, tolerance));
                    Check.Equal<BoundarySide?>(null, BoundaryContact.Side(WorldRight - x - width, width, 0, WorldRight, tolerance));
                    Check.Equal<BoundarySide?>(null, BoundaryContact.Side(WorldRight / 2 - width / 2f, width, 0, WorldRight, tolerance));
                }
        });
        cases.Add("contact.tolerance.default-two-pixels.inclusive", "body-contact", () =>
        {
            int tolerance = BoundarySettings.Default.ContactTolerance;
            Check.Equal(2, tolerance);
            foreach (float gap in new[] { 0f, 0.25f, 1.75f, 2f, 2.25f, 3f, 8f })
            {
                Check.Equal<BoundarySide?>(gap <= 2 ? BoundarySide.Left : null,
                    BoundaryContact.Side(Padding + gap, 20, 0, WorldRight, tolerance));
                Check.Equal<BoundarySide?>(gap <= 2 ? BoundarySide.Right : null,
                    BoundaryContact.Side(WorldRight - Padding - 20 - gap, 20, 0, WorldRight, tolerance));
            }
        });
        cases.Add("contact.tolerance.zero.requires-exact", "body-contact", () =>
        {
            Check.Equal<BoundarySide?>(BoundarySide.Left, BoundaryContact.Side(Padding, 20, 0, WorldRight, 0));
            Check.Equal<BoundarySide?>(null, BoundaryContact.Side(Padding + 0.25f, 20, 0, WorldRight, 0));
            Check.Equal<BoundarySide?>(BoundarySide.Right, BoundaryContact.Side(WorldRight - Padding - 20, 20, 0, WorldRight, 0));
            Check.Equal<BoundarySide?>(null, BoundaryContact.Side(WorldRight - Padding - 20 - 0.25f, 20, 0, WorldRight, 0));
        });
        cases.Add("contact.tolerance.zero-through-eight.exact-thresholds", "body-contact", () =>
        {
            const float left = 123.5f, right = left + WorldRight;
            for (int tolerance = 0; tolerance <= 8; tolerance++)
                foreach (int width in new[] { 1, 20, 128 })
                {
                    float x = left + Padding + tolerance;
                    Check.Equal<BoundarySide?>(BoundarySide.Left, BoundaryContact.Side(x, width, left, right, tolerance));
                    Check.Equal<BoundarySide?>(null, BoundaryContact.Side(x + 0.25f, width, left, right, tolerance));
                    x = right - Padding - width - tolerance;
                    Check.Equal<BoundarySide?>(BoundarySide.Right, BoundaryContact.Side(x, width, left, right, tolerance));
                    Check.Equal<BoundarySide?>(null, BoundaryContact.Side(x - 0.25f, width, left, right, tolerance));
                }
        });
        cases.Add("contact.native-clamp-overshoot.still-inside-world", "body-contact", () =>
        {
            foreach (float x in new[] { 0f, 1f, Padding - 0.25f, Padding })
            {
                Check.Equal<BoundarySide?>(BoundarySide.Left, BoundaryContact.Side(x, 20, 0, WorldRight, 0));
                Check.Equal<BoundarySide?>(BoundarySide.Right, BoundaryContact.Side(WorldRight - x - 20, 20, 0, WorldRight, 0));
            }
        });
        cases.Add("contact.geometry.independent-of-motion-or-key-input", "body-contact", () =>
        {
            // Side仅判几何接触；按键/速度由独立IsOutward谓词约束，不混入身体接触判断。
            for (int repeat = 0; repeat < 8; repeat++)
            {
                Check.Equal<BoundarySide?>(BoundarySide.Left, BoundaryContact.Side(Padding, 20, 0, WorldRight, 2));
                Check.Equal<BoundarySide?>(BoundarySide.Right, BoundaryContact.Side(WorldRight - Padding - 20, 20, 0, WorldRight, 2));
            }
        });
        cases.Add("contact.worlds.small-medium-large-and-offset", "body-contact", () =>
        {
            foreach (var (left, span) in new[] { (0f, 128 * 16f), (0f, 4200 * 16f), (1234.5f, 6400 * 16f), (-2048.5f, 8400 * 16f) })
                foreach (int width in new[] { 1, 20, 128 })
                {
                    float right = left + span;
                    Check.Equal<BoundarySide?>(BoundarySide.Left, BoundaryContact.Side(left + Padding, width, left, right, 0));
                    Check.Equal<BoundarySide?>(BoundarySide.Right, BoundaryContact.Side(right - Padding - width, width, left, right, 0));
                    float middleX = left + (span - width) / 2f;
                    Check.Equal<BoundarySide?>(null, BoundaryContact.Side(middleX, width, left, right, 8));
                    Check.True(BoundaryContact.IsInterior(middleX, width, left, right));
                }
        });
        cases.Add("contact.worlds.nonzero-left-not-an-absolute-origin", "body-contact", () =>
        {
            foreach (float left in new[] { -10000.5f, 128.25f, 200000f })
            {
                float right = left + WorldRight;
                Check.Equal<BoundarySide?>(BoundarySide.Left, BoundaryContact.Side(left + Padding + 2, 20, left, right, 2));
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(left + Padding + 2.25f, 20, left, right, 2));
                Check.Equal<BoundarySide?>(BoundarySide.Right, BoundaryContact.Side(right - Padding - 22, 20, left, right, 2));
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(right - Padding - 22.25f, 20, left, right, 2));
            }
        });
    }

    private static void AddOutward(List<AlgorithmCase> cases)
    {
        cases.Add("outward.keys.zero-velocity-native-clamp", "outward-intent", () =>
        {
            Check.True(BoundaryContact.IsOutward(BoundarySide.Left, 0, true, false));
            Check.True(BoundaryContact.IsOutward(BoundarySide.Right, 0, false, true));
            Check.False(BoundaryContact.IsOutward(BoundarySide.Left, 0, false, false));
            Check.False(BoundaryContact.IsOutward(BoundarySide.Right, 0, false, false));
        });
        cases.Add("outward.outward-key.overrides-inward-velocity", "outward-intent", () =>
        {
            foreach (float inwardSpeed in new[] { 0.01f, 0.0101f, 1f, 100f })
            {
                Check.True(BoundaryContact.IsOutward(BoundarySide.Left, inwardSpeed, true, false));
                Check.True(BoundaryContact.IsOutward(BoundarySide.Right, -inwardSpeed, false, true));
            }
        });
        cases.Add("outward.reverse-key.blocks-old-outward-inertia", "outward-intent", () =>
        {
            foreach (float outwardSpeed in new[] { 0f, 0.01f, 0.0101f, 1f, 100f })
            {
                Check.False(BoundaryContact.IsOutward(BoundarySide.Left, -outwardSpeed, false, true));
                Check.False(BoundaryContact.IsOutward(BoundarySide.Right, outwardSpeed, true, false));
            }
        });
        cases.Add("outward.both-keys.always-block", "outward-intent", () =>
        {
            foreach (BoundarySide side in Enum.GetValues<BoundarySide>())
                foreach (float velocity in new[] { -100f, -0.0101f, -0.01f, 0f, 0.01f, 0.0101f, 100f })
                    Check.False(BoundaryContact.IsOutward(side, velocity, true, true));
        });
        cases.Add("outward.no-key.deadzone-inclusive", "outward-intent", () =>
        {
            foreach (BoundarySide side in Enum.GetValues<BoundarySide>())
                foreach (float velocity in new[] { -0.01f, -0.0099f, -0f, 0f, 0.0099f, 0.01f })
                    Check.False(BoundaryContact.IsOutward(side, velocity, false, false));
        });
        cases.Add("outward.no-key.velocity-sign-and-strict-threshold", "outward-intent", () =>
        {
            foreach (float speed in new[] { 0.0101f, 0.25f, 1f, 100f, float.MaxValue })
            {
                Check.True(BoundaryContact.IsOutward(BoundarySide.Left, -speed, false, false));
                Check.False(BoundaryContact.IsOutward(BoundarySide.Left, speed, false, false));
                Check.True(BoundaryContact.IsOutward(BoundarySide.Right, speed, false, false));
                Check.False(BoundaryContact.IsOutward(BoundarySide.Right, -speed, false, false));
            }
        });
        cases.Add("outward.nonfinite-velocity.always-rejected", "outward-intent", () =>
        {
            foreach (BoundarySide side in Enum.GetValues<BoundarySide>())
                foreach (float velocity in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                    foreach (bool controlLeft in new[] { false, true })
                        foreach (bool controlRight in new[] { false, true })
                            Check.False(BoundaryContact.IsOutward(side, velocity, controlLeft, controlRight));
        });
        cases.Add("outward.property.fixed-seed.direction-mirror-and-oracle", "contact-properties", () =>
        {
            var random = new Random(314159);
            for (int i = 0; i < 512; i++)
            {
                float velocity = i % 4 == 0 ? new[] { -0.0101f, -0.01f, 0f, 0.01f, 0.0101f }[random.Next(0, 5)]
                    : random.Next(-4000, 4001) / 100f;
                bool controlLeft = random.Next(0, 2) == 1, controlRight = random.Next(0, 2) == 1;
                bool left = BoundaryContact.IsOutward(BoundarySide.Left, velocity, controlLeft, controlRight);
                bool right = BoundaryContact.IsOutward(BoundarySide.Right, velocity, controlLeft, controlRight);
                string context = $"seed=314159 index={i}";
                Check.Equal(!controlRight && (controlLeft || velocity < -0.01f), left, context);
                Check.Equal(!controlLeft && (controlRight || velocity > 0.01f), right, context);
                Check.Equal(left, BoundaryContact.IsOutward(BoundarySide.Right, -velocity, controlRight, controlLeft), context);
                Check.Equal(right, BoundaryContact.IsOutward(BoundarySide.Left, -velocity, controlRight, controlLeft), context);
            }
        });
    }

    private static void AddValidation(List<AlgorithmCase> cases)
    {
        cases.Add("contact.invalid-tolerance.rejected", "contact-input-validation", () =>
        {
            foreach (int tolerance in new[] { -1, 9, int.MinValue, int.MaxValue })
            {
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(Padding, 20, 0, WorldRight, tolerance));
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(WorldRight - Padding - 20, 20, 0, WorldRight, tolerance));
            }
        });
        cases.Add("contact.invalid-width.rejected", "contact-input-validation", () =>
        {
            foreach (int width in new[] { int.MinValue, -1, 0, 129, int.MaxValue })
            {
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(Padding, width, 0, WorldRight, 2));
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(WorldRight - Padding - 20, width, 0, WorldRight, 2));
                Check.False(BoundaryContact.IsInterior(WorldRight / 2, width, 0, WorldRight));
            }
        });
        cases.Add("contact.nonfinite-position.rejected", "contact-input-validation", () =>
        {
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(value, 20, 0, WorldRight, 2));
                Check.False(BoundaryContact.IsInterior(value, 20, 0, WorldRight));
            }
        });
        cases.Add("contact.nonfinite-world-bounds.rejected", "contact-input-validation", () =>
        {
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(Padding, 20, value, WorldRight, 2));
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(Padding, 20, 0, value, 2));
                Check.False(BoundaryContact.IsInterior(WorldRight / 2, 20, value, WorldRight));
                Check.False(BoundaryContact.IsInterior(WorldRight / 2, 20, 0, value));
            }
        });
        cases.Add("contact.extreme-finite-position.rejected", "contact-input-validation", () =>
        {
            foreach (float value in new[] { float.MaxValue, -float.MaxValue, 1e30f, -1e30f })
            {
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(value, 20, 0, WorldRight, 2));
                Check.False(BoundaryContact.IsInterior(value, 20, 0, WorldRight));
            }
        });
        cases.Add("contact.world-too-small.strict-body-and-rearm-fit", "contact-input-validation", () =>
        {
            const float left = 125;
            foreach (int width in new[] { 1, 20, 128 })
            {
                float minimumSpan = 2 * (Padding + Rearm) + width;
                foreach (float span in new[] { -1f, 0f, 1f, 128f, 2f * Padding, minimumSpan - 0.25f, minimumSpan })
                {
                    Check.Equal<BoundarySide?>(null, BoundaryContact.Side(left + Padding, width, left, left + span, 2));
                    Check.False(BoundaryContact.IsInterior(left + Padding + Rearm + 0.25f, width, left, left + span));
                }
                float right = left + minimumSpan + 0.5f;
                Check.Equal<BoundarySide?>(BoundarySide.Left, BoundaryContact.Side(left + Padding, width, left, right, 0));
                Check.Equal<BoundarySide?>(BoundarySide.Right, BoundaryContact.Side(right - Padding - width, width, left, right, 0));
                Check.True(BoundaryContact.IsInterior(left + Padding + Rearm + 0.25f, width, left, right));
            }
        });
        cases.Add("contact.body-outside-entire-world.rejected", "contact-input-validation", () =>
        {
            const float left = -123.5f, right = left + WorldRight;
            foreach (int width in new[] { 1, 20, 128 })
            {
                foreach (float x in new[] { left - width - 1, left - 0.25f, right - width + 0.25f, right, right + width })
                {
                    Check.Equal<BoundarySide?>(null, BoundaryContact.Side(x, width, left, right, 8));
                    Check.False(BoundaryContact.IsInterior(x, width, left, right));
                }
                // 整个世界坐标范围是闭区间；与原生可行走范围不同，不能把两者混淆。
                Check.Equal<BoundarySide?>(BoundarySide.Left, BoundaryContact.Side(left, width, left, right, 0));
                Check.Equal<BoundarySide?>(BoundarySide.Right, BoundaryContact.Side(right - width, width, left, right, 0));
            }
        });
    }

    private static void AddInterior(List<AlgorithmCase> cases)
    {
        cases.Add("interior.left-rearm.strict-entire-body", "contact-rearm", () =>
        {
            const float left = 123.5f, right = left + WorldRight;
            float line = left + Padding + Rearm;
            foreach (int width in new[] { 1, 20, 128 })
            {
                Check.False(BoundaryContact.IsInterior(line - 0.25f, width, left, right));
                Check.False(BoundaryContact.IsInterior(line, width, left, right));
                Check.True(BoundaryContact.IsInterior(line + 0.25f, width, left, right));
                Check.True(line + width / 2f > line, "即使中心越线，身体左侧等于界线仍不能重新武装。");
            }
        });
        cases.Add("interior.right-rearm.strict-entire-body", "contact-rearm", () =>
        {
            const float left = -128.5f, right = left + WorldRight;
            float line = right - Padding - Rearm;
            foreach (int width in new[] { 1, 20, 128 })
            {
                Check.False(BoundaryContact.IsInterior(line - width + 0.25f, width, left, right));
                Check.False(BoundaryContact.IsInterior(line - width, width, left, right));
                Check.True(BoundaryContact.IsInterior(line - width - 0.25f, width, left, right));
                Check.True(line - width / 2f < line, "即使中心越线，身体右侧等于界线仍不能重新武装。");
            }
        });
        cases.Add("interior.rearm-eight-tiles-not-contact-tolerance", "contact-rearm", () =>
        {
            foreach (float gap in new[] { 0f, 2f, 8f, 127.75f, 128f, 128.25f, 129f })
            {
                Check.Equal(gap > Rearm, BoundaryContact.IsInterior(Padding + gap, 20, 0, WorldRight));
                Check.Equal(gap > Rearm, BoundaryContact.IsInterior(WorldRight - Padding - 20 - gap, 20, 0, WorldRight));
                foreach (int tolerance in new[] { 0, 2, 8 })
                {
                    Check.Equal<BoundarySide?>(gap <= tolerance ? BoundarySide.Left : null,
                        BoundaryContact.Side(Padding + gap, 20, 0, WorldRight, tolerance));
                    Check.Equal<BoundarySide?>(gap <= tolerance ? BoundarySide.Right : null,
                        BoundaryContact.Side(WorldRight - Padding - 20 - gap, 20, 0, WorldRight, tolerance));
                }
            }
        });
        cases.Add("interior.neither-side-can-remain-on-rearm-line", "contact-rearm", () =>
        {
            const int width = 128;
            float left = Padding + Rearm, right = WorldRight - Padding - Rearm;
            Check.False(BoundaryContact.IsInterior(left - width / 2f, width, 0, WorldRight));
            Check.False(BoundaryContact.IsInterior(right - width / 2f, width, 0, WorldRight));
            Check.True(BoundaryContact.IsInterior((WorldRight - width) / 2, width, 0, WorldRight));
        });
    }

    private static void AddProperties(List<AlgorithmCase> cases)
    {
        cases.Add("contact.property.fixed-seed.left-right-body-symmetry", "contact-properties", () =>
        {
            var random = new Random(314159);
            for (int i = 0; i < 512; i++)
            {
                float left = random.Next(-400000, 400001) / 4f;
                int span = random.Next(128, 8401) * 16, width = random.Next(1, 129), tolerance = random.Next(0, 9);
                float right = left + span;
                float localX = i % 3 == 0 ? Padding + random.Next(-128, 1401) / 4f
                    : i % 3 == 1 ? span - Padding - width - random.Next(-128, 1401) / 4f
                    : random.Next(-32, span * 4 + 33) / 4f;
                float x = left + localX, mirror = right - localX - width;
                string context = $"seed=314159 index={i} width={width} tolerance={tolerance}";
                var side = BoundaryContact.Side(x, width, left, right, tolerance);
                Check.Equal(ReferenceSide(localX, width, span, tolerance), side, context);
                Check.Equal(Opposite(side), BoundaryContact.Side(mirror, width, left, right, tolerance), context);
                bool interior = localX > Padding + Rearm && localX + width < span - Padding - Rearm;
                Check.Equal(interior, BoundaryContact.IsInterior(x, width, left, right), context);
                Check.Equal(interior, BoundaryContact.IsInterior(mirror, width, left, right), context);
            }
        });
        cases.Add("contact.property.fixed-seed.translation-invariant", "contact-properties", () =>
        {
            var random = new Random(314159);
            for (int i = 0; i < 512; i++)
            {
                int span = random.Next(128, 8401) * 16, width = random.Next(1, 129), tolerance = random.Next(0, 9);
                float shift = random.Next(-400000, 400001) / 4f;
                float x = i % 2 == 0 ? Padding + random.Next(-64, 801) / 4f
                    : span - Padding - width - random.Next(-64, 801) / 4f;
                Check.Equal(BoundaryContact.Side(x, width, 0, span, tolerance),
                    BoundaryContact.Side(x + shift, width, shift, span + shift, tolerance), $"seed=314159 index={i}");
                Check.Equal(BoundaryContact.IsInterior(x, width, 0, span),
                    BoundaryContact.IsInterior(x + shift, width, shift, span + shift), $"seed=314159 index={i}");
            }
        });
        cases.Add("contact.property.fixed-seed.strict-rearm-and-no-overlap", "contact-properties", () =>
        {
            var random = new Random(314159);
            for (int i = 0; i < 512; i++)
            {
                int width = random.Next(1, 129), span = random.Next(128, 8401) * 16;
                float left = random.Next(-400000, 400001) / 4f, right = left + span;
                float gap = Rearm + random.Next(-32, 33) / 4f;
                float x = left + Padding + gap, mirror = right - Padding - width - gap;
                bool expected = gap > Rearm;
                Check.Equal(expected, BoundaryContact.IsInterior(x, width, left, right), $"seed=314159 index={i}");
                Check.Equal(expected, BoundaryContact.IsInterior(mirror, width, left, right), $"seed=314159 index={i}");
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(x, width, left, right, 8));
                Check.Equal<BoundarySide?>(null, BoundaryContact.Side(mirror, width, left, right, 8));
            }
        });
    }

    private static BoundarySide? ReferenceSide(float localX, int width, int span, int tolerance)
    {
        if (localX < 0 || localX > span - width) return null;
        float leftGap = localX - Padding, rightGap = span - (localX + width) - Padding;
        return leftGap <= tolerance ? BoundarySide.Left : rightGap <= tolerance ? BoundarySide.Right : null;
    }

    private static BoundarySide? Opposite(BoundarySide? side) => side switch
    {
        BoundarySide.Left => BoundarySide.Right,
        BoundarySide.Right => BoundarySide.Left,
        _ => null
    };
}
