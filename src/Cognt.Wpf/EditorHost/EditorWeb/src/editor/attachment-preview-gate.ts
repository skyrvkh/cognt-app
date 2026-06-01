export class AttachmentPreviewGate {
  private pendingPreviewCount = 0;

  public constructor(private readonly cancelPendingContentChanged: () => void) {}

  public get hasPendingPreview() {
    return this.pendingPreviewCount > 0;
  }

  public begin() {
    this.pendingPreviewCount++;
    this.cancelPendingContentChanged();
  }

  public end() {
    this.pendingPreviewCount = Math.max(0, this.pendingPreviewCount - 1);
  }
}
