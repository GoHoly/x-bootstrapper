# Skyboxes

X Bootstrapper's sky picker (Mods page) replaces the Octane client's default sky. This page records what the
client uses and how X Bootstrapper writes it.

## What the client uses

The 2021 client (`%LOCALAPPDATA%\Octane\clients\2021`) draws its default sky from six cube faces in
`content\textures\sky`:

| File | Face |
| --- | --- |
| `sky512_ft.tex` | front (+Z) |
| `sky512_bk.tex` | back (-Z) |
| `sky512_lf.tex` | left |
| `sky512_rt.tex` | right |
| `sky512_up.tex` | top |
| `sky512_dn.tex` | bottom |

Despite the name and the `.tex` extension, each file is a plain DDS texture:

- 1024 x 1024, DXT1 (BC1), 11 mip levels, 699,192 bytes.
- The header was written by NVIDIA Texture Tools 2.1.0: flags `0x000A1007`, linear size `0x80000`,
  `UVER`/`NVTT` markers in the reserved area, caps `0x00401008`.

The folder also holds `indoor512_*.tex` (512 px, 10 mips), which X Bootstrapper doesn't touch. The old
512 px JPG sky under `content\sky` and `skyspheremap.jpg` aren't used for the default sky either.
`ExtraContent\textures\sky` only has `white.png`.

Seam matching of the stock faces gives this orientation (u to the right and v down, both in [-1, 1];
X right, Y up, Z toward `ft`):

| Face | Direction |
| --- | --- |
| ft | ( u, -v,  1) |
| lf | ( 1, -v, -u) |
| bk | (-u, -v, -1) |
| rt | (-1, -v,  u) |
| up | (-v,  1,  u) |
| dn | ( v, -1,  u) |

Going around the horizon from left to right: rt, ft, lf, bk.

Games that set their own `Sky` object override the default sky, so the picker only changes places that
don't set one.

## How X Bootstrapper writes a sky

- **Built-in skies** (Sunset, Starry night, Purple nebula, Clear day) are generated in code by
  `src/Caelus/Services/SkyboxService.cs`. Every pixel comes from a small procedural shader evaluated in the
  direction of that pixel, so the faces line up without seams. No third-party images are used.
- **Custom skies** take six images of any size and format that WPF can read. Each image is scaled to
  1024 x 1024. Mods → Custom opens an editor with per-face upload slots and a 3D skybox preview (drag to
  look around) that uses the same theme/style as the rest of the app.
- Every face is encoded to DXT1 with a principal-axis block encoder and 11 box-filtered mips, and written
  with a header identical to the stock one. The files end up the same size as the originals.
- The faces go to `Modifications\content\textures\sky\sky512_*.tex` and are applied like any other mod:
  the originals are backed up in `ModBackups` first. `Modifications\xb-sky.txt` records which sky is set.
- On launch, X Bootstrapper reapplies the sky while OctanePlayerLauncher starts the player (the official
  launcher can restore stock files) and once more when the player process appears.
- **Default** removes the sky files from Modifications and restores the backed-up originals, byte for byte
  (checked with SHA-256).
- Sky files aren't mirrored into `ExtraContent`.
