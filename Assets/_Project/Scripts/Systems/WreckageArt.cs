using System.Collections.Generic;
using UnityEngine;

// Pixel art for what a space station sheds as it shakes apart, drawn in code at the ship tileset's pixel size. Every piece
// is drawn fresh from a random seed, so no two look quite alike:
//   Panel    a white hull or ceiling panel, bent and torn, with its seams and rivets
//   Foil     a torn scrap of gold insulation blanket (the crinkled foil wrapped around a spacecraft)
//   Pipe     a length of coolant line, flanged at one end and snapped off at the other
//   Truss    a section of lattice girder with hazard striping
//   Grate    a vent grate knocked out of its frame
//   Fixture  an LED strip light, its diffuser cracked
//   Cables   a bundle of wiring torn out by the roots, the ends frayed
//   Circuit  an electronics module: a circuit board in its tray, chips and contacts showing
//   Scatter  loose odds and ends: bolts, nuts, shards of panel, snippets of cable, a chip
//   Pile     several of the above heaped up, for wreckage that blocks the way
//   RobotPart  a piece off a robot that's just been destroyed: plate, strut, lens, bolt, board, or wiring
// and the marks left on the floor (FloorStain): a scorch where something burned, and a pool of leaked coolant.
// FallingDebris uses these; each call makes a new canvas, so turn it into a sprite and destroy that when you're done.
public static class WreckageArt
{
    static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

    static readonly Color32 Ink = C(20, 22, 28);
    static readonly Color32 HullLight = C(216, 218, 220);
    static readonly Color32 HullMid = C(178, 182, 188);
    static readonly Color32 Steel = C(128, 134, 142);
    static readonly Color32 SteelDark = C(82, 88, 96);
    static readonly Color32 Rivet = C(68, 72, 80);
    static readonly Color32 Gold = C(212, 168, 70);
    static readonly Color32 GoldLight = C(252, 224, 142);
    static readonly Color32 GoldDark = C(112, 80, 30);
    static readonly Color32 Silver = C(196, 200, 208);
    static readonly Color32 Coolant = C(66, 140, 206);
    static readonly Color32 Hazard = C(226, 182, 52);
    static readonly Color32 HazardBlack = C(34, 32, 30);
    static readonly Color32 Board = C(36, 88, 60);
    static readonly Color32 Trace = C(98, 170, 114);
    static readonly Color32 Chip = C(26, 26, 30);
    static readonly Color32 Pins = C(150, 154, 160);
    static readonly Color32 Contact = C(224, 186, 92);
    static readonly Color32 Led = C(255, 70, 50);
    static readonly Color32 Diffuser = C(255, 240, 208);
    static readonly Color32 DiffuserSeam = C(196, 182, 156);
    static readonly Color32 Copper = C(216, 130, 72);
    static readonly Color32[] Insulation =
    {
        C(188, 54, 44), C(54, 96, 188), C(216, 174, 46), C(44, 44, 48), C(226, 226, 226), C(64, 150, 88),
    };

    // The colour of the chips each kind throws when it lands.
    public static readonly Color PanelChips = (Color)HullMid;
    public static readonly Color FoilChips = (Color)Gold;
    public static readonly Color MetalChips = (Color)Steel;
    public static readonly Color GlassChips = (Color)Diffuser;
    public static readonly Color WireChips = (Color)Insulation[0];
    public static readonly Color BoardChips = (Color)Board;

    static int Between(System.Random dice, int min, int max) => dice.Next(min, max + 1);
    static float Between(System.Random dice, float min, float max) => min + (float)dice.NextDouble() * (max - min);
    static bool Chance(System.Random dice, float chance) => dice.NextDouble() < chance;

    static PixelCanvas Finish(PixelCanvas canvas, System.Random dice, float grain, bool bevel = true)
    {
        canvas.Grain(dice, grain);
        if (bevel) canvas.Bevel();
        canvas.Outline(Ink);
        return canvas;
    }

    // --- The pieces ---

