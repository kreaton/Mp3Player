import type {ReactNode} from 'react';
import {AbsoluteFill, Html5Audio, Img, interpolate, Sequence, spring, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {ACCENT, Caption, Confetti, type Focus, fontFamily, Key, Shot, Stage, useAccent, useIsPhone, Visualizer, WHOLE, Words} from './parts';
import {b, beatPulse, SHORT_FRAMES, TOTAL_FRAMES} from './timing';
import {MediaDesk, TaskbarDesk} from './windows';

/** A sequence placed in beats of the song. */
const Beats = ({from, to, children}: {from: number; to: number; children: ReactNode}) => {
	const start = from === 0 ? 0 : b(from);
	return (
		<Sequence from={start} durationInFrames={b(to) - start}>
			{children}
		</Sequence>
	);
};

const useBeatFrames = (beats: number) => b(beats) - b(0);

// ---------------------------------------------------------------- opening

const Logo = () => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const icon = spring({frame, fps, config: {damping: 11, stiffness: 120}});
	const pulse = beatPulse(frame);
	return (
		<AbsoluteFill style={{alignItems: 'center', justifyContent: 'center', fontFamily, paddingBottom: 60}}>
			<Img src={staticFile('icon.png')} style={{width: 300, height: 300, transform: `scale(${icon * (1 + pulse * 0.05)})`, filter: 'drop-shadow(0 30px 60px rgba(0,0,0,0.55))'}} />
			<div style={{fontSize: 150, fontWeight: 800, color: '#fff', letterSpacing: -3, marginTop: 26}}>
				<Words text="MP3 Player" delay={14} step={6} />
			</div>
		</AbsoluteFill>
	);
};

const Grooviest = () => (
	<AbsoluteFill style={{alignItems: 'center', justifyContent: 'center', fontFamily, paddingBottom: 90, textAlign: 'center'}}>
		<div style={{fontSize: 148, fontWeight: 800, lineHeight: 1.02, color: '#fff', letterSpacing: -3, width: 1500}}>
			<Words text="The grooviest MP3 player around" step={4} />
		</div>
	</AbsoluteFill>
);

