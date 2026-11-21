/** A short local chime; no audio downloads or notification permission required. */
export class FocusAlarmAudio {
  private context: AudioContext | null = null;

  async prepare(): Promise<boolean> {
    try {
      this.context ??= new AudioContext();
      if (this.context.state === "suspended") await this.context.resume();
      return this.context.state === "running";
    } catch { return false; }
  }

  async play(): Promise<boolean> {
    if (!await this.prepare() || !this.context) return false;
    const context = this.context;
    for (const [index, frequency] of [880, 1108.73, 1318.51].entries()) {
      const oscillator = context.createOscillator();
      const gain = context.createGain();
      const start = context.currentTime + index * 0.45;
      oscillator.type = "sine";
      oscillator.frequency.value = frequency;
      gain.gain.setValueAtTime(0, start);
      gain.gain.linearRampToValueAtTime(0.18, start + 0.015);
      gain.gain.exponentialRampToValueAtTime(0.001, start + 0.8);
      oscillator.connect(gain);
      gain.connect(context.destination);
      oscillator.onended = () => { oscillator.disconnect(); gain.disconnect(); };
      oscillator.start(start);
      oscillator.stop(start + 0.85);
    }
    return true;
  }

  close() {
    const context = this.context;
    this.context = null;
    if (context && context.state !== "closed") void context.close().catch(() => {});
  }
}