    public static PixelCanvas Panel(System.Random dice)
    {
        int w = Between(dice, 26, 36), h = Between(dice, 20, 28);
        var canvas = new PixelCanvas(w + 2, h + 2);

        // A rectangle with one corner torn away.
        Vector2[] corners = { new Vector2(1, 1), new Vector2(w + 1, 1), new Vector2(w + 1, h + 1), new Vector2(1, h + 1) };
        Vector2 middle = new Vector2(w * 0.5f + 1, h * 0.5f + 1);
        int torn = dice.Next(4);
        var outline = new List<Vector2>();
        for (int i = 0; i < 4; i++)
        {
            Vector2 corner = corners[i];
            if (i != torn)
            {
                outline.Add(corner + new Vector2(Between(dice, -1f, 1f), Between(dice, -1f, 1f)));
                continue;
            }
            Vector2 from = Vector2.Lerp(corner, corners[(i + 3) % 4], Between(dice, 0.25f, 0.45f));
            Vector2 to = Vector2.Lerp(corner, corners[(i + 1) % 4], Between(dice, 0.25f, 0.45f));
            outline.Add(from);
            for (int j = 1; j <= 3; j++)
            {
                Vector2 along = Vector2.Lerp(from, to, j / 4f);
                outline.Add(along + (middle - along).normalized * Between(dice, 0f, 3f) * (j % 2 == 0 ? -1f : 1f));
            }
            outline.Add(to);
        }

        Color32 face = Chance(dice, 0.7f) ? HullLight : HullMid;
        canvas.FillPolygon(outline.ToArray(), (x, y) => PixelCanvas.Scale(face, 0.9f + 0.12f * y / h));

        // A seam inset round the edge, with rivets at its corners.
        const int inset = 4;
        for (int x = inset; x <= w + 1 - inset; x++)
        {
            Darken(canvas, x, inset, 0.78f);
            Darken(canvas, x, h + 2 - inset, 0.78f);
            Darken(canvas, x, h + 1 - inset, 1.08f);
        }
        for (int y = inset; y <= h + 2 - inset; y++)
        {
            Darken(canvas, inset, y, 0.78f);
            Darken(canvas, w + 2 - inset, y, 0.78f);
            Darken(canvas, inset + 1, y, 1.08f);
        }
        foreach (Vector2Int at in new[] { new Vector2Int(inset - 2, inset - 2), new Vector2Int(w - inset + 2, inset - 2),
                                          new Vector2Int(inset - 2, h - inset + 2), new Vector2Int(w - inset + 2, h - inset + 2) })
        {
            canvas.Paint(at.x, at.y, Rivet);
            canvas.Paint(at.x + 1, at.y, Rivet);
            canvas.Paint(at.x, at.y + 1, PixelCanvas.Scale(Rivet, 1.8f));
        }

        // A warning band along one side, sometimes.
        if (Chance(dice, 0.4f))
        {
            int bandTop = h - inset - 1;
            for (int y = bandTop - 2; y <= bandTop; y++)
                for (int x = inset + 2; x <= w - inset; x++)
                    canvas.Paint(x, y, ((x + y) / 3) % 2 == 0 ? Hazard : HazardBlack);
        }

        // Bent across the middle: a lit ridge with a shadow under it.
        if (Chance(dice, 0.5f))
        {
            int y0 = Between(dice, h / 3, 2 * h / 3), y1 = Between(dice, h / 3, 2 * h / 3);
            for (int x = 1; x <= w + 1; x++)
            {
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, (float)x / (w + 1)));
                Darken(canvas, x, y, 1.15f);
                Darken(canvas, x, y - 1, 0.75f);
            }
        }

        // Scuffs.
        for (int i = Between(dice, 1, 3); i > 0; i--)
        {
            int x = Between(dice, 3, w - 3), y = Between(dice, 3, h - 3);
            for (int step = 0; step < Between(dice, 3, 6); step++) Darken(canvas, x + step, y + step / 2, 0.88f);
        }
        return Finish(canvas, dice, 0.035f);
    }

    public static PixelCanvas Foil(System.Random dice)
    {
        int radius = Between(dice, 11, 15);
        var canvas = new PixelCanvas(radius * 2 + 6, radius * 2 + 4);
        Vector2 middle = new Vector2(canvas.Width * 0.5f, canvas.Height * 0.5f);

        // A ragged scrap: points round a circle, each pulled in by a different amount.
        int count = 11;
        var outline = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Between(dice, -0.3f, 0.3f)) / count * Mathf.PI * 2f;
            float reach = radius * Between(dice, 0.55f, 1f);
            outline[i] = middle + new Vector2(Mathf.Cos(angle) * reach * 1.15f, Mathf.Sin(angle) * reach * 0.85f);
        }

        // Crinkled: creases run across it, and each facet between them catches the light differently.
        var creasePoints = new Vector2[6];
        var creaseNormals = new Vector2[6];
        for (int i = 0; i < creasePoints.Length; i++)
        {
            creasePoints[i] = middle + new Vector2(Between(dice, -radius, radius), Between(dice, -radius, radius));
            float angle = Between(dice, 0f, Mathf.PI * 2f);
            creaseNormals[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        Color32 sheet = Chance(dice, 0.75f) ? Gold : Silver;
        canvas.FillPolygon(outline, (x, y) =>
        {
            float shade = 1f;
            for (int i = 0; i < creasePoints.Length; i++)
                shade += Vector2.Dot(new Vector2(x, y) - creasePoints[i], creaseNormals[i]) > 0f ? 0.1f : -0.1f;
            return PixelCanvas.Scale(sheet, Mathf.Clamp(shade, 0.6f, 1.35f));
        });

        // Glints where the foil faces the light.
        Color32 glint = sheet.r == Gold.r ? GoldLight : C(240, 244, 250);
        for (int i = 0; i < canvas.Width * canvas.Height / 18; i++)
            canvas.Paint(dice.Next(canvas.Width), dice.Next(canvas.Height), glint);

        canvas.Grain(dice, 0.05f);
        canvas.Bevel(1.2f, 0.75f);
        canvas.Outline(sheet.r == Gold.r ? GoldDark : SteelDark);
        return canvas;
    }

    public static PixelCanvas Pipe(System.Random dice)
    {
        int length = Between(dice, 52, 66), thickness = Between(dice, 7, 9);
        var canvas = new PixelCanvas(length + 2, thickness + 6);
        int bottom = 3, top = bottom + thickness - 1;
        bool white = Chance(dice, 0.5f);
        Color32 lit = white ? HullLight : HullMid, dark = white ? Steel : SteelDark;

        // Round: dark underneath, a bright line along the top where the light catches it.
        System.Func<int, int, Color32> round = (x, y) =>
        {
            float across = (float)(y - bottom) / Mathf.Max(1, thickness - 1);
            Color32 color = PixelCanvas.Mix(dark, lit, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, across * 1.4f)));
            return Mathf.Abs(across - 0.72f) < 0.12f ? PixelCanvas.Scale(color, 1.15f) : color;
        };
        for (int x = 1; x <= length; x++)
            for (int y = bottom; y <= top; y++)
                canvas.Set(x, y, round(x, y));

        // The flange at the end that came away cleanly.
        for (int x = 1; x <= 4; x++)
            for (int y = bottom - 2; y <= top + 2; y++)
                canvas.Set(x, y, PixelCanvas.Scale(round(x, Mathf.Clamp(y, bottom, top)), x == 1 ? 1.1f : 0.8f));

        // Clamps along it, and a coolant band.
        for (int x = Between(dice, 12, 16); x < length - 6; x += Between(dice, 14, 18))
            for (int y = bottom - 1; y <= top + 1; y++)
            {
                canvas.Set(x, y, PixelCanvas.Scale(round(x, Mathf.Clamp(y, bottom, top)), 0.72f));
                canvas.Set(x + 1, y, PixelCanvas.Scale(round(x, Mathf.Clamp(y, bottom, top)), 0.85f));
            }
        int band = Between(dice, length / 3, length / 2);
        for (int x = band; x < band + 5; x++)
            for (int y = bottom; y <= top; y++)
            {
                float across = (float)(y - bottom) / Mathf.Max(1, thickness - 1);
                canvas.Set(x, y, PixelCanvas.Scale(Coolant, Mathf.Lerp(0.65f, 1.2f, across)));
            }

        // Snapped off at the other end: jagged, and dark inside.
        for (int y = bottom; y <= top; y++)
        {
            int cut = Between(dice, 0, 4);
            for (int x = length - cut + 1; x <= length; x++) canvas.Clear(x, y);
            canvas.Paint(length - cut, y, SteelDark);
        }
        return Finish(canvas, dice, 0.025f, bevel: false);
    }

    public static PixelCanvas Truss(System.Random dice)
    {
        int length = Between(dice, 60, 74), height = Between(dice, 13, 16);
        var canvas = new PixelCanvas(length + 2, height + 2);
        int low = 1, high = height;

        // Two rails, posts between them, and bracing zig-zagging from post to post.
        for (int x = 1; x <= length; x++)
            for (int y = 0; y < 3; y++)
            {
                canvas.Set(x, high - y, y == 0 ? HullMid : Steel);
                canvas.Set(x, low + y, y == 2 ? Steel : SteelDark);
            }
        int spacing = Between(dice, 10, 13);
        bool up = true;
        for (int x = 1; x + spacing <= length; x += spacing)
        {
            canvas.Line(x, low + 2, x, high - 2, Steel, 2);
            canvas.Line(x + 1, up ? low + 2 : high - 2, x + spacing, up ? high - 2 : low + 2, SteelDark, 2);
            up = !up;
        }

        // Hazard striping on the end plate.
        for (int x = 1; x <= 6; x++)
            for (int y = low; y <= high; y++)
                canvas.Set(x, y, ((x + y) / 3) % 2 == 0 ? Hazard : HazardBlack);

        // Torn off at the far end, sometimes.
        if (Chance(dice, 0.6f))
            for (int y = low; y <= high; y++)
                for (int x = length - Between(dice, 0, 5); x <= length; x++) canvas.Clear(x, y);

        return Finish(canvas, dice, 0.03f);
    }

    public static PixelCanvas Grate(System.Random dice)
    {
        int size = Between(dice, 22, 28);
        var canvas = new PixelCanvas(size + 2, size + 2);
        canvas.FillRect(1, 1, size, size, Steel);

        // Slats with the floor showing between them.
        for (int y = 5; y <= size - 4; y++)
        {
            bool gap = (y - 5) % 4 >= 2;
            for (int x = 5; x <= size - 4; x++)
            {
                if (gap) canvas.Clear(x, y);
                else canvas.Set(x, y, (y - 5) % 4 == 0 ? SteelDark : HullMid);
            }
        }

        // Screws in the frame, one of them sheared, and a corner bent away.
        foreach (Vector2Int at in new[] { new Vector2Int(2, 2), new Vector2Int(size - 2, 2), new Vector2Int(2, size - 2), new Vector2Int(size - 2, size - 2) })
            canvas.Set(at.x, at.y, Rivet);
        int corner = dice.Next(4);
        int cx = corner % 2 == 0 ? 1 : size, cy = corner < 2 ? 1 : size;
        int bite = Between(dice, 3, 6);
        for (int i = 0; i < bite; i++)
            for (int j = 0; j < bite - i; j++)
                canvas.Clear(cx + (corner % 2 == 0 ? i : -i), cy + (corner < 2 ? j : -j));

        return Finish(canvas, dice, 0.03f);
    }

    public static PixelCanvas Fixture(System.Random dice)
    {
        int w = Between(dice, 30, 36), h = 10;
        var canvas = new PixelCanvas(w + 2, h + 2);
        canvas.FillRect(1, 1, w, h, SteelDark);
        canvas.FillRect(2, h, w - 1, h, HullMid);
        foreach (Vector2Int at in new[] { new Vector2Int(1, 1), new Vector2Int(w, 1), new Vector2Int(1, h), new Vector2Int(w, h) })
            canvas.Clear(at.x, at.y);

        // The diffuser, in segments.
        for (int x = 4; x <= w - 3; x++)
            for (int y = 3; y <= h - 3; y++)
                canvas.Set(x, y, (x - 4) % 7 == 6 ? DiffuserSeam : PixelCanvas.Scale(Diffuser, 0.9f + 0.1f * (y - 3) / (h - 6)));

        // Cracked, with a piece missing.
        if (Chance(dice, 0.7f))
        {
            int x0 = Between(dice, 6, w - 8);
            canvas.Line(x0, 3, x0 + Between(dice, 2, 5), h - 3, Ink);
            if (Chance(dice, 0.5f)) canvas.FillRect(x0 + 1, 3, x0 + 3, 4, PixelCanvas.Scale(SteelDark, 0.6f));
        }
        return Finish(canvas, dice, 0.02f);
    }

    public static PixelCanvas Cables(System.Random dice)
    {
        int length = Between(dice, 44, 58), height = 20;
        var canvas = new PixelCanvas(length + 2, height + 2);
        int count = Between(dice, 3, 4);
        var used = new HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            int pick;
            do pick = dice.Next(Insulation.Length); while (!used.Add(pick) && used.Count < Insulation.Length);
            Color32 color = Insulation[pick];
            float baseY = 6 + i * 3.2f, sway = Between(dice, 1.5f, 3.5f), wave = Between(dice, 0.08f, 0.16f), phase = Between(dice, 0f, 6f);
            int end = length - Between(dice, 0, 8);
            int lastY = 0;
            for (int x = 7; x <= end; x++)
            {
                int y = Mathf.RoundToInt(baseY + sway * Mathf.Sin(x * wave + phase));
                canvas.Set(x, y + 1, PixelCanvas.Scale(color, 1.3f));
                canvas.Set(x, y, color);
                canvas.Set(x, y - 1, PixelCanvas.Scale(color, 0.65f));
                lastY = y;
            }
            // Frayed copper where it tore.
            for (int strand = -1; strand <= 1; strand++)
                canvas.Line(end + 1, lastY + strand, end + Between(dice, 2, 4), lastY + strand * 2, Copper);
        }

        // The connector it was pulled out of.
        canvas.FillRect(1, 3, 7, height - 3, SteelDark);
        canvas.FillRect(2, height - 4, 6, height - 3, HullMid);
        for (int y = 5; y < height - 4; y += 2) canvas.Set(7, y, Contact);
        return Finish(canvas, dice, 0.03f, bevel: false);
    }

    public static PixelCanvas Circuit(System.Random dice)
    {
        int w = Between(dice, 26, 32), h = Between(dice, 18, 22);
        var canvas = new PixelCanvas(w + 2, h + 2);
        canvas.FillRect(1, 1, w, h, Steel);
        canvas.FillRect(3, 3, w - 2, h - 2, Board);

        // Traces wandering across the board in right angles.
        for (int i = Between(dice, 4, 6); i > 0; i--)
        {
            int x = Between(dice, 4, w - 4), y = Between(dice, 4, h - 4);
            for (int step = 0; step < 4; step++)
            {
                int nx = step % 2 == 0 ? Between(dice, 4, w - 3) : x, ny = step % 2 == 0 ? y : Between(dice, 4, h - 3);
                canvas.Line(x, y, nx, ny, Trace);
                x = nx;
                y = ny;
            }
        }

        // Chips with their legs showing.
        for (int i = Between(dice, 2, 3); i > 0; i--)
        {
            int cw = Between(dice, 5, 8), ch = Between(dice, 3, 5);
            int x = Between(dice, 5, w - cw - 3), y = Between(dice, 6, h - ch - 3);
            canvas.FillRect(x, y, x + cw - 1, y + ch - 1, Chip);
            canvas.Set(x, y + ch - 1, PixelCanvas.Scale(Chip, 2.5f));
            for (int px = x; px < x + cw; px += 2)
            {
                canvas.Set(px, y - 1, Pins);
                canvas.Set(px, y + ch, Pins);
            }
        }

        // Gold contacts along the edge, and a status light.
        for (int x = 4; x < w - 3; x += 2)
        {
            canvas.Set(x, 3, Contact);
            canvas.Set(x, 4, PixelCanvas.Scale(Contact, 0.8f));
        }
        int ledX = Between(dice, 4, w - 4);
        canvas.Set(ledX, h - 3, Led);

        // Snapped across a corner, sometimes.
        if (Chance(dice, 0.4f))
        {
            int bite = Between(dice, 4, 8);
            for (int i = 0; i < bite; i++)
                for (int j = 0; j < bite - i; j++) canvas.Clear(w - i, h - j);
        }
        return Finish(canvas, dice, 0.03f);
    }

    public static PixelCanvas Scatter(System.Random dice)
    {
        var canvas = new PixelCanvas(42, 28);
        for (int i = Between(dice, 6, 9); i > 0; i--)
        {
            int x = Between(dice, 3, canvas.Width - 9), y = Between(dice, 3, canvas.Height - 8);
            switch (dice.Next(5))
            {
                case 0: // a bolt
                    canvas.FillRect(x, y, x + 3, y + 3, Steel);
                    canvas.Clear(x, y);
                    canvas.Clear(x + 3, y + 3);
                    canvas.Set(x + 1, y + 2, HullLight);
                    break;
                case 1: // a nut
                    canvas.FillRect(x, y, x + 4, y + 4, SteelDark);
                    canvas.FillRect(x + 1, y + 1, x + 3, y + 3, Steel);
                    canvas.Clear(x + 2, y + 2);
                    break;
                case 2: // a shard of panel
                    canvas.FillPolygon(new[] { new Vector2(x, y), new Vector2(x + Between(dice, 5, 8), y + Between(dice, 0, 2)), new Vector2(x + Between(dice, 1, 4), y + Between(dice, 4, 6)) }, HullLight);
                    break;
                case 3: // a snippet of cable
                    Color32 wire = Insulation[dice.Next(Insulation.Length)];
                    canvas.Line(x, y, x + Between(dice, 4, 7), y + Between(dice, -2, 2), wire, 2);
                    break;
                default: // a chip
                    canvas.FillRect(x, y, x + 4, y + 2, Chip);
                    canvas.Set(x, y - 1, Pins);
                    canvas.Set(x + 2, y - 1, Pins);
                    canvas.Set(x + 4, y - 1, Pins);
                    break;
            }
        }
        return Finish(canvas, dice, 0.03f);
    }

    // Wreckage heaped up: several pieces piled together, those at the back drawn first.
    public static PixelCanvas Pile(System.Random dice)
    {
        var canvas = new PixelCanvas(68, 46);
        var pieces = new List<(PixelCanvas art, int x, int y)>();
        System.Func<System.Random, PixelCanvas>[] makers = { Panel, Panel, Truss, Pipe, Circuit, Grate, Foil, Cables };
        int count = Between(dice, 5, 6);
        for (int i = 0; i < count; i++)
        {
            PixelCanvas art = makers[dice.Next(makers.Length)](dice);
            int x = Between(dice, 0, Mathf.Max(0, canvas.Width - art.Width));
            int y = Mathf.Min(canvas.Height - art.Height, Between(dice, 0, 4) + i * 3);
            pieces.Add((art, x, Mathf.Max(0, y)));
        }
        pieces.Sort((a, b) => b.y.CompareTo(a.y));
        foreach (var piece in pieces) canvas.Stamp(piece.art, piece.x, piece.y);
        canvas.Outline(Ink);
        return canvas;
    }

    // --- Robot parts ---

    // A piece knocked off a robot: a torn plate, a strut, a lens, a bolt, a board, or a length of its wiring. Small, so
    // a burst of them reads as one thing coming apart.
    public static PixelCanvas RobotPart(System.Random dice)
    {
        switch (dice.Next(6))
        {
            case 0: return Plate(dice);
            case 1: return Strut(dice);
            case 2: return Lens(dice);
            case 3: return Bolt(dice);
            case 4: return CircuitScrap(dice);
            default: return Wire(dice);
        }
    }

    static PixelCanvas Plate(System.Random dice)
    {
        int w = Between(dice, 7, 11), h = Between(dice, 5, 9);
        var canvas = new PixelCanvas(w + 2, h + 2);
        var corners = new Vector2[5];
        for (int i = 0; i < corners.Length; i++)
        {
            float angle = i / (float)corners.Length * Mathf.PI * 2f;
            corners[i] = new Vector2(w * 0.5f + 1 + Mathf.Cos(angle) * w * Between(dice, 0.3f, 0.5f),
                                     h * 0.5f + 1 + Mathf.Sin(angle) * h * Between(dice, 0.3f, 0.5f));
        }
        canvas.FillPolygon(corners, Chance(dice, 0.5f) ? HullMid : Steel);
        return Finish(canvas, dice, 0.05f);
    }

    static PixelCanvas Strut(System.Random dice)
    {
        int length = Between(dice, 9, 14);
        var canvas = new PixelCanvas(length + 2, 5);
        canvas.FillRect(1, 2, length, 3, Steel);
        canvas.FillRect(1, 1, 3, 3, SteelDark);
        canvas.FillRect(length - 2, 1, length, 3, SteelDark);
        return Finish(canvas, dice, 0.04f);
    }

    static PixelCanvas Lens(System.Random dice)
    {
        var canvas = new PixelCanvas(7, 7);
        canvas.FillEllipse(3.5f, 3.5f, 3f, 3f, (x, y) => SteelDark);
        canvas.FillEllipse(3.5f, 3.5f, 2f, 2f, (x, y) => Led);
        canvas.Set(3, 4, new Color32(255, 200, 190, 255));
        return Finish(canvas, dice, 0.03f, bevel: false);
    }

    static PixelCanvas Bolt(System.Random dice)
    {
        var canvas = new PixelCanvas(5, 5);
        canvas.FillRect(1, 1, 3, 3, Steel);
        canvas.Set(2, 2, SteelDark);
        return Finish(canvas, dice, 0.05f);
    }

    static PixelCanvas CircuitScrap(System.Random dice)
    {
        int w = Between(dice, 5, 8);
        var canvas = new PixelCanvas(w + 2, 6);
        canvas.FillRect(1, 1, w, 4, Board);
        canvas.Line(2, 3, w - 1, 3, Trace);
        canvas.FillRect(2, 1, 3, 2, Chip);
        for (int x = 2; x <= w; x += 2) canvas.Set(x, 1, Contact);
        return Finish(canvas, dice, 0.04f);
    }

    static PixelCanvas Wire(System.Random dice)
    {
        int length = Between(dice, 8, 13);
        var canvas = new PixelCanvas(length + 2, 7);
        Color32 color = Insulation[dice.Next(Insulation.Length)];
        int lastY = 3;
        for (int x = 1; x <= length; x++)
        {
            lastY = 3 + Mathf.RoundToInt(1.6f * Mathf.Sin(x * 0.5f));
            canvas.Set(x, lastY, color);
            canvas.Set(x, lastY + 1, PixelCanvas.Scale(color, 1.3f));
        }
        canvas.Set(length, lastY, Copper);
        return Finish(canvas, dice, 0.04f, bevel: false);
    }

    // --- Stains on the floor ---

    // A burn where something shorted out or caught fire: soot darkest in the middle, feathering out in specks.
    public static PixelCanvas Scorch(System.Random dice)
    {
        int radius = Between(dice, 12, 18);
        var canvas = new PixelCanvas(radius * 2 + 2, radius * 2 + 2);
        Vector2 middle = new Vector2(canvas.Width * 0.5f, canvas.Height * 0.5f);
        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                float reach = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), middle) / radius;
                float wobble = 0.85f + 0.3f * (float)dice.NextDouble();
                if (reach * wobble > 1f) continue;
                // Solid soot in the middle, broken into specks toward the edge.
                if (reach > 0.55f && dice.NextDouble() > (1f - reach) * 1.8f) continue;
                byte alpha = (byte)Mathf.Lerp(200f, 90f, reach);
                canvas.Set(x, y, new Color32(12, 10, 10, alpha));
            }
        }
        return canvas;
    }

    // Coolant leaked from a burst line: a flat, glossy blue pool with a highlight along its top edge.
    public static PixelCanvas CoolantSpill(System.Random dice)
    {
        int width = Between(dice, 26, 38), height = Between(dice, 12, 18);
        var canvas = new PixelCanvas(width + 2, height + 2);
        Vector2 middle = new Vector2(canvas.Width * 0.5f, canvas.Height * 0.5f);
        int lobes = Between(dice, 3, 4);
        var centers = new Vector2[lobes];
        var sizes = new Vector2[lobes];
        for (int i = 0; i < lobes; i++)
        {
            // Kept inside the canvas, so no side of the pool is cut straight.
            sizes[i] = new Vector2(Between(dice, width * 0.2f, width * 0.32f), Between(dice, height * 0.28f, height * 0.4f));
            centers[i] = middle + new Vector2(Between(dice, -1f, 1f) * (width * 0.5f - sizes[i].x), Between(dice, -1f, 1f) * (height * 0.5f - sizes[i].y));
        }
        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                for (int i = 0; i < lobes; i++)
                {
                    Vector2 d = new Vector2((x + 0.5f - centers[i].x) / sizes[i].x, (y + 0.5f - centers[i].y) / sizes[i].y);
                    if (d.sqrMagnitude > 1f) continue;
                    canvas.Set(x, y, new Color32(46, 118, 178, 150));
                    break;
                }
            }
        }
        // Lit along the top edge of the pool, darker round the rest.
        for (int y = 0; y < canvas.Height; y++)
            for (int x = 0; x < canvas.Width; x++)
                if (canvas.Filled(x, y) && !canvas.Filled(x, y + 1)) canvas.Set(x, y, new Color32(150, 214, 255, 200));
        canvas.Outline(new Color32(24, 60, 96, 120));
        return canvas;
    }

    static void Darken(PixelCanvas canvas, int x, int y, float amount)
    {
        if (canvas.Filled(x, y)) canvas.Set(x, y, PixelCanvas.Scale(canvas.Get(x, y), amount));
    }
}
