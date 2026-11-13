import beat from './beat.json';

export const FPS = 30;
const SECONDS_PER_BEAT = 60 / beat.bpm;

/** The frame of a beat of the song; every cut in the video is placed with this. */
export const b = (n: number) => Math.round((beat.firstBeat + n * SECONDS_PER_BEAT) * FPS);

/**
 * The landscape video starts the song 8 beats in, so its opening is quicker but every later scene still sits on the same
 * part of the song. In the video's own beats the song is loud to 52, quiet to 84, loud to 130, quiet to 161, loud to the end.
 */
export const PROMO_MUSIC_SKIP = Math.round(8 * SECONDS_PER_BEAT * FPS);

/** Length of the video: the credits end with a fade in the song's last loud section. */
export const TOTAL_FRAMES = b(169);

/** Length of the portrait Short: under a minute, so a claim on the music can't get it blocked. */
export const SHORT_FRAMES = b(100);

/** 1 right on a beat, decaying towards 0 before the next one. */
export const beatPulse = (frame: number) => {
	const position = (frame / FPS - beat.firstBeat) / SECONDS_PER_BEAT;
	if (position < 0) return 0;
	return Math.exp(-3.5 * (position % 1));
};
