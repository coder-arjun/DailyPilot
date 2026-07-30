import { createAudioPlayer } from 'expo-audio';

/**
 * Short UI sounds. The completion chime is a synthesized asset (C6–E6–G6
 * arpeggio, ~0.7s) bundled with the app — nothing licensed or downloaded.
 * One module-level player, rewound per play; failures are silently ignored
 * (a missing sound must never break a task action).
 */
let completionPlayer: ReturnType<typeof createAudioPlayer> | null = null;

export function playCompletionTone(): void {
  try {
    completionPlayer ??= createAudioPlayer(require('../../assets/sounds/complete.wav'));
    completionPlayer.seekTo(0);
    completionPlayer.play();
  } catch {
    // audio unavailable (e.g. some emulators) — the action still succeeds
  }
}