const ThreeWords = () => {
	const beat = useBeatFrames(1);
	const lines: [string, number, string][] = [
		['for the party.', 0, ACCENT.neon],
		['for the quiet listen.', beat, ACCENT.lena],
		['for the big clean-up.', beat * 2, ACCENT.mopeds],
	];
	return (
		<AbsoluteFill style={{justifyContent: 'center', paddingLeft: 190, paddingBottom: 90, fontFamily}}>
			{lines.map(([text, delay, color]) => (
				<div key={text} style={{fontSize: 136, fontWeight: 800, lineHeight: 1.16, letterSpacing: -2.5, color}}>
					<Words text={text} delay={delay} step={1} />
				</div>
			))}
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- party

const Covers = () => {
	const shots = ['main-neon', 'main-disco', 'main-comets', 'main-velvet'];
	const from = shots.map((_, i) => b(12 + i * 4) - b(12));
	return (
		<>
			<Caption kicker="Party night" title="Every album repaints the whole player" sub="Colours and backdrop follow the cover art." subDelay={18} />
			{shots.map((name, i) => (
				<Sequence key={name} from={from[i]} durationInFrames={(from[i + 1] ?? b(28) - b(12)) - from[i]}>
					<Shot name={name} from={{x: 0.5, y: 0.5, z: 1}} to={{x: 0.5, y: 0.5, z: 1.03}} enter={i === 0} phone={{from: {x: 0.28, y: 0.47, z: 1.45}, to: {x: 0.28, y: 0.47, z: 1.5}}} />
				</Sequence>
			))}
		</>
	);
};

const Crossfade = () => (
	<>
		<Caption title="Crossfade up to 18 seconds" sub="One song melts into the next. No dead air on the dancefloor." />
		<Shot name="main-neon" from={WHOLE} to={{x: 0.2, y: 0.9, z: 2.3}} moveFrames={26} enter={false} />
	</>
);

const ShuffleScene = () => (
	<>
		<Caption title="Shuffle with no repeats" sub="Every track plays once before any track comes round again." />
		<Shot name="main-disco" from={{x: 0.14, y: 0.92, z: 2.5}} to={{x: 0.14, y: 0.92, z: 2.7}} enter={false} />
	</>
);

const SearchScene = () => {
	const frame = useCurrentFrame();
	const beat = useBeatFrames(1);
	// Ctrl+F, then one letter per beat.
	const typed = Math.max(0, Math.min(7, Math.floor((frame - 2 * beat) / beat) + 1));
	const name = `search-${typed}`;
	const enterAt = b(56) - b(42) - beat;
	const camera: Focus = {x: 0.5, y: 0.52, z: 1.32};
	return (
		<>
			<Caption title="Someone shouts a request?" sub="Type any part of the artist, album or title. Enter plays it." subDelay={14}>
				<div style={{marginTop: 44, display: 'flex', alignItems: 'center', gap: 14, height: 90}}>
					{frame < enterAt ? (
						<>
							<Key>Ctrl</Key>
							<span style={{color: '#fff', fontSize: 44, fontFamily, fontWeight: 800}}>+</span>
							<Key>F</Key>
						</>
					) : (
						<Key>Enter</Key>
					)}
				</div>
			</Caption>
			<Shot name={name} from={camera} enter phone={{from: {x: 0.5, y: 0.52, z: 1.6}}} />
		</>
	);
};

const Requested = () => (
	<>
		<Caption title="Now playing. Everyone is happy." />
		<Shot name="main-comets" from={{x: 0.5, y: 0.5, z: 1.08}} to={WHOLE} moveFrames={30} enter={false} phone={{from: {x: 0.28, y: 0.47, z: 1.55}, to: {x: 0.28, y: 0.47, z: 1.45}}} />
	</>
);

// ---------------------------------------------------------------- listening

const Listen = () => (
	<>
		<Caption kicker="Quiet evening" title="Or just listen" sub="Cover art, every tag and the file details, all on one calm screen." subDelay={16} />
		<Shot name="main-lena" from={WHOLE} to={{x: 0.42, y: 0.42, z: 1.22}} phone={{from: {x: 0.28, y: 0.47, z: 1.35}, to: {x: 0.3, y: 0.42, z: 1.55}}} />
	</>
);

const Shortcut = ({keys, label, delay}: {keys: string[]; label: string; delay: number}) => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const phone = useIsPhone();
	const s = spring({frame: frame - delay, fps, config: {damping: 15, stiffness: 150}});
	return (
		<div style={{display: 'flex', alignItems: 'center', gap: 30, opacity: Math.min(1, s * 1.4), transform: `translateY(${(1 - s) * 30}px)`, height: 110}}>
			<div style={{display: 'flex', gap: 12, width: phone ? 340 : 370, justifyContent: 'flex-end'}}>
				{keys.map((k) => (
					<Key key={k} size={phone ? 40 : 44}>
						{k}
					</Key>
				))}
			</div>
			<div style={{fontFamily, color: '#fff', fontSize: phone ? 42 : 48, fontWeight: 600}}>{label}</div>
		</div>
	);
};

const Keyboard = () => {
	const beat = useBeatFrames(1);
	const phone = useIsPhone();
	const rows: [string[], string][] = [
		[['Space'], 'Play / pause'],
		[['Ctrl', '←', '→'], 'Previous / next'],
		[['←', '→'], 'Seek 5 seconds'],
		[['↑', '↓'], 'Volume'],
		[['S'], 'Shuffle'],
		[['Ctrl', 'F'], 'Search'],
	];
	return (
		<>
			<Shot name="main-lena" from={{x: 0.42, y: 0.42, z: 1.22}} style={{opacity: 0.18, filter: 'blur(4px)', ...(phone ? {} : {left: 340})}} enter={false} />
			<AbsoluteFill style={{alignItems: 'center', justifyContent: 'center', paddingBottom: 130, fontFamily}}>
				<div style={{fontSize: phone ? 92 : 104, lineHeight: 1.05, width: phone ? 900 : undefined, fontWeight: 800, color: '#fff', letterSpacing: -2, marginBottom: phone ? 50 : 70}}>
					<Words text="Your hands stay on the keys" />
				</div>
				<div style={{display: 'grid', gridTemplateColumns: phone ? 'auto' : 'auto auto', columnGap: 60, rowGap: phone ? 8 : 50}}>
					{rows.map(([keys, label], i) => (
						<Shortcut key={label} keys={keys} label={label} delay={8 + i * beat} />
					))}
				</div>
			</AbsoluteFill>
		</>
	);
};

const WindowsMedia = () => (
	<>
		<Caption
			title="Plays nicely with Windows"
			sub={useIsPhone() ? 'Media keys work from any app, and Windows shows what is playing.' : "Media keys work from any app, the Windows media overlay shows what's on, and an optional notification tells you when the track changes."}
			subDelay={14}
		/>
		<MediaDesk />
	</>
);

const Library = () => (
	<>
		<Caption title="Open one folder. Get them all." sub="Every MP3 in every subfolder. Your folder, volume and options are remembered." subDelay={14} />
		<Shot name="main-velvet" from={{x: 0.25, y: 0.07, z: 1.5}} to={WHOLE} moveFrames={60} />
	</>
);

// ---------------------------------------------------------------- clean-up

const CleanUp = () => (
	<>
		<Caption kicker="Clean-up time" title="Hear a track you never want again?" sub="Delete it right there, while you listen." subDelay={18} />
		<Shot name="main-mopeds" from={WHOLE} to={{x: 0.86, y: 0.08, z: 1.9}} moveFrames={40} phone={{from: {x: 0.28, y: 0.47, z: 1.35}, to: {x: 0.84, y: 0.08, z: 2.6}}} />
	</>
);

const DeleteTrack = () => (
	<>
		<Caption title="Asks first, if you want it to" sub="It goes to the Recycle Bin and the next track plays. Flip the trash toggle to skip the question." />
		<Shot name="delete-track" from={{x: 0.5, y: 0.5, z: 1.05}} to={{x: 0.5, y: 0.52, z: 1.5}} moveFrames={24} enter={false} phone={{from: {x: 0.5, y: 0.52, z: 1.5}, to: {x: 0.5, y: 0.52, z: 2.1}}} />
	</>
);

const DeleteFolder = () => (
	<>
		<Caption title="Whole album of bad demos?" sub={useIsPhone() ? 'One click takes the folder too.' : 'One click takes the folder too. You see exactly what goes before it does.'} subDelay={14} />
		<Shot name="delete-folder" from={{x: 0.5, y: 0.5, z: 1.5}} enter={false} phone={{from: {x: 0.5, y: 0.5, z: 2}}} />
	</>
);

const TaskbarDelete = () => (
	<>
		<Caption title="Or straight from the taskbar" sub="Hover the icon for delete, previous, play and next. No need to open the window." subDelay={14} />
		<TaskbarDesk />
	</>
);

// ---------------------------------------------------------------- ending

const Finale = () => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const icon = spring({frame, fps, config: {damping: 11, stiffness: 120}});
	const pulse = beatPulse(frame);
	return (
		<AbsoluteFill style={{alignItems: 'center', justifyContent: 'center', fontFamily, textAlign: 'center', paddingBottom: 80}}>
			<Img src={staticFile('icon.png')} style={{width: 220, height: 220, transform: `scale(${icon * (1 + pulse * 0.05)})`, filter: 'drop-shadow(0 30px 60px rgba(0,0,0,0.55))'}} />
			<div style={{fontSize: 130, fontWeight: 800, color: '#fff', letterSpacing: -3, marginTop: 20}}>
				<Words text="MP3 Player" delay={6} step={4} />
			</div>
			<div style={{fontSize: 62, fontWeight: 600, color: '#fff', marginTop: 16}}>
				<Words text="Party. Listen. Clean up." delay={18} step={8} />
			</div>
			<div style={{fontSize: 36, fontWeight: 400, color: '#d8d8e4', marginTop: 26}}>
				<Words text="For Windows" delay={46} step={3} />
			</div>
		</AbsoluteFill>
	);
};

