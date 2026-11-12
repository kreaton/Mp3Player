import type {CSSProperties, ReactNode} from 'react';
import {Img, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {fontFamily, useAccent, useIsPhone} from './parts';
import {b} from './timing';

// Windows' own surfaces (taskbar preview, media overlay, notification), drawn in vector so they stay sharp at 4K.
// The app pictures inside them are cut from the real screenshots in public/app.

const beats = (n: number) => b(n) - b(0);

/** The same 16x16 shapes the app draws on its taskbar buttons (filled even-odd, so the trash can's slots are cut out). */
const GLYPHS = {
	delete: 'M6,1 H10 V2.5 H6 Z M2,2.5 H14 V4 H2 Z M3.5,5 H12.5 L11.7,15 H4.3 Z M6,7 H7 V13 H6 Z M9,7 H10 V13 H9 Z',
	previous: 'M14,2 L6,8 L14,14 Z M2,2 H5 V14 H2 Z',
	next: 'M2,2 L10,8 L2,14 Z M11,2 H14 V14 H11 Z',
	play: 'M4,2 L14,8 L4,14 Z',
	pause: 'M3,2 H7 V14 H3 Z M9,2 H13 V14 H9 Z',
	bell: 'M8,1.5 C5.2,1.5 4,3.6 4,6 V9.5 L2.5,12 H13.5 L12,9.5 V6 C12,3.6 10.8,1.5 8,1.5 Z M6.3,13 H9.7 C9.7,14 9,14.8 8,14.8 C7,14.8 6.3,14 6.3,13 Z',
};
export type GlyphName = keyof typeof GLYPHS;

export const Glyph = ({name, size, color = '#fff'}: {name: GlyphName; size: number; color?: string}) => (
	<svg width={size} height={size} viewBox="0 0 16 16" style={{display: 'block'}}>
		<path d={GLYPHS[name]} fill={color} fillRule="evenodd" />
	</svg>
);

/**
 * The cover art, cut out of a screenshot. Every capture has it in the same place, given here as fractions of the
 * capture, so captures at another display scaling (a different pixel size, same window shape) work too.
 */
const COVER = {x: 65 / 1426, y: 231 / 993, w: 318 / 1426, h: 318 / 993};

export const Cover = ({name, size, style}: {name: string; size: number; style?: CSSProperties}) => {
	const width = size / COVER.w;
	const height = size / COVER.h;
	return (
		<div style={{position: 'relative', width: size, height: size, borderRadius: size * 0.07, overflow: 'hidden', flexShrink: 0, ...style}}>
			<Img src={staticFile(`app/${name}.png`)} style={{position: 'absolute', width, height, left: -COVER.x * width, top: -COVER.y * height, maxWidth: 'none'}} />
		</div>
	);
};

const Pointer = ({x, y, down}: {x: number; y: number; down: boolean}) => (
	<svg width={34} height={46} viewBox="0 0 17 23" style={{position: 'absolute', left: x, top: y, transform: `scale(${down ? 0.86 : 1})`, transformOrigin: '0 0', filter: 'drop-shadow(0 4px 8px rgba(0,0,0,0.6))'}}>
		<path d="M1,1 L1,18 L5.5,13.8 L8.6,21 L11.6,19.7 L8.6,12.8 L14.6,12.8 Z" fill="#fff" stroke="#111" strokeWidth={1.2} strokeLinejoin="round" />
	</svg>
);

/** Where the picture of the desktop sits: where a screenshot would be, so the captions keep their place. */
const useDesk = () => {
	const phone = useIsPhone();
	return phone ? {left: 40, top: 700, width: 1000, height: 860} : {left: 630, top: 108, width: 1240, height: 863};
};

const Desk = ({children, enter = true}: {children: ReactNode; enter?: boolean}) => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const accent = useAccent();
	const desk = useDesk();
	const pop = enter ? spring({frame, fps, config: {damping: 16, stiffness: 170}}) : 1;
	return (
		<div
			style={{
				position: 'absolute',
				...desk,
				borderRadius: 14,
				overflow: 'hidden',
				background: 'linear-gradient(160deg, #1d1d2b, #0c0c14)',
				boxShadow: '0 40px 110px rgba(0,0,0,0.65), 0 0 0 1px rgba(255,255,255,0.12)',
				transform: `translateY(${(1 - pop) * 60}px) scale(${0.96 + pop * 0.04})`,
				opacity: Math.min(1, pop * 1.4),
				fontFamily: `"Segoe UI Variable Text", "Segoe UI", ${fontFamily}`,
				color: '#fff',
			}}
		>
			{/* a glow in the scene's colour, like the player's own backdrop */}
			<div style={{position: 'absolute', inset: 0, background: `radial-gradient(circle at 30% 20%, ${accent}, transparent 60%)`, opacity: 0.3}} />
			{children}
		</div>
	);
};

