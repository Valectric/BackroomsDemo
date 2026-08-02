# Third-party audio — Freesound, CC0 only

Every file here is **CC0 1.0 (public domain dedication)**. CC0 requires no attribution; this list
exists to record provenance, so anyone cloning the repo can re-verify the licence themselves rather
than take it on trust.

Downloaded from Freesound's public preview streams, then re-encoded to mono Ogg Vorbis at 22.05 kHz
and trimmed, because this ships inside a mobile WebGL build where every kilobyte is part of the
download.

They are also **deliberately processed to sound about a hundred metres off**: band-limited to roughly
110–700 Hz, given a long soft echo, and normalised far under the mix. Distance is spectral before it
is quiet — air and structure eat the high end long before they eat the level — so a source left
bright and merely turned down reads as "the same room, quieter" rather than "somewhere else in the
building". The unprocessed originals are at the Freesound links below.

| File | Theme | Freesound | Uploader | Licence |
|---|---|---|---|---|
| `office/yellow-rooms-ballast.ogg` | THE YELLOW ROOMS | [577277](https://freesound.org/people/TRP/sounds/577277/) | TRP | CC0 1.0 |
| `mall/mall-chime.ogg` | ABANDONED MALL | [720037](https://freesound.org/people/Helmer88/sounds/720037/) | Helmer88 | CC0 1.0 |
| `laundromat/laundromat-cycle-done.ogg` | JANKY LAUNDROMAT | [630439](https://freesound.org/people/KaleidacousticsAudio/sounds/630439/) | KaleidacousticsAudio | CC0 1.0 |
| `carnival/carnival-street-organ.ogg` | TWISTED CARNIVAL | [644832](https://freesound.org/people/Breviceps/sounds/644832/) | Breviceps | CC0 1.0 |
| `carnival/carnival-carousel.ogg` | TWISTED CARNIVAL | [98180](https://freesound.org/people/tec_studio/sounds/98180/) | tec_studio | CC0 1.0 |
| `asylum/asylum-door-slam.ogg` | CONDEMNED ASYLUM | [218888](https://freesound.org/people/qubodup/sounds/218888/) | qubodup | CC0 1.0 |
| `asylum/asylum-door-shut.ogg` | CONDEMNED ASYLUM | [218890](https://freesound.org/people/qubodup/sounds/218890/) | qubodup | CC0 1.0 |

Folder names match `PropStyle`, the enum a `FloorTheme` already carries, so a floor's ambience is
found from the theme it is built with rather than from a second table that could drift out of step.