const CREDIT_ROWS: [string, string][] = [
	['Music', 'GROZA at Pixabay'],
	['Made by', 'Per Hejndorf, with Claude (Anthropic)'],
	['Audio', 'NAudio by Mark Heath (MIT)'],
	['Tags', 'TagLibSharp (LGPL-2.1)'],
	['Icons', 'Segoe Fluent Icons by Microsoft'],
];

const Credit = ({label, value, delay}: {label: string; value: string; delay: number}) => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const phone = useIsPhone();
	const s = spring({frame: frame - delay, fps, config: {damping: 200}});
	return (
		<div style={{opacity: s, transform: `translateY(${(1 - s) * 16}px)`, marginBottom: phone ? 34 : 26}}>
			<div style={{fontSize: phone ? 26 : 24, fontWeight: 800, letterSpacing: 3, textTransform: 'uppercase', color: '#c9c9d8'}}>{label}</div>
			<div style={{fontSize: phone ? 46 : 40, fontWeight: 600, color: '#fff', lineHeight: 1.2}}>{value}</div>
		</div>
	);
};

const Credits = () => {
	const frame = useCurrentFrame();
	const accent = useAccent();
	const {durationInFrames} = useVideoConfig();
	const fade = interpolate(frame, [durationInFrames - 24, durationInFrames - 2], [0, 1], {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'});
	return (
		<>
			<Shot name="about" from={{x: 0.5, y: 0.5, z: 1.85}} width={900} style={{left: 60, right: 'auto'}} />
			<div style={{position: 'absolute', left: 1020, top: 0, bottom: 110, width: 840, display: 'flex', flexDirection: 'column', justifyContent: 'center', fontFamily}}>
				<div style={{fontSize: 74, fontWeight: 800, color: '#fff', letterSpacing: -1.5, marginBottom: 32, textShadow: `0 0 34px ${accent}`}}>
					<Words text="Credits" />
				</div>
				{CREDIT_ROWS.map(([label, value], i) => (
					<Credit key={label} label={label} value={value} delay={6 + i * 6} />
				))}
				<div style={{fontSize: 30, fontWeight: 400, color: '#d8d8e4', marginTop: 8, opacity: interpolate(frame, [40, 60], [0, 1], {extrapolateRight: 'clamp'})}}>
					© 2026 Per Hejndorf · Version 1.1.2
					<br />
					Artists and albums shown are fictional.
				</div>
			</div>
			<AbsoluteFill style={{background: '#000', opacity: fade}} />
		</>
	);
};

// ---------------------------------------------------------------- timeline, in beats of the song

export const Promo = () => {
	const volume = (frame: number) =>
		interpolate(frame, [b(149), TOTAL_FRAMES - 4], [1, 0], {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'});
	return (
		<AbsoluteFill style={{background: '#0a0a10'}}>
			<Html5Audio src={staticFile('music.mp3')} volume={volume} />
			<Stage />
			<Beats from={0} to={12}>
				<Confetti count={30} clearCaption={false} />
			</Beats>
			<Beats from={12} to={60}>
				<Confetti />
			</Beats>
			<Beats from={92} to={132}>
				<Confetti count={18} />
			</Beats>
			<Visualizer />

			<Beats from={0} to={4}><Logo /></Beats>
			<Beats from={4} to={8}><Grooviest /></Beats>
			<Beats from={8} to={12}><ThreeWords /></Beats>

			<Beats from={12} to={28}><Covers /></Beats>
			<Beats from={28} to={36}><Crossfade /></Beats>
			<Beats from={36} to={42}><ShuffleScene /></Beats>
			<Beats from={42} to={56}><SearchScene /></Beats>
			<Beats from={56} to={60}><Requested /></Beats>

			<Beats from={60} to={68}><Listen /></Beats>
			<Beats from={68} to={76}><Keyboard /></Beats>
			<Beats from={76} to={84}><WindowsMedia /></Beats>
			<Beats from={84} to={92}><Library /></Beats>

			<Beats from={92} to={100}><CleanUp /></Beats>
			<Beats from={100} to={108}><DeleteTrack /></Beats>
			<Beats from={108} to={116}><DeleteFolder /></Beats>
			<Beats from={116} to={124}><TaskbarDelete /></Beats>

			<Beats from={124} to={132}><Finale /></Beats>
			<Beats from={132} to={154}><Credits /></Beats>
		</AbsoluteFill>
	);
};

// ---------------------------------------------------------------- the Short: portrait and under a minute

const PhoneHook = () => {
	const frame = useCurrentFrame();
	const {fps} = useVideoConfig();
	const icon = spring({frame, fps, config: {damping: 11, stiffness: 120}});
	const pulse = beatPulse(frame);
	return (
		<AbsoluteFill style={{alignItems: 'center', justifyContent: 'center', fontFamily, textAlign: 'center', paddingBottom: 240}}>
			<Img src={staticFile('icon.png')} style={{width: 260, height: 260, transform: `scale(${icon * (1 + pulse * 0.05)})`, filter: 'drop-shadow(0 30px 60px rgba(0,0,0,0.55))'}} />
			<div style={{fontSize: 136, fontWeight: 800, lineHeight: 1.02, color: '#fff', letterSpacing: -3, width: 960, marginTop: 40}}>
				<Words text="The grooviest MP3 player around" step={3} />
			</div>
		</AbsoluteFill>
	);
};

const PhoneThreeWords = () => {
	const beat = useBeatFrames(1);
	const lines: [string, number, string][] = [
		['for the party.', 0, ACCENT.neon],
		['for the quiet listen.', beat, ACCENT.lena],
		['for the big clean-up.', beat * 2, ACCENT.mopeds],
	];
	return (
		<AbsoluteFill style={{justifyContent: 'center', paddingLeft: 70, paddingBottom: 240, fontFamily}}>
			{lines.map(([text, delay, color]) => (
				<div key={text} style={{fontSize: 118, fontWeight: 800, lineHeight: 1.08, letterSpacing: -2.5, color, width: 900, marginBottom: 34}}>
					<Words text={text} delay={delay} step={1} />
				</div>
			))}
		</AbsoluteFill>
	);
};

/** The finale and the credits on one screen, because a Short has no time for both. */
const PhoneEnding = () => {
	const frame = useCurrentFrame();
	const {fps, durationInFrames} = useVideoConfig();
	const icon = spring({frame, fps, config: {damping: 11, stiffness: 120}});
	const pulse = beatPulse(frame);
	const fade = interpolate(frame, [durationInFrames - 14, durationInFrames - 2], [0, 1], {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'});
	return (
		<>
			<div style={{position: 'absolute', left: 60, top: 170, width: 880, fontFamily}}>
				<div style={{display: 'flex', alignItems: 'center', gap: 30}}>
					<Img src={staticFile('icon.png')} style={{width: 150, height: 150, transform: `scale(${icon * (1 + pulse * 0.05)})`}} />
					<div style={{fontSize: 112, fontWeight: 800, color: '#fff', letterSpacing: -3}}>
						<Words text="MP3 Player" delay={3} step={3} />
					</div>
				</div>
				<div style={{fontSize: 60, fontWeight: 600, color: '#fff', margin: '26px 0 60px'}}>
					<Words text="Party. Listen. Clean up." delay={8} step={4} />
				</div>
				{CREDIT_ROWS.map(([label, value], i) => (
					<Credit key={label} label={label} value={value} delay={10 + i * 3} />
				))}
				<div style={{fontSize: 32, fontWeight: 400, lineHeight: 1.35, color: '#d8d8e4', marginTop: 12, opacity: interpolate(frame, [24, 36], [0, 1], {extrapolateRight: 'clamp'})}}>
					© 2026 Per Hejndorf · Version 1.1.2
					<br />
					Artists and albums shown are fictional.
				</div>
			</div>
			<AbsoluteFill style={{background: '#000', opacity: fade}} />
		</>
	);
};

export const PromoShort = () => {
	const volume = (frame: number) =>
		interpolate(frame, [b(97), SHORT_FRAMES - 4], [1, 0], {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'});
	return (
		<AbsoluteFill style={{background: '#0a0a10'}}>
			<Html5Audio src={staticFile('music.mp3')} volume={volume} />
			<Stage />
			<Beats from={0} to={12}>
				<Confetti count={30} clearCaption={false} />
			</Beats>
			<Beats from={12} to={60}>
				<Confetti />
			</Beats>
			<Beats from={74} to={90}>
				<Confetti count={18} />
			</Beats>
			<Visualizer />

			<Beats from={0} to={6}><PhoneHook /></Beats>
			<Beats from={6} to={12}><PhoneThreeWords /></Beats>

			<Beats from={12} to={28}><Covers /></Beats>
			<Beats from={28} to={36}><Crossfade /></Beats>
			<Beats from={36} to={42}><ShuffleScene /></Beats>
			<Beats from={42} to={56}><SearchScene /></Beats>
			<Beats from={56} to={60}><Requested /></Beats>

			{/* No room for the keyboard shortcuts in a Short: the Windows integration takes their place. */}
			<Beats from={60} to={65}><Listen /></Beats>
			<Beats from={65} to={70}><WindowsMedia /></Beats>
			<Beats from={70} to={74}><TaskbarDelete /></Beats>

			<Beats from={74} to={79}><CleanUp /></Beats>
			<Beats from={79} to={85}><DeleteTrack /></Beats>
			<Beats from={85} to={90}><DeleteFolder /></Beats>

			<Sequence from={b(90)} durationInFrames={SHORT_FRAMES - b(90)}><PhoneEnding /></Sequence>
		</AbsoluteFill>
	);
};