const ease = (frame: number, from: number, to: number) =>
	interpolate(frame, [from, to], [0, 1], {extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: (t) => t * t * (3 - 2 * t)});

// ---------------------------------------------------------------- taskbar preview

const TASKBAR_H = 64;
const S = 1.3; // the preview is drawn a little larger than life, so it reads in the video

/** Hover the taskbar icon, click the trash can, and the next track is already playing. */
export const TaskbarDesk = () => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const accent = useAccent();
	const {width: W, height: H} = useDesk();

	const appX = W / 2 + 28;
	const popW = 440 * S;
	const thumbW = popW - 24 * S;
	const thumbH = (thumbW * 993) / 1426;
	const popH = (12 + 34 + 10 + 40 + 12) * S + thumbH;
	const popLeft = Math.min(W - popW - 16, Math.max(16, appX - popW / 2));
	const popTop = H - TASKBAR_H - 12 - popH;
	const buttonsTop = popTop + (12 + 34 + 10) * S + thumbH;
	const buttonX = (i: number) => popLeft + popW / 2 + (i - 1.5) * 58 * S;

	const show = spring({frame: frame - beats(1.2), fps, config: {damping: 18, stiffness: 160}});
	const clickAt = beats(3.2);
	const deleted = ease(frame, clickAt + 2, clickAt + 10);

	// The pointer: onto the icon, up to the trash can, click, then off to one side.
	const p1 = ease(frame, 4, beats(1));
	const p2 = ease(frame, beats(2), beats(2.9));
	const p3 = ease(frame, beats(5), beats(6.2));
	const start = {x: W * 0.72, y: H * 0.45};
	const icon = {x: appX - 4, y: H - TASKBAR_H / 2 - 4};
	const trash = {x: buttonX(0) - 4, y: buttonsTop + 14 * S};
	const rest = {x: buttonX(3) + 70, y: buttonsTop - 60};
	const px = start.x + (icon.x - start.x) * p1 + (trash.x - icon.x) * p2 + (rest.x - trash.x) * p3;
	const py = start.y + (icon.y - start.y) * p1 + (trash.y - icon.y) * p2 + (rest.y - trash.y) * p3;
	const down = frame >= clickAt && frame < clickAt + 5;

	const hover = (i: number) => (i === 0 && p2 > 0.9 && p3 < 0.2 ? 0.16 : 0);
	const titles = ['Midnight Mopeds - Untitled Demo 7 (Copy) - MP3 Player', 'The Velvet Static - Afterglow FM - MP3 Player'];
	const notice = spring({frame: frame - clickAt - 6, fps, config: {damping: 16, stiffness: 160}});

	return (
		<Desk>
			{/* the option that makes one click enough */}
			<div style={{position: 'absolute', left: 30, top: 30, display: 'flex', alignItems: 'center', gap: 12, padding: '12px 22px', borderRadius: 999, background: accent, fontSize: 26, fontWeight: 600}}>
				<Glyph name="delete" size={24} />
				Delete without asking
			</div>
			<div
				style={{
					position: 'absolute',
					left: 30,
					top: 104,
					padding: '14px 22px',
					borderRadius: 14,
					background: 'rgba(255,255,255,0.1)',
					border: '1px solid rgba(255,255,255,0.14)',
					fontSize: 24,
					lineHeight: 1.35,
					opacity: Math.min(1, notice * 1.4),
					transform: `translateY(${(1 - notice) * 20}px)`,
				}}
			>
				"Untitled Demo 7 (Copy)" is in the Recycle Bin.
				<br />
				<span style={{color: '#c9c9d8'}}>Next up: Afterglow FM</span>
			</div>

			{/* the thumbnail preview */}
			<div
				style={{
					position: 'absolute',
					left: popLeft,
					top: popTop,
					width: popW,
					height: popH,
					padding: 12 * S,
					boxSizing: 'border-box',
					borderRadius: 10 * S,
					background: 'rgba(44,44,52,0.97)',
					border: '1px solid rgba(255,255,255,0.12)',
					boxShadow: '0 24px 60px rgba(0,0,0,0.6)',
					opacity: Math.min(1, show * 1.5),
					transform: `translateY(${(1 - show) * 24}px)`,
				}}
			>
				<div style={{display: 'flex', alignItems: 'center', gap: 10 * S, height: 34 * S}}>
					<Img src={staticFile('icon.png')} style={{width: 20 * S, height: 20 * S}} />
					<div style={{position: 'relative', flex: 1, height: 22 * S, fontSize: 15 * S}}>
						{titles.map((t, i) => (
							<div key={t} style={{position: 'absolute', inset: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', opacity: i === 0 ? 1 - deleted : deleted}}>
								{t}
							</div>
						))}
					</div>
				</div>
				<div style={{position: 'relative', width: thumbW, height: thumbH, borderRadius: 4, overflow: 'hidden'}}>
					<Img src={staticFile('app/main-mopeds.png')} style={{position: 'absolute', width: thumbW, height: thumbH}} />
					<Img src={staticFile('app/main-velvet.png')} style={{position: 'absolute', width: thumbW, height: thumbH, opacity: deleted}} />
				</div>
			</div>
			{(['delete', 'previous', 'pause', 'next'] as GlyphName[]).map((g, i) => (
				<div
					key={g}
					style={{
						position: 'absolute',
						left: buttonX(i) - 26 * S,
						top: buttonsTop,
						width: 52 * S,
						height: 40 * S,
						borderRadius: 6 * S,
						display: 'flex',
						alignItems: 'center',
						justifyContent: 'center',
						background: `rgba(255,255,255,${hover(i) + (i === 0 && down ? 0.12 : 0)})`,
						border: '1px solid rgba(255,255,255,0.1)',
						opacity: Math.min(1, show * 1.5),
						transform: `translateY(${(1 - show) * 24}px) scale(${i === 0 && down ? 0.92 : 1})`,
					}}
				>
					<Glyph name={g} size={16 * S} />
				</div>
			))}

			{/* the taskbar */}
			<div style={{position: 'absolute', left: 0, right: 0, bottom: 0, height: TASKBAR_H, background: 'rgba(28,28,36,0.95)', borderTop: '1px solid rgba(255,255,255,0.08)'}}>
				{Array.from({length: 8}, (_, i) => {
					const cx = W / 2 + (i - 3.5) * 56;
					const isApp = i === 4;
					return (
						<div key={i} style={{position: 'absolute', left: cx - 22, top: 10, width: 44, height: 44, borderRadius: 8, background: isApp && p1 > 0.95 ? 'rgba(255,255,255,0.12)' : 'transparent', display: 'flex', alignItems: 'center', justifyContent: 'center'}}>
							{isApp ? (
								<Img src={staticFile('icon.png')} style={{width: 34, height: 34}} />
							) : (
								<div style={{width: 30, height: 30, borderRadius: 7, background: 'rgba(255,255,255,0.16)'}} />
							)}
							{isApp ? <div style={{position: 'absolute', bottom: -4, width: 18, height: 4, borderRadius: 2, background: accent}} /> : null}
						</div>
					);
				})}
			</div>

			<Pointer x={px} y={py} down={down} />
		</Desk>
	);
};

// ---------------------------------------------------------------- media overlay and notification

const KeyCap = ({glyph, pressed}: {glyph: GlyphName; pressed: boolean}) => (
	<div
		style={{
			width: 92,
			height: 80,
			borderRadius: 14,
			display: 'flex',
			alignItems: 'center',
			justifyContent: 'center',
			background: pressed ? 'linear-gradient(#d5d5e0, #c4c4d2)' : 'linear-gradient(#f7f7fb, #d5d5e0)',
			boxShadow: pressed ? '0 2px 0 #8e8ea0, 0 6px 12px rgba(0,0,0,0.5)' : '0 6px 0 #8e8ea0, 0 13px 22px rgba(0,0,0,0.5)',
			transform: `translateY(${pressed ? 4 : 0}px)`,
		}}
	>
		<Glyph name={glyph} size={34} color="#16161f" />
	</div>
);

const TRACKS = [
	{cover: 'main-lena', title: 'Slow Bloom', artist: 'Lena Sol', album: 'Slow Bloom', at: 94, length: 182},
	{cover: 'main-velvet', title: 'Afterglow FM', artist: 'The Velvet Static', album: 'Afterglow FM', at: 0, length: 246},
];

const time = (s: number) => `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, '0')}`;

/** A media key from another app skips the track; the Windows overlay follows, and the notification says what's on. */
export const MediaDesk = () => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const accent = useAccent();
	const phone = useIsPhone();
	const {width: W} = useDesk();

	const pressAt = beats(2);
	const pressed = frame >= pressAt && frame < pressAt + 6;
	const next = ease(frame, pressAt + 2, pressAt + 9);
	const flyout = spring({frame: frame - beats(0.6), fps, config: {damping: 18, stiffness: 150}});
	const toast = spring({frame: frame - beats(3), fps, config: {damping: 17, stiffness: 140}});

	const cardW = phone ? 760 : 640;
	const cardLeft = (W - cardW) / 2;
	const cardTop = phone ? 170 : 150;

	return (
		<Desk>
			{/* the keyboard's media keys */}
			<div style={{position: 'absolute', left: 0, right: 0, top: 32, display: 'flex', justifyContent: 'center', gap: 22}}>
				<KeyCap glyph="previous" pressed={false} />
				<KeyCap glyph="pause" pressed={false} />
				<KeyCap glyph="next" pressed={pressed} />
			</div>

			{/* the Windows media overlay */}
			<div
				style={{
					position: 'absolute',
					left: cardLeft,
					top: cardTop,
					width: cardW,
					padding: 26,
					boxSizing: 'border-box',
					borderRadius: 14,
					background: 'rgba(40,40,48,0.96)',
					border: '1px solid rgba(255,255,255,0.12)',
					boxShadow: '0 30px 70px rgba(0,0,0,0.6)',
					opacity: Math.min(1, flyout * 1.4),
					transform: `translateY(${(1 - flyout) * 30}px)`,
				}}
			>
				<div style={{display: 'flex', alignItems: 'center', gap: 10, fontSize: 20, color: '#c9c9d8', marginBottom: 18}}>
					<Img src={staticFile('icon.png')} style={{width: 24, height: 24}} />
					MP3 Player
				</div>
				<div style={{position: 'relative', height: 132}}>
					{TRACKS.map((t, i) => {
						const o = i === 0 ? 1 - next : next;
						return (
							<div key={t.title} style={{position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', gap: 24, opacity: o}}>
								<Cover name={t.cover} size={132} />
								<div>
									<div style={{fontSize: 38, fontWeight: 700}}>{t.title}</div>
									<div style={{fontSize: 26, color: '#c9c9d8', marginTop: 4}}>{t.artist}</div>
								</div>
							</div>
						);
					})}
				</div>
				{(() => {
					const t = next < 0.5 ? TRACKS[0] : TRACKS[1];
					const since = next < 0.5 ? frame : frame - pressAt;
					const pos = Math.min(t.length, t.at + since / fps);
					return (
						<div style={{marginTop: 22}}>
							<div style={{height: 6, borderRadius: 3, background: 'rgba(255,255,255,0.18)'}}>
								<div style={{width: `${(pos / t.length) * 100}%`, height: '100%', borderRadius: 3, background: accent}} />
							</div>
							<div style={{display: 'flex', justifyContent: 'space-between', fontSize: 18, color: '#c9c9d8', marginTop: 8}}>
								<span>{time(pos)}</span>
								<span>{time(t.length)}</span>
							</div>
						</div>
					);
				})()}
				<div style={{display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 46, marginTop: 6}}>
					<Glyph name="previous" size={28} />
					<div style={{width: 64, height: 64, borderRadius: 32, background: 'rgba(255,255,255,0.12)', display: 'flex', alignItems: 'center', justifyContent: 'center'}}>
						<Glyph name="pause" size={28} />
					</div>
					<Glyph name="next" size={28} />
				</div>
			</div>

			{/* the optional notification, sliding in from the edge of the screen */}
			<div
				style={{
					position: 'absolute',
					right: 24,
					bottom: 24,
					width: phone ? 640 : 560,
					padding: 20,
					boxSizing: 'border-box',
					borderRadius: 12,
					background: 'rgba(48,48,58,0.98)',
					border: '1px solid rgba(255,255,255,0.14)',
					boxShadow: '0 24px 60px rgba(0,0,0,0.6)',
					transform: `translateX(${(1 - toast) * (W * 0.6)}px)`,
					opacity: Math.min(1, toast * 2),
				}}
			>
				<div style={{display: 'flex', alignItems: 'center', gap: 10, fontSize: 18, color: '#c9c9d8', marginBottom: 14}}>
					<Img src={staticFile('icon.png')} style={{width: 22, height: 22}} />
					<span style={{flex: 1}}>MP3 Player</span>
					<Glyph name="bell" size={20} color="#c9c9d8" />
				</div>
				<div style={{display: 'flex', alignItems: 'center', gap: 18}}>
					<Cover name="main-velvet" size={84} />
					<div>
						<div style={{fontSize: 28, fontWeight: 700}}>Afterglow FM</div>
						<div style={{fontSize: 22, color: '#c9c9d8', marginTop: 2}}>The Velvet Static • Afterglow FM</div>
					</div>
				</div>
			</div>
		</Desk>
	);
};
