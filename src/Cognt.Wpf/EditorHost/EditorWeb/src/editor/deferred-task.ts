export type DeferredTaskIdleOptions = {
  useIdle?: boolean;
  timeout?: number;
};

export class DeferredTask {
  private timer: number | undefined;
  private idleId: number | undefined;

  public get isScheduled() {
    return this.timer !== undefined || this.idleId !== undefined;
  }

  public schedule(
    delayMs: number,
    task: () => void | Promise<void>,
    idleOptions: DeferredTaskIdleOptions = {}
  ) {
    this.cancel();
    this.timer = window.setTimeout(() => {
      this.timer = undefined;
      const run = () => {
        this.idleId = undefined;
        void task();
      };

      if (idleOptions.useIdle && window.requestIdleCallback) {
        this.idleId = window.requestIdleCallback(run, {
          timeout: idleOptions.timeout,
        });
        return;
      }

      run();
    }, delayMs);
  }

  public cancel() {
    window.clearTimeout(this.timer);
    this.timer = undefined;

    if (this.idleId !== undefined) {
      window.cancelIdleCallback?.(this.idleId);
      this.idleId = undefined;
    }
  }
}
