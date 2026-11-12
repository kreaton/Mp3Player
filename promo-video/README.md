# MP3 Player promo video

Two introductions to the player, made with [Remotion](https://www.remotion.dev/), both in 4K for YouTube:

| Composition | Format | Length | File |
|---|---|---|---|
| `Promo` | Landscape, 3840×2160 | 92 s | `out/mp3player-promo-4k.mp4` |
| `PromoShort` | Portrait, 2160×3840, for YouTube Shorts | Under 60 s | `out/mp3player-short-4k.mp4` |

The landscape video is edited to the music: every cut is placed in beats of the song, and the sections follow the song's structure (party on the loud opening, listening and the Windows integration on the breakdown, clean-up and the taskbar buttons on the build back up).

## Setup

The music isn't in the repository, because the Pixabay track shouldn't be redistributed on its own. Download it and save it as `public/music.mp3`.

```bash
npm install
```

## Preview and render

```bash
npm run studio
```

```bash
npm run render
```

That renders both videos. `npm run render:landscape` and `npm run render:short` render one each. Rendering downloads a headless Chrome the first time, and fetches the Bricolage Grotesque font from Google Fonts.

## Files

| Path | Contents |
|---|---|
| `src/Promo.tsx` | The scenes and the timeline, which is the place to change wording or timing. |
| `src/parts.tsx` | Background, music visualizer, confetti, app window with camera, captions and key caps. |
| `src/windows.tsx` | The Windows parts: taskbar preview with its buttons, the media overlay and the notification, drawn in vector around cut-outs of the screenshots. |
| `src/timing.ts` | Converts beats of the song to frames. |
| `src/beat.json` | Tempo and first beat, written by `scripts/analyze-beat.mjs`. Run that script again if you change the music. |
| `public/app/` | Screenshots of the real WPF app. |

## About the Short

YouTube accepts Shorts of up to three minutes, but blocks a Short of one to three minutes if it has an active Content ID claim. So this one stays under a minute (`SHORT_FRAMES` in `src/timing.ts`). It plays the song unedited from the start and leaves out the folder and keyboard scenes. Its captions stay in the upper part of the frame, and the app window stops above where YouTube draws its title and buttons.

## About the screenshots

They show the real app, playing a demo library of silent MP3s with invented artists, albums and generated cover art. The video's credits say the artists are fictional. To update them after changing the UI, capture the app again at the same window size and keep the file names.

The captures are 1426×993, so in the 4K videos they're scaled up and look softer than the text and graphics, which are drawn at full 4K.

## Credits

- Music: GROZA at Pixabay
- App credits are the ones on the app's About screen
