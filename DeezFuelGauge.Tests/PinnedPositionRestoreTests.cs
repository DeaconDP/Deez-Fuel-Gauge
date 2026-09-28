using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class PinnedPositionRestoreTests
{
    [Fact]
    public void Resolve_with_compact_size_keeps_near_bottom_right_pin()
    {
        var areas = new[] { (0, 0, 1920, 1080) };
        const int savedLeft = 1800;
        const int savedTop = 1000;
        const double compactW = 120;
        const double compactH = 36;

        var (x, y, moved) = PinnedPositionRestore.Resolve(
            savedLeft,
            savedTop,
            compactW,
            compactH,
            areas);

        Assert.Equal(savedLeft, x);
        Assert.Equal(savedTop, y);
        Assert.False(moved);
    }

    [Fact]
    public void Resolve_landing_pad_depends_on_restore_height_when_offscreen()
    {
        var areas = new[] { (0, 0, 1920, 1080) };
        const int savedLeft = 1800;
        const int savedTop = 5000;

        var (compactX, compactY, compactMoved) = PinnedPositionRestore.Resolve(
            savedLeft,
            savedTop,
            restoreWidth: 120,
            restoreHeight: 36,
            areas);
        var (fullX, fullY) = WindowAnchorHelper.ClampToWorkingAreas(
            savedLeft,
            savedTop,
            120,
            400,
            areas);

        Assert.True(compactMoved);
        Assert.Equal(compactX, fullX);
        // Full-height clamp pulls the pin higher; compact restore can sit lower.
        Assert.True(compactY > fullY);
        Assert.Equal(1080 - 36, compactY);
        Assert.Equal(1080 - 400, fullY);
    }

    [Fact]
    public void Resolve_moves_truly_offscreen_compact_pin_onto_display()
    {
        var areas = new[] { (0, 0, 1920, 1080) };

        var (x, y, moved) = PinnedPositionRestore.Resolve(
            savedLeft: 4000,
            savedTop: 40,
            restoreWidth: 140,
            restoreHeight: 48,
            areas);

        Assert.True(moved);
        Assert.True(WindowAnchorHelper.HasVisibleOverlap(x, y, 140, 48, areas[0]));
    }

    [Fact]
    public void Resolve_keeps_on_screen_full_widget_pin()
    {
        var areas = new[] { (0, 0, 1920, 1080) };

        var (x, y, moved) = PinnedPositionRestore.Resolve(
            savedLeft: 200,
            savedTop: 150,
            restoreWidth: 300,
            restoreHeight: 260,
            areas);

        Assert.Equal(200, x);
        Assert.Equal(150, y);
        Assert.False(moved);
    }

    [Fact]
    public void ResolveStableCompactHost_places_full_hwnd_so_br_pill_stays_on_screen()
    {
        // Repro from a pinned compact rest past the right edge on a 2048-wide work area.
        // Setting Position to the clamped compact rest while the HWND stays full-sized
        // parks the Win32-clipped pill at x>=2048 (invisible / unopenable).
        var areas = new[] { (0, 0, 2048, 1104) };
        const double compactW = 94;
        const double compactH = 126;
        const double fullW = 300;
        const double fullH = 274;

        var (hostX, hostY, restX, restY, moved) = PinnedPositionRestore.ResolveStableCompactHost(
            savedLeft: 2450,
            savedTop: 1046,
            compactW,
            compactH,
            fullW,
            fullH,
            areas);

        Assert.True(moved);
        Assert.Equal(2048 - (int)compactW, restX);
        Assert.Equal(1104 - (int)compactH, restY);
        Assert.Equal(restX + (int)compactW - (int)fullW, hostX);
        Assert.Equal(restY + (int)compactH - (int)fullH, hostY);
        Assert.True(WindowAnchorHelper.HasVisibleOverlap(
            restX, restY, (int)compactW, (int)compactH, areas[0]));

        // Wrong restore (Position = compact rest, HWND = full) puts the BR pill off-screen.
        var wrongPillX = restX + (int)fullW - (int)compactW;
        Assert.True(wrongPillX >= 2048);
    }
}
