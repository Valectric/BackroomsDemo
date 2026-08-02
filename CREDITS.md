# Credits

The code is MIT (see [LICENSE](LICENSE)). Every bundled asset is **CC0 1.0** — public domain,
no attribution required. It is given anyway, because the people below chose to give their work away
and that is worth naming.

This is also the evidence for the repository's own rule: **CC0 assets only**, so the project stays
clonable, buildable and redistributable by anyone. Nothing here is licensed on terms that a fork
would have to renegotiate.

## 3D models

| Asset | Author | Licence | Source |
|---|---|---|---|
| Furniture Kit | [Kenney](https://kenney.nl) | CC0 1.0 | [kenney.nl/assets/furniture-kit](https://kenney.nl/assets/furniture-kit) |

Lives in `Assets/ThirdParty/Kenney/FurnitureKit/` with its original `License.txt` intact.

## Audio

Seven recordings, one or two per floor theme, from [Freesound](https://freesound.org). Each was
verified CC0 on its own page before download — the fetch script refuses anything that does not say
so — then re-encoded to mono Ogg Vorbis and processed to sound roughly a hundred metres away.

| File | Floor theme | Author | Licence | Source |
|---|---|---|---|---|
| `office/yellow-rooms-ballast.ogg` | The Yellow Rooms | TRP | CC0 1.0 | [freesound 577277](https://freesound.org/people/TRP/sounds/577277/) |
| `mall/mall-chime.ogg` | Abandoned Mall | Helmer88 | CC0 1.0 | [freesound 720037](https://freesound.org/people/Helmer88/sounds/720037/) |
| `laundromat/laundromat-cycle-done.ogg` | Janky Laundromat | KaleidacousticsAudio | CC0 1.0 | [freesound 630439](https://freesound.org/people/KaleidacousticsAudio/sounds/630439/) |
| `carnival/carnival-street-organ.ogg` | Twisted Carnival | Breviceps | CC0 1.0 | [freesound 644832](https://freesound.org/people/Breviceps/sounds/644832/) |
| `carnival/carnival-carousel.ogg` | Twisted Carnival | tec_studio | CC0 1.0 | [freesound 98180](https://freesound.org/people/tec_studio/sounds/98180/) |
| `asylum/asylum-door-slam.ogg` | Condemned Asylum | qubodup | CC0 1.0 | [freesound 218888](https://freesound.org/people/qubodup/sounds/218888/) |
| `asylum/asylum-door-shut.ogg` | Condemned Asylum | qubodup | CC0 1.0 | [freesound 218890](https://freesound.org/people/qubodup/sounds/218890/) |

Lives in `Assets/ThirdParty/FreesoundCC0/`, with per-file provenance in
[`SOURCES.md`](Assets/ThirdParty/FreesoundCC0/SOURCES.md).

**All other audio is synthesised in code** — the room hum, the pursuit drone, footsteps, the relic
chime, the descent tone, the blink and the banisher. A waveform computed from a formula has no
licence, which is how a repo restricted to CC0 gets a soundtrack at all.

## Everything else

Generated procedurally in code, with no imported art: the level geometry, the wallpaper, carpet and
ceiling-tile textures, skirting, columns, stairwells, relics and the Dwellers.

## Not bundled

**[MooseRunner](https://mooserunner.com)** — the test runner this project exists to showcase — is a
separate commercial product and is **not** redistributed here. It resolves as a Unity package from
the public npm registry (`com.valectric.mooserunner`), and its CLI lives in a gitignored folder.

## Setting

Inspired by *Discount Dan: A LitRPG Adventure* by **James A. Hunter**. This is an unofficial fan
demo, not affiliated with or endorsed by the author, and no text or content from the books is
reproduced in it.
